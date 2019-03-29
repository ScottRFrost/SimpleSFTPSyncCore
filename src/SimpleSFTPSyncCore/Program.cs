using System;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Diagnostics; // For process and process start
using System.Threading; // For Thread Sleep

// To update DB Context: Scaffold-DbContext "Filename={full path here}\SimpleSFTPSyncCore.sqlite" Microsoft.EntityFrameworkCore.Sqlite -Force
// Simple DB GUI at http://sqlitebrowser.org/

namespace SimpleSFTPSyncCore
{
    public class Program
    {
        private static string logPath;
        private static readonly object logLock = new object();

        public static void Main(string[] args)
        {
            logPath = Path.Combine(Directory.GetCurrentDirectory(), DateTime.Now.ToString("MM-dd-yyyy") + ".log");

            try
            {
                // Start Main Loop
                if (args.Length == 0)
                {
                    var simpleSFTPSync = new SimpleSFTPSync();
                    simpleSFTPSync.StartRun();
                    // Console.ReadKey(); // DEBUG
                }

                else if (args[0] == "?" || args[0] == "-?" || args[0] == "-h" || args[0] == "help")
                {
                    Log("Usage: dotnet SimpleSFTPSync.dll {options}");
                    Log("No options - Begin main sync");
                    Log("move {path name} - Moves *.mkvs in the given path");
                    Log("copy {path name} - Copies *.mkvs in the given path");
                    Log("movie {path name} - Test renaming for a given movie path");
                    Log("sql {sql command text} - Execute the command text against SimpleSFTPSync's sqlite database");
                    Log("tv {path name} - Test renaming for a given tv path");
                }

                // Move a folder full of TV / movies
                else if (args[0] == "move")
                {
                    var path = string.Join(" ", args).Substring(5);
                    Log("Moving for path: " + path);
                    var mkvs = new List<string>();
                    if (path.Trim().EndsWith(".mkv"))
                    {
                        // Single File
                        mkvs.Add(path);
                    }
                    else
                    {
                        // Folder
                        mkvs.AddRange(Directory.GetFiles(path, "*.mkv", SearchOption.AllDirectories));
                    }
                    ////mkvs.AddRange(Directory.GetFiles(path, "*.m2ts"));
                    ////mkvs.AddRange(Directory.GetFiles(path, "*.mp4"));
                    ////mkvs.AddRange(Directory.GetFiles(path, "*.avi"));
                    ////mkvs.AddRange(Directory.GetFiles(path, "*.m4v"));
                    Log("Found: " + mkvs.Count);
                    if (mkvs.Count > 0)
                    {
                        var simpleSFTPSync = new SimpleSFTPSync();
                        simpleSFTPSync.MoveFiles(mkvs);
                    }
                }

                // Copy a folder full of TV / movies
                else if (args[0] == "copy")
                {

                    var path = string.Join(" ", args).Substring(5);
                    Log("Copying for path: " + path);
                    var mkvs = new List<string>();
                    var rars = new List<string>();
                    if (path.Trim().EndsWith(".mkv"))
                    {
                        // Single File
                        mkvs.Add(path);
                    }
                    else
                    {
                        // Folder
                        mkvs.AddRange(Directory.GetFiles(path, "*.mkv", SearchOption.AllDirectories));

                        // Rars
                        rars.AddRange(Directory.GetFiles(path, "*.rar", SearchOption.AllDirectories));
                    }
                    ////mkvs.AddRange(Directory.GetFiles(path, "*.m2ts"));
                    ////mkvs.AddRange(Directory.GetFiles(path, "*.mp4"));
                    ////mkvs.AddRange(Directory.GetFiles(path, "*.avi"));
                    ////mkvs.AddRange(Directory.GetFiles(path, "*.m4v"));
                    Log("Found: " + mkvs.Count + " mkvs and " + rars.Count + " rars");

                    // Unrar
                    if(rars.Count > 0)
                    {
                        foreach (var rar in rars)
                        {
                            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
                            Log("Reading Config from " + configPath);
                            var fileText = File.ReadAllText(configPath);
                            var config = JObject.Parse(fileText);
                            var unrar = config["unrar"].Value<string>();
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
                    }
                    

                    // Process MKVs
                    if (mkvs.Count > 0)
                    {
                        var simpleSFTPSync = new SimpleSFTPSync();
                        simpleSFTPSync.MoveFiles(mkvs, true);
                    }
                }

                // Test parse Movie Name
                else if (args[0] == "movie")
                {
                    // Read configuration
                    var configPath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
                    var fileText = File.ReadAllText(configPath);
                    var config = JObject.Parse(fileText);
                    var tmdbKey = config["tmdbKey"].Value<string>();

                    // Parse
                    var path = string.Join(" ", args).Substring(6);
                    Log(Rename.Movie(path, tmdbKey));
                    Console.ReadKey();
                }

                // Direct SQL command
                else if (args[0] == "sql")
                {
                    var db = new SimpleSFTPSyncCoreContext();
                    var command = string.Join(" ", args).Substring(4);
                    #pragma warning disable EF1000 // Possible SQL injection vulnerability.
                    Log(db.Database.ExecuteSqlCommand(command) + " rows affected");
                    #pragma warning restore EF1000 // Possible SQL injection vulnerability.
                }

                // Test parse TV 
                else if (args[0] == "tv")
                {
                    // Read configuration
                    var configPath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
                    var fileText = File.ReadAllText(configPath);
                    var config = JObject.Parse(fileText);
                    var tmdbKey = config["tmdbKey"].Value<string>();

                    // Parse
                    var path = string.Join(" ", args).Substring(3);
                    Log(Rename.TV(path, tmdbKey));
                    Console.ReadKey();
                }
            }
            catch(Exception ex)
            {
                Log("!!ERROR!! During command line parsing " + ex);
            }
        }

        /// <summary>
        /// Log text to file and console window
        /// </summary>
        /// <param name="logText">Text to display</param>
        public static void Log(string logText)
        {
            if (logText.Length > 127)
            {
                Console.Title = logText.Substring(0, 127);
            }
            else
            {
                Console.Title = logText;
            }
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + logText);
            var logBytes = new UTF8Encoding(true).GetBytes(DateTime.Now.ToString("HH:mm:ss") + " " + logText + "\r\n");
            lock (logLock)
            {
                using (FileStream log = new FileStream(logPath, FileMode.Append, FileAccess.Write))
                {
                    log.Write(logBytes, 0, logBytes.Length);
                }
            }
        }
    }
}
