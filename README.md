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


Sample execute.bat file to use with Deluge execute plugin (assuming you run from C:\Users\YourUserNameHere\Downloads\SimpleSFTPSyncCore and you download to a folder with "Movies" in the path):

    @echo off
    set torrentid=%1
    set torrentname=%~2
    set torrentpath=%~3
    C:
    cd C:\Users\YourUserNameHere\Downloads\SimpleSFTPSyncCore
    
    @echo Testing if path contains "Movies"
    if x%torrentpath:Movies=%==x%torrentpath% GOTO end
    
    @echo Looks like a movie, executing...
    dotnet C:\Users\YourUserNameHere\Downloads\SimpleSFTPSyncCore\SimpleSFTPSyncCore.dll copy "%torrentpath%\%torrentname%" 
    
    :end
    endlocal
    timeout 10 > nul
