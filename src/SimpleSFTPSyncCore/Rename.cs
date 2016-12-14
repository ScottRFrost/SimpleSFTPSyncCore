using System;
using System.Net; // For URL Encode
using System.Globalization;
using Newtonsoft.Json.Linq;


namespace SimpleSFTPSyncCore
{
    static class Rename
    {
        /// <summary>
        /// Shared cleaning code
        /// </summary>
        /// <param name="filename">Full file path</param>
        /// <returns>Cleaned Version</returns>
        static string Clean(this string filename)
        {
            // Determine if we want to try to operate on the filename itself or the parent folder
            var chunks = filename.Split('\\');
            if (chunks.Length > 1)
            {
                var parentFolder = chunks[chunks.Length - 2];
                if (parentFolder.Contains("720p") || parentFolder.Contains("1080p"))
                {
                    // Check for TV style naming
                    var found = false;
                    for (var season = 1; season < 36; season++)
                    {
                        for (var episode = 0; episode < 36; episode++)
                        {
                            var episodeNumber = "S" + (season < 10 ? "0" + season : season.ToString(CultureInfo.InvariantCulture)) + "E" + (episode < 10 ? "0" + episode : episode.ToString(CultureInfo.InvariantCulture));
                            var idx = filename.ToUpperInvariant().IndexOf(episodeNumber, StringComparison.Ordinal);
                            if (idx > 0)
                            {
                                filename = filename.Substring(0, idx) + " - " + episodeNumber.ToUpperInvariant() + ".mkv";
                                found = true;
                                break;
                            }
                        }
                        if (found)
                        {
                            break;
                        }
                    }

                    if (found)
                    {
                        // May be a whole season of TV shows, use filename
                        filename = chunks[chunks.Length - 1].ToLowerInvariant();
                    }
                    else
                    {
                        // Probably a movie and the parent folder name looks descriptive, use parent folder
                        filename = chunks[chunks.Length - 2].ToLowerInvariant() + ".mkv";
                    }
                }
            }
            else
            {
                // Use filename
                filename = chunks[chunks.Length - 1].ToLowerInvariant();
            }

            // Strip things we know we don't want
            return (
                filename
                .Replace("1080p", string.Empty).Replace("720p", string.Empty).Replace("x264", string.Empty).Replace("h264", string.Empty).Replace("ac3", string.Empty).Replace("dts", string.Empty)
                .Replace("blurayrip", string.Empty).Replace("bluray", string.Empty).Replace("dvdrip", string.Empty).Replace("HDTV", string.Empty).Replace("Webrip", string.Empty)
                .Replace(".", " ").Replace("  ", " ").Replace("  ", " ")
                .ToTitleCase().Replace(" Mkv", ".mkv")
            );
        }

        /// <summary>
        /// Shared cleaning code
        /// </summary>
        /// <param name="filename">Full file path</param>
        /// <returns>Cleaned with Windows illegal characters removed</returns>
        static string CleanFilePath(this string filename)
        {
            return filename.Replace(":", "").Replace("*", "").Replace("?", "").Replace("/", "").Replace("\"", "").Replace("<", "").Replace(">", "").Replace("|", "");
        }

        /// <summary>
        /// Returns True if filename contains S00E00 style episode number, indicating it's probably a TV show (and not a movie)
        /// </summary>
        /// <param name="filename">Full file path</param>
        /// <returns>True for TV, False for not</returns>
        public static bool IsTV(string filename)
        {
            filename = filename.Clean();

            // Check for TV style naming - Usually 'Show Name s##e##' followed by garbage
            var found = false;
            for (var season = 1; season < 36; season++)
            {
                for (var episode = 0; episode < 36; episode++)
                {
                    var episodeNumber = "S" + (season < 10 ? "0" + season : season.ToString(CultureInfo.InvariantCulture)) + "E" + (episode < 10 ? "0" + episode : episode.ToString(CultureInfo.InvariantCulture));
                    var idx = filename.ToUpperInvariant().IndexOf(episodeNumber, StringComparison.Ordinal);
                    if (idx > 0)
                    {
                        filename = filename.Substring(0, idx) + " - " + episodeNumber.ToUpperInvariant() + ".mkv";
                        found = true;
                        break;
                    }
                }
                if (found)
                {
                    break;
                }
            }
            return found;
        }

