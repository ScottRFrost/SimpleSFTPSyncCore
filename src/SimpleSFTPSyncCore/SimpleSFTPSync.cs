// SimpleSFTPSync by ScottRFrost - https://github.com/ScottRFrost/SimpleSFTPSync
// Build with: dotnet compile
// Publish with: dotnet publish -r win10-x64 / dotnet publish -r ubuntu.16.10-x64 etc

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;
using System.Diagnostics; // For process and process start


namespace SimpleSFTPSyncCore
{
    public class SimpleSFTPSync
    {
        private readonly JObject config;
        private static string logPath;
        private SimpleSFTPSyncCoreContext db;
        private readonly object dbLock = new object();
        private readonly object logLock = new object();
        private readonly string hostname;
        private readonly int port;
        private readonly string username;
        private readonly string password;
        private readonly string fingerprint;
        private readonly string remoteDir;
        private readonly string downloadDir;
        private readonly string unrar;
        private readonly string movieDir;
        private readonly string tvDir;
        private readonly string tmdbKey;
        private List<string> rars;
        private List<string> mkvs;

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
            config = JObject.Parse(fileText);
            hostname = config["hostname"].Value<string>();
            port = config["port"].Value<int>();
            username = config["username"].Value<string>();
            password = config["password"].Value<string>();
            fingerprint = config["fingerprint"].Value<string>();
            remoteDir = config["remoteDir"].Value<string>();
            downloadDir = config["downloadDir"].Value<string>();
            unrar = config["unrar"].Value<string>();
            movieDir = config["movieDir"].Value<string>();
            tvDir = config["tvDir"].Value<string>();
            tmdbKey = config["tmdbKey"].Value<string>();
            rars = new List<string>();
            mkvs = new List<string>();
            Log("Configuration Read");
        }

