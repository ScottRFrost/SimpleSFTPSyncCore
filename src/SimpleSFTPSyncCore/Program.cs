using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Collections.Generic;
// To update DB Context: Scaffold-DbContext "Filename={full path here}\SimpleSFTPSyncCore.sqlite" Microsoft.EntityFrameworkCore.Sqlite -Force
// Simple DB GUI at http://sqlitebrowser.org/

namespace SimpleSFTPSyncCore
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Start Main Loop
            if (args.Count() == 0)
            {
                var simpleSFTPSync = new SimpleSFTPSync();
                simpleSFTPSync.StartRun();
            }

            else if (args[0] == "?" || args[0] == "-?" || args[0] == "-h" || args[0] == "help")
            {
                Console.WriteLine("Usage: dotnet SimpleSFTPSync.dll {options}");
                Console.WriteLine("No options - Begin main sync");
                Console.WriteLine("move {path name} - Moves *.mkvs in the given path");
                Console.WriteLine("copy {path name} - Copies *.mkvs in the given path");
                Console.WriteLine("movie {path name} - Test renaming for a given movie path");
                Console.WriteLine("sql {sql command text} - Execute the command text against SimpleSFTPSync's sqlite database");
                Console.WriteLine("tv {path name} - Test renaming for a given tv path");
            }

            // Move a folder full of TV / movies
            else if (args[0] == "move")
            {
                var path = string.Join(" ", args).Substring(5);
                Console.WriteLine("Moving for path: " + path);
                var mkvs = new List<string>();
                mkvs.AddRange(Directory.GetFiles(path, "*.mkv", SearchOption.AllDirectories));
                ////mkvs.AddRange(Directory.GetFiles(path, "*.m2ts"));
                ////mkvs.AddRange(Directory.GetFiles(path, "*.mp4"));
                ////mkvs.AddRange(Directory.GetFiles(path, "*.avi"));
                ////mkvs.AddRange(Directory.GetFiles(path, "*.m4v"));
                Console.WriteLine("Found: " + mkvs.Count);
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
                Console.WriteLine("Copying for path: " + path);
                var mkvs = new List<string>();
                mkvs.AddRange(Directory.GetFiles(path, "*.mkv", SearchOption.AllDirectories));
                ////mkvs.AddRange(Directory.GetFiles(path, "*.m2ts"));
                ////mkvs.AddRange(Directory.GetFiles(path, "*.mp4"));
                ////mkvs.AddRange(Directory.GetFiles(path, "*.avi"));
                ////mkvs.AddRange(Directory.GetFiles(path, "*.m4v"));
                Console.WriteLine("Found: " + mkvs.Count);
                if (mkvs.Count > 0)
                {
                    var simpleSFTPSync = new SimpleSFTPSync();
                    simpleSFTPSync.MoveFiles(mkvs, true);
                }
            }

            // Test parse Movie Name
            else if (args[0] == "movie")
            {
                var path = string.Join(" ", args).Substring(6);
                Console.WriteLine(Rename.Movie(path));
            }

            // Direct SQL command
            else if (args[0] == "sql")
            {
                var db = new SimpleSFTPSyncCoreContext();
                var command = string.Join(" ", args).Substring(4);
                Console.WriteLine(db.Database.ExecuteSqlCommand(command) + " rows affected");
            }

            // Test parse TV 
            else if (args[0] == "tv")
            {
                var path = string.Join(" ", args).Substring(3);
                Console.WriteLine(Rename.TV(path));
            }
        }
    }
}