        /// <summary>
        /// Rename Movies
        /// </summary>
        /// <param name="filename">Full file path</param>
        /// <returns>Renamed Version</returns>
        public static string Movie(string filename)
        {
            filename = filename.Clean();

            // Usually 'Movie Name yyyy' followed by garbage
            for (var year = 1960; year < 2030; year++)
            {
                // Find Year
                var idx = filename.IndexOf(year.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
                if (idx <= 0)
                {
                    continue;
                }

                // Attempt OMDBAPI Check
                var title = filename.Substring(0, idx - 1).Trim(); // Strip garbage after year
                title = title.Substring(title.LastIndexOf("\\", StringComparison.Ordinal) + 1); // Strip Folder
                var httpClient = new ProHttpClient();
                dynamic omdbapi = JObject.Parse(httpClient.DownloadString("http://www.omdbapi.com/?type=movie&t=" + WebUtility.UrlEncode(title) + "&y=" + year.ToString(CultureInfo.InvariantCulture)).Result);
                if (omdbapi.Response == "False")
                {
                    // Didn't find it, return a best guess
                    return title + " (" + year.ToString(CultureInfo.InvariantCulture) + ").mkv";
                }

                // Found it, put it in the correct folder
                var genre = "Action";
                var genres = (string)omdbapi.Genre;
                if (genres.Contains("Romance")) { genre = "Chick Flick";  }
                else if (genres.Contains("Animation")) { genre = "Animation"; }
                else if (genres.Contains("Horror")) { genre = "Horror"; }
                else if (genres.Contains("Family")) { genre = "Family"; }
                else if (genres.Contains("Science Fiction")) { genre = "Science Fiction"; }
                else if (genres.Contains("Fantasy")) { genre = "Science Fiction"; }
                else if (genres.Contains("Comedy")) { genre = "Comedy"; }
                else if (genres.Contains("Documentary")) { genre = "Documentary"; }
                else if (genres.Contains("History")) { genre = "Documentary"; }
                else if (genres.Contains("Drama")) { genre = "Drama"; }
                else if (genres.Contains("Adventure")) { genre = "Adventure"; }
                return (genre +"\\" + (string)omdbapi.Title + " (" + year.ToString(CultureInfo.InvariantCulture) + ").mkv").CleanFilePath();
            }
            return filename.Substring(filename.LastIndexOf("\\", StringComparison.Ordinal) + 1).CleanFilePath();
        }

        /// <summary>
        /// Rename TV shows
        /// </summary>
        /// <param name="filename">Full file path</param>
        /// <returns>Renamed Version</returns>
        public static string TV(string filename)
        {
            filename = filename.Clean();

            // Usually 'Show Name s##e##' followed by garbage
            filename = filename.Replace("HDTV", string.Empty).Replace("Webrip", string.Empty);
            for (var season = 1; season < 36; season++)
            {
                for (var episode = 1; episode < 36; episode++)
                {
                    var episodeNumber = "S" + (season < 10 ? "0" + season : season.ToString(CultureInfo.InvariantCulture)) + "E" + (episode < 10 ? "0" + episode : episode.ToString(CultureInfo.InvariantCulture));
                    var idx = filename.ToUpperInvariant().IndexOf(episodeNumber, StringComparison.Ordinal);
                    if (idx > 0)
                    {
                        // Attempt OMDBAPI Check
                        var title = filename.Substring(0, idx - 1).Trim(); // Strip S01E01 and trailing garbage
                        title = title.Substring(title.LastIndexOf("\\", StringComparison.Ordinal) + 1); // Strip Folder
                        var httpClient = new ProHttpClient();
                        dynamic omdbapi = JObject.Parse(httpClient.DownloadString("http://www.omdbapi.com/?type=series&t=" + title).Result);
                        if (omdbapi.Response == "False")
                        {
                            // Didn't find it, return a best guess
                            return (title.CleanFilePath() + "\\Season " + season.ToString(CultureInfo.InvariantCulture) + "\\" + title.CleanFilePath() + " - " + episodeNumber.ToUpperInvariant() + ".mkv");
                        }
                        // Found it, use the corrected title
                        title = (string)omdbapi.Title;
                        return (title.CleanFilePath() + "\\Season " + season.ToString(CultureInfo.InvariantCulture) + "\\" + title.CleanFilePath() + " - " + episodeNumber.ToUpperInvariant() + ".mkv").CleanFilePath();
                    }
                }
            }
            return filename.Substring(filename.LastIndexOf("\\", StringComparison.Ordinal) + 1).CleanFilePath();
        }
    }
}
