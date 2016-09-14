using System;
using System.Linq;
using Microsoft.EntityFrameworkCore; 
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

            // Test parse TV 
            else if(args[0] == "tv")
            {
                Console.WriteLine(Rename.TV(string.Join(" ", args).Substring(3)));
            }

            // Test parse Movie Name
            else if (args[0] == "movie")
            {
                Console.WriteLine(Rename.Movie(string.Join(" ", args).Substring(6)));
            }

            // Direct SQL command
            else if (args[0] == "sql")
            {
                var db = new SimpleSFTPSyncCoreContext();

                Console.WriteLine(db.Database.ExecuteSqlCommand(string.Join(" ", args).Substring(4)));
            }
        }
    }
}
