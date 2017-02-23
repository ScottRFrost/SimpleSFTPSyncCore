# SimpleSFTPSyncCore
Download files ONCE from a remote SFTP, even if you move or delete the files from the download location

This is the new .NET Core version of [SimpleSFTPSync](https://github.com/ScottRFrost/SimpleSFTPSync), which now uses SQLite instead of MS SQL Compact.

It also has command line options to move or copy .mkv files (with genre and smart renaming).

    Usage: dotnet SimpleSFTPSync.dll {options}
    No options - Begin main sync
    move {path name} - Moves *.mkvs in the given path
    copy {path name} - Copies *.mkvs in the given path
    movie {path name} - Test renaming for a given movie path
    sql {sql command text} - Execute the command text against SimpleSFTPSync's sqlite database
    tv {path name} - Test renaming for a given tv path


As always, input is appreciated!
