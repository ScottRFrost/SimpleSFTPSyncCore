# SimpleSFTPSyncCore
Download files **ONCE** from a remote SFTP server, **even if you move or delete the files from the download location**

After downloading, the files are moved based on [TheMovieDB](https://api.themoviedb.org) genres (for movies) and series titles (for tv series).

This is the .NET 5 version of [SimpleSFTPSync](https://github.com/ScottRFrost/SimpleSFTPSync), which now uses SQLite instead of MS SQL Compact.

As always, input is appreciated!

# Usage
Edit the included config.json and then just run the binary

In Windows
    Just execute SimpleSFTPSyncCore.exe without any parameters
    
In Linux:
    chmod +x SimpleSFTPSyncCore
    ./SimpleSFTPSyncCore

# Command Line move / copy
It also has command line options to move or copy existing .mkv files (with genre and smart renaming):

    Usage: dotnet SimpleSFTPSync.dll {options}
    No options - Begin main sync
    move {path name} - Moves *.mkvs and *.mp4s in the given path
    copy {path name} - Copies *.mkvs and *.mp4s in the given path
    movie {path name} - Test renaming for a given movie path
    sql {sql command text} - Execute the command text against SimpleSFTPSync's sqlite database
    tv {path name} - Test renaming for a given tv path

# Automation / Execute after download in Deluge
Sample execute.bat file to use with [Deluge execute plugin](https://dev.deluge-torrent.org/wiki/Plugins/Execute) (assuming you run from C:\Users\YourUserNameHere\Downloads\SimpleSFTPSyncCore and you download to a folder with "Movies" in the path):

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
