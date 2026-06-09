// SimpleSFTPSync by ScottRFrost - https://github.com/ScottRFrost/SimpleSFTPSync
// Build with: dotnet compile
// Publish with: dotnet publish -r win10-x64 / dotnet publish -r ubuntu.16.10-x64 etc

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using System.IO;
using System.Text;
using System.Diagnostics; // For process and process start
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SimpleSFTPSyncCore;

public class SimpleSFTPSync
{
    private readonly ConfigFile config;
    private static string logPath = string.Empty;
    private readonly System.Threading.Lock dbLock = new();
    private readonly System.Threading.Lock logLock = new();
    private readonly SimpleSFTPSyncCoreContext db;
    private readonly List<string> rars = [];
    private readonly List<string> mkvs = []; // also has mp4s

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public SimpleSFTPSync()
    {
        // Connect DB
        db = new SimpleSFTPSyncCoreContext();

        // Open Log File
        logPath = Path.Combine(Directory.GetCurrentDirectory(), DateTime.Now.ToString("MM-dd-yyyy") + ".log");
        Console.WriteLine("Logging to " + logPath);

        // Read configuration
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
        Log("Reading Config from " + configPath);
        var fileText = File.ReadAllText(configPath);
        config = JsonSerializer.Deserialize<ConfigFile>(fileText, _jsonOptions) 
                 ?? throw new InvalidOperationException("Failed to deserialize configuration file.");
        Log("Configuration Read");
    }