        /// <summary>
        /// Starts a new SFTP connect and download run
        /// </summary>
        public void StartRun()
        {
            try
            {
                // Make SFTP connection and do work
                using (var sftp = new SftpClient(hostname, port, username, password))
                {
                    // Connect and scan
                    sftp.Connect();
                    Log("Connected to " + hostname + " running " + sftp.ConnectionInfo.ServerVersion);
                    Log("Checking for new files to download...");
                    var foundFiles = ListFilesRecursive(sftp, remoteDir, string.Empty).ToString();
                    Log(foundFiles + " files found for download");


                    // Download and queue up unrar / rename.  For some reason SCP is much faster for downloads.
                    foreach (var syncFile in db.SyncFile.Where(f => f.DateDownloaded == null).OrderBy(f => f.DateDiscovered))
                    {
                        try
                        {
                            var localPath = downloadDir + (syncFile.RemotePath.Replace('/', Path.DirectorySeparatorChar).Replace(Path.DirectorySeparatorChar.ToString() + Path.DirectorySeparatorChar.ToString(), Path.DirectorySeparatorChar.ToString()));
                            var localDirectory = localPath.Substring(0, localPath.LastIndexOf(Path.DirectorySeparatorChar));
                            if (File.Exists(localPath))
                            {
                                var localFile = new FileInfo(localPath);
                                if (localFile.Length == syncFile.Length)
                                {
                                    Log(syncFile.RemotePath + " and " + localPath + " are the same size.  Skipping.");
                                    syncFile.DateDownloaded = DateTime.Now.ToString();
                                    lock(dbLock) {
                                        db.SaveChanges();
                                    }
                                    continue;
                                }

                                Log(syncFile.RemotePath + " and " + localPath + " are different sizes.  Replacing.");
                                File.Delete(localPath);
                            }
                            if (sftp.Exists(remoteDir + syncFile.RemotePath))
                            {
                                if (!Directory.Exists(localDirectory))
                                {
                                    Directory.CreateDirectory(localDirectory);
                                }

                                // Download File
                                var success = false;

                                Log("Downloading " + remoteDir + syncFile.RemotePath + " -->\r\n     " + localPath);
                                try
                                {
                                    var startTime = DateTime.Now;

                                // ASync SFTP
                                //using (var fileStream = File.OpenWrite(localPath))
                                //{
                                //var download = (SftpDownloadAsyncResult)sftp.BeginDownloadFile(syncFile.RemotePath, fileStream);
                                //ulong lastDownloadedBytes = 0;
                                //while (!download.IsCompleted)
                                //{
                                //    Status(string.Format("Downloaded {0:n0} / {1:n0} KB @ {2:n0} KB/sec", download.DownloadedBytes / 1024, syncFile.Length / 1024, (download.DownloadedBytes - lastDownloadedBytes) / 1024));
                                //    lastDownloadedBytes = download.DownloadedBytes;
                                //    Thread.Sleep(1000);
                                //}
                                //}

                                // Sync SFTP
                                // sftp.DownloadFile(remoteDir + syncFile.RemotePath, fileStream);
                                using (var scp = new ScpClient(hostname, port, username, password))
                                {
                                    scp.Connect();
                                    scp.Downloading += Scp_Downloading;
                                    scp.Download(remoteDir + syncFile.RemotePath, new DirectoryInfo(localDirectory));
                                    scp.Disconnect();
                                }

                                var endTime = DateTime.Now;

                                var timespan = TimeSpan.FromSeconds((endTime - startTime).TotalSeconds);
                                Log(string.Format("Downloaded Successfully at {0:n0} KB/sec", (syncFile.Length / 1024) / timespan.TotalSeconds));
                                success = true;
                                }
                                catch (Exception exception)
                                {
                                    Log("!!ERROR!! while downloading " + remoteDir + syncFile.RemotePath + " - " + exception);
                                }

                                // Check for Rars or MKVs
                                if (success)
                                {

                                    syncFile.DateDownloaded = DateTime.Now.ToString();
                                    lock(dbLock)
                                    {
                                        db.SaveChanges();
                                    }

                                    if (localPath.EndsWith(".part1.rar", StringComparison.Ordinal) || !localPath.Contains(".part") && localPath.EndsWith(".rar", StringComparison.Ordinal))
                                    {
                                        rars.Add(localPath);
                                        Log("Added " + localPath + " to auto-unrar queue");
                                    }
                                    else if (localPath.EndsWith(".mkv", StringComparison.Ordinal))
                                    {
                                        mkvs.Add(localPath);
                                    }
                                }
                            }
                            else
                            {
                                Log(syncFile.RemotePath + " no longer exists");
                                syncFile.DateDownloaded = DateTime.Now.ToString();
                                lock(dbLock)
                                {
                                    db.SaveChanges();
                                }
                            }
                        }
                        catch (Exception exception)
                        {
                            Log("!!ERROR!! while downloading and scanning " + remoteDir + syncFile.RemotePath + " - " + exception);
                        }
                    }
                    lock(dbLock)
                    {
                        db.SaveChanges();
                    }
                    
                    sftp.Disconnect();
                }


                Log("Downloading complete.  Processing " + rars.Count + " rars and " + mkvs.Count + " mkvs ...");
                // Unrar
                foreach (var rar in rars)
                {
                    try
                    {
                        var unrarFolder = rar.Substring(0, rar.LastIndexOf(Path.DirectorySeparatorChar) + 1) + "_unrar";
                        if (!Directory.Exists(unrarFolder))
                        {
                            Directory.CreateDirectory(unrarFolder);
                        }
                        Log("Unraring " + rar);
                        var process = Process.Start(new ProcessStartInfo(unrar) { Arguments = "x -o- \"" + rar + "\" \"" + unrarFolder + "\"" }); // x = extract, -o- = Don't overwrite or prompt to overwrite
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
                MoveFiles(mkvs);
                
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
        public void MoveFiles(List<string> mkvs, bool CopyInsteadOfMove = false)
        {
            foreach (var mkv in mkvs.Where(mkv => !mkv.Contains("Sample")))
            {
                try
                {
                    // Determine if TV or Movie
                    if (Rename.IsTV(mkv))
                    {
                        var filename = Rename.TV(mkv, tmdbKey);
                        var filePath = Path.Combine(tvDir, filename);
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
                            Directory.CreateDirectory(tvDir + Path.DirectorySeparatorChar + filename.Substring(0, filename.LastIndexOf(Path.DirectorySeparatorChar)));
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
                        var filename = Rename.Movie(mkv, tmdbKey);
                        var filePath = Path.Combine(movieDir, filename);
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
                            Directory.CreateDirectory(movieDir + Path.DirectorySeparatorChar + filename.Substring(0, filename.LastIndexOf(Path.DirectorySeparatorChar)));
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
        /// Status updates during SCP download
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Scp_Downloading(object sender, Renci.SshNet.Common.ScpDownloadEventArgs e)
        {
            var percentage = ((double)e.Downloaded / (double)e.Size) * 100D;
            Status(string.Format("{0} - {1:n2}%  {2:n0} / {3:n0}", e.Filename, percentage, e.Downloaded, e.Size));
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
            Status("Checking /" + subDirectory);
            var foundFiles = 0;
            var remoteDirectoryInfo = sftp.ListDirectory(basePath + subDirectory);
            try
            {
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
                        var file = db.SyncFile.FirstOrDefault(f => f.RemotePath == filePath);
                        if (file == null)
                        {
                            Log("Found New file: " + filePath);
                            lock(dbLock)
                            {
                                db.SyncFile.Add(new SyncFile
                                {
                                    DateDiscovered = DateTime.Now.ToString(),
                                    DateDownloaded = null,
                                    Length = sftpFile.Length,
                                    RemoteDateModified = sftpFile.LastWriteTime.ToString(),
                                    RemotePath = filePath
                                });
                                db.SaveChanges();
                            }
                            
                            foundFiles++;
                        }
                        else if (file.Length != sftpFile.Length || Convert.ToDateTime(file.RemoteDateModified) != sftpFile.LastWriteTime)
                        {
                            Log("Found Modified file: " + filePath);
                            file.DateDownloaded = null;
                            file.Length = sftpFile.Length;
                            file.RemoteDateModified = sftpFile.LastWriteTime.ToString();
                            lock(dbLock)
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
            Console.Title = logText;
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + logText);
            var logBytes = new UTF8Encoding(true).GetBytes(DateTime.Now.ToString("HH:mm:ss") + " " + logText + "\r\n");
            lock(logLock)
            {
                using(FileStream log = new FileStream(logPath, FileMode.Append, FileAccess.Write))
                {
                    log.Write(logBytes, 0, logBytes.Length);
                }  
            }
        }

        /// <summary>
        /// Show text on window title only, do not log to file
        /// </summary>
        /// <param name="statusText">Text to display</param>
        public void Status(string statusText)
        {
            Console.Title = statusText;
        }
    }
}