    /// <summary>
    /// Starts a new SFTP connect and download run
    /// </summary>
    public async Task StartRun()
    {
        try
        {
            // Make SFTP connection and do work
            using (var sftp = new SftpClient(config.hostname, config.port, config.username, config.password))
            {
                // Connect and scan
                sftp.Connect();
                Log("Connected to " + config.hostname + " running " + sftp.ConnectionInfo.ServerVersion);
                Log("Checking for new files to download...");
                foreach (var remoteDir in config.remoteDir)
                {
                    Log("Checking for new files in " + remoteDir);
                    var foundFiles = ListFilesRecursive(sftp, remoteDir, string.Empty).ToString();
                    Log(foundFiles + " files found for download in " + remoteDir);
                }

                // Download and queue up unrar / rename.
                foreach (var syncFile in db.SyncFile.Where(f => f.DateDownloaded == null).OrderBy(f => f.DateDiscovered))
                {
                    try
                    {
                        var localPath = config.downloadDir + syncFile.RemotePath.Replace('/', Path.DirectorySeparatorChar).Replace(Path.DirectorySeparatorChar.ToString() + Path.DirectorySeparatorChar.ToString(), Path.DirectorySeparatorChar.ToString());
                        var localDirectory = localPath[..localPath.LastIndexOf(Path.DirectorySeparatorChar)];
                        if (File.Exists(localPath))
                        {
                            var localFile = new FileInfo(localPath);
                            if (localFile.Length == syncFile.Length)
                            {
                                Log(syncFile.RemotePath + " and " + localPath + " are the same size.  Skipping.");
                                syncFile.DateDownloaded = DateTime.Now.ToString();
                                lock (dbLock)
                                {
                                    db.SaveChanges();
                                }
                                continue;
                            }

                            Log(syncFile.RemotePath + " and " + localPath + " are different sizes.  Replacing.");
                            File.Delete(localPath);
                        }
                        if (sftp.Exists(syncFile.RemotePath))
                        {
                            if (!Directory.Exists(localDirectory))
                            {
                                Directory.CreateDirectory(localDirectory);
                            }

                            // Download File
                            var success = false;

                            Log("Downloading " + syncFile.RemotePath + " -->\r\n     " + localPath);
                            try
                            {
                                var stopWatch = new Stopwatch();
                                stopWatch.Restart();

                                if (config.lftp == 0)
                                {
                                    // Sync SFTP
                                    using var fileStream = File.OpenWrite(localPath);
                                    sftp.DownloadFile(syncFile.RemotePath, fileStream);
                                    stopWatch.Stop();
                                    var elapsed = stopWatch.Elapsed;

                                    Log($"Downloaded Successfully at {syncFile.Length / 1024 / elapsed.TotalSeconds:n0} KB/sec");
                                    success = true;
                                }
                                else
                                {
                                    // You may need to ssh manually once to save the host key
                                    var args = string.Concat("-u ", config.username, ",", config.password, " -e \"pget -c -n ", config.lftp, " '", syncFile.RemotePath.Replace("'","\\'"), "' -o '", localPath.Replace("'","\\'"), "'; exit\" sftp://", config.hostname, ":", config.port);
                                    var processStartInfo = new ProcessStartInfo
                                    {
                                        FileName = "lftp",
                                        RedirectStandardInput = false,
                                        RedirectStandardOutput = false,
                                        Arguments = args,
                                        UseShellExecute = true,
                                        CreateNoWindow = true
                                    };

                                    using var process = Process.Start(processStartInfo);
                                    if (process != null && process.WaitForExit(43200000)) // Max wait 12 hours
                                    {
                                        stopWatch.Stop();
                                        var elapsed = stopWatch.Elapsed;

                                        Log($"Downloaded Successfully at {syncFile.Length / 1024 / elapsed.TotalSeconds:n0} KB/sec");
                                        success = true;
                                    }
                                    else
                                    {
                                        process?.Kill(true);
                                        success = false;
                                        Log("lftp download FAILED.  Process hung for > 12 hours or failed to start");
                                    }
                                }
                            }
                            catch (Exception exception)
                            {
                                Log("!!ERROR!! while downloading " + syncFile.RemotePath + " - " + exception);
                            }

                            // Check for Rars, MKVs, and MP4s
                            if (success)
                            {
                                syncFile.DateDownloaded = DateTime.Now.ToString();
                                lock (dbLock)
                                {
                                    db.SaveChanges();
                                }

                                if (localPath.EndsWith(".part1.rar", StringComparison.Ordinal) || (!localPath.Contains(".part") && localPath.EndsWith(".rar", StringComparison.Ordinal)))
                                {
                                    rars.Add(localPath);
                                    Log("Added " + localPath + " to auto-unrar queue");
                                }
                                else if (localPath.EndsWith(".mkv", StringComparison.Ordinal) || localPath.EndsWith(".mp4", StringComparison.Ordinal))
                                {
                                    mkvs.Add(localPath);
                                }
                            }
                        }
                        else
                        {
                            Log(syncFile.RemotePath + " no longer exists");
                            syncFile.DateDownloaded = DateTime.Now.ToString();
                            lock (dbLock)
                            {
                                db.SaveChanges();
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        Log("!!ERROR!! while downloading and scanning " + syncFile.RemotePath + " - " + exception);
                    }
                }
                lock (dbLock)
                {
                    db.SaveChanges();
                }

                sftp.Disconnect();
            }

            Log($"Downloading complete.  Processing {rars.Count} rars and {mkvs.Count} mkvs / mp4s ...");
            // Unrar
            foreach (var rar in rars)
            {
                try
                {
                    var unrarFolder = rar[..(rar.LastIndexOf(Path.DirectorySeparatorChar) + 1)] + "_unrar";
                    if (!Directory.Exists(unrarFolder))
                    {
                        Directory.CreateDirectory(unrarFolder);
                    }
                    Log("Unraring " + rar);
                    using var process = Process.Start(new ProcessStartInfo(config.unrar) { Arguments = "x -o- \"" + rar + "\" \"" + unrarFolder + "\"" }); // x = extract, -o- = Don't overwrite or prompt to overwrite
                    if (process == null)
                    {
                        continue;
                    }
                    process.WaitForExit();
                    Log("Unrared " + rar);
                    Thread.Sleep(2000); // Wait for unrar to completely clean up
                    mkvs.AddRange(Directory.GetFiles(unrarFolder, "*.mkv"));
                }
                catch (Exception exception)
                {
                    Log("!!ERROR!! during Unrar of " + rar + " - " + exception);
                }
            }

            // MKV move & rename
            await MoveFiles(mkvs);

            Log("All jobs complete.  Closing in 60 seconds...");
            Thread.Sleep(60000);
        }
        catch (Exception exception)
        {
            Log("!!ERROR!! Unexpected top level error - " + exception);
        }
    }

    /// <summary>
    /// Move a given list of mkvs
    /// </summary>
    /// <param name="mkvs">A list of mkv paths to move</param>
    public async Task MoveFiles(List<string> mkvs, bool CopyInsteadOfMove = false)
    {
        foreach (var mkv in mkvs.Where(mkv => !mkv.Contains("Sample")))
        {
            try
            {
                // Determine if TV or Movie
                if (Rename.IsTV(mkv))
                {
                    var filename = await Rename.TV(mkv, config.tmdbKey);
                    var filePath = Path.Combine(config.tvDir, filename);
                    if (CopyInsteadOfMove)
                    {
                        Log("Copying TV " + mkv + " -->\r\n     " + filePath);
                    }
                    else
                    {
                        Log("Moving TV " + mkv + " -->\r\n     " + filePath);
                    }

                    if (filename.Contains(Path.DirectorySeparatorChar))
                    {
                        Directory.CreateDirectory(Path.Combine(config.tvDir, filename[..filename.LastIndexOf(Path.DirectorySeparatorChar)]));
                    }

                    var shouldMove = true;
                    if (File.Exists(filePath))
                    {
                        var newFile = new FileInfo(mkv);
                        var existingFile = new FileInfo(filePath);
                        if (newFile.Length == existingFile.Length)
                        {
                            if (CopyInsteadOfMove)
                            {
                                Log("Existing file with same name and file size found.  Ignoring...");
                            }
                            else
                            {
                                Log("Existing file with same name and file size found.  Deleting source file...");
                                File.Delete(mkv);
                            }
                            shouldMove = false;
                        }
                        else
                        {
                            Log("Existing file with same name but different size found.  Deleting destination file...");
                            File.Delete(filePath);
                        }
                    }
                    if (shouldMove)
                    {
                        if (CopyInsteadOfMove)
                        {
                            File.Copy(mkv, filePath);
                            Log("Copied Successfully");
                        }
                        else
                        {
                            File.Move(mkv, filePath);
                            Log("Moved Successfully");
                        }
                    }
                }
                else
                {
                    var filename = await Rename.Movie(mkv, config.tmdbKey);
                    var filePath = Path.Combine(config.movieDir, filename);
                    if (CopyInsteadOfMove)
                    {
                        Log("Copying Movie " + mkv + " -->\r\n     " + filePath);
                    }
                    else
                    {
                        Log("Moving Movie " + mkv + " -->\r\n     " + filePath);
                    }

                    if (filename.Contains(Path.DirectorySeparatorChar))
                    {
                        Directory.CreateDirectory(Path.Combine(config.movieDir, filename[..filename.LastIndexOf(Path.DirectorySeparatorChar)]));
                    }
                    var shouldMove = true;
                    if (File.Exists(filePath))
                    {
                        var newFile = new FileInfo(mkv);
                        var existingFile = new FileInfo(filePath);
                        if (newFile.Length == existingFile.Length)
                        {
                            if (CopyInsteadOfMove)
                            {
                                Log("Existing file with same name and file size found.  Ignoring...");
                            }
                            else
                            {
                                Log("Existing file with same name and file size found.  Deleting source file......");
                                File.Delete(mkv);
                            }
                            shouldMove = false;
                        }
                        else
                        {
                            Log("Existing file with same name but different size found.  Deleting destination file...");
                            File.Delete(filePath);
                        }
                    }
                    if (shouldMove)
                    {
                        if (CopyInsteadOfMove)
                        {
                            File.Copy(mkv, filePath);
                            Log("Copied Successfully");
                        }
                        else
                        {
                            File.Move(mkv, filePath);
                            Log("Moved Successfully");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("!!ERROR!! during move of " + mkv + " - " + ex);
            }
        }
    }

    /// <summary>
    /// Scan entire directory structure of remote SFTP server
    /// </summary>
    /// <param name="sftp">SftpClient</param>
    /// <param name="basePath">Root of the remote file system</param>
    /// <param name="subDirectory">Subdirectory to look for files and further subdirectories in</param>
    /// <returns>Number of files found</returns>
    private int ListFilesRecursive(SftpClient sftp, string basePath, string subDirectory)
    {
        Status("Checking " + basePath + subDirectory);
        try
        {
            var foundFiles = 0;
            var remoteDirectoryInfo = sftp.ListDirectory(basePath + subDirectory);
            foreach (SftpFile sftpFile in remoteDirectoryInfo.OrderBy(f => f.Name))
            {
                var filePath = subDirectory + "/" + sftpFile.Name;
                if (sftpFile.IsDirectory)
                {
                    if (!sftpFile.Name.StartsWith(".", StringComparison.Ordinal))
                    {
                        foundFiles += ListFilesRecursive(sftp, basePath, filePath);
                    }
                }
                else
                {
                    var file = db.SyncFile.FirstOrDefault(f => f.RemotePath == basePath + filePath);
                    if (file == null)
                    {
                        Log("Found New file: " + basePath + filePath);
                        lock (dbLock)
                        {
                            db.SyncFile.Add(new SyncFile
                            {
                                DateDiscovered = DateTime.Now.ToString(),
                                DateDownloaded = null,
                                Length = sftpFile.Length,
                                RemoteDateModified = sftpFile.LastWriteTime.ToString(),
                                RemotePath = basePath + filePath
                            });
                            db.SaveChanges();
                        }

                        foundFiles++;
                    }
                    else if (file.Length != sftpFile.Length || Convert.ToDateTime(file.RemoteDateModified) != sftpFile.LastWriteTime)
                    {
                        Log("Found Modified file: " + basePath + filePath);
                        file.DateDownloaded = null;
                        file.Length = sftpFile.Length;
                        file.RemoteDateModified = sftpFile.LastWriteTime.ToString();
                        lock (dbLock)
                        {
                            db.SaveChanges();
                        }
                        foundFiles++;
                    }
                }
            }
            return foundFiles;
        }
        catch (Exception ex)
        {
            Log("!!ERROR!! While scanning for files " + ex);
            return 0;
        }
    }

    /// <summary>
    /// Log text to file and console window
    /// </summary>
    /// <param name="logText">Text to display</param>
    public void Log(string logText)
    {
        if (logText.Length > 127)
        {
            Console.Title = logText[..127];
        }
        else
        {
            Console.Title = logText;
        }
        Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + logText);
        var logBytes = new UTF8Encoding(true).GetBytes(DateTime.Now.ToString("HH:mm:ss") + " " + logText + "\r\n");
        lock (logLock)
        {
            using FileStream log = new(logPath, FileMode.Append, FileAccess.Write);
            log.Write(logBytes, 0, logBytes.Length);
        }
    }

    /// <summary>
    /// Show text on window title only, do not log to file
    /// </summary>
    /// <param name="statusText">Text to display</param>
    public static void Status(string statusText)
    {
        Console.Title = statusText;
    }
}
