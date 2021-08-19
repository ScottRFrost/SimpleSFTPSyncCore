using System;
using System.Net; // For URL Encode
using System.IO; // For Path
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace SimpleSFTPSyncCore
{
    internal static class Rename
    {
        public enum TmdbGenres
        {
            Adventure = 12,
            Fantasy = 14,
            Animation = 16,
            Drama = 18,
            Horror = 27,
            Action = 28,
            Comedy = 35,
            History = 36,
            Western = 37,
            Thriller = 53,
            Crime = 80,
            Documentary = 99,
            Science_Fiction = 878,
            Mystery = 9648,
            Music = 10402,
            Romance = 10749,
            Family = 10751,
            War = 10752,
            TV_Movie = 10770
        }

        /// <summary>
        /// Shared cleaning code
        /// </summary>
        /// <param name="filename">Full file path</param>
        /// <returns>Cleaned Version</returns>
        private static string Clean(this string filename)
        {
            // Determine if we want to try to operate on the filename itself or the parent folder
            var chunks = filename.Split(Path.DirectorySeparatorChar);
            if (chunks.Length > 1)
            {
                var parentFolder = chunks[^2];
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
                        filename = chunks[^1].ToLowerInvariant();
                    }
                    else
                    {
                        // Probably a movie and the parent folder name looks descriptive, use parent folder
                        filename = chunks[^2].ToLowerInvariant() + ".mkv";
                    }
                }
            }
            else
            {
                // Use filename
                filename = chunks[^1].ToLowerInvariant();
            }

            // Strip things we know we don't want
            return filename
                .Replace("1080p", string.Empty).Replace("720p", string.Empty).Replace("x264", string.Empty).Replace("h264", string.Empty).Replace("ac3", string.Empty).Replace("dts", string.Empty)
                .Replace("blurayrip", string.Empty).Replace("bluray", string.Empty).Replace("dvdrip", string.Empty).Replace("HDTV", string.Empty).Replace("Webrip", string.Empty)
                .Replace(".", " ").Replace("  ", " ").Replace("  ", " ")
                .ToTitleCase().Replace(" Mkv", ".mkv");
        }

        /// <summary>
        /// Shared cleaning code
        /// </summary>
        /// <param name="filename">Full file path</param>
        /// <returns>Cleaned with Windows illegal characters removed</returns>
        private static string CleanFilePath(this string filename)
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
            for (var season = 1; season < 64; season++)
            {
                for (var episode = 0; episode < 64; episode++)
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
        public static string Movie(string filename, string tmdbKey)
        {
            filename = filename.Clean();
            var culture = CultureInfo.CurrentCulture;
            var textInfo = culture.TextInfo;

            // Usually 'Movie Name yyyy' followed by garbage
            for (var year = 1960; year < 2030; year++)
            {
                // Find Year
                var idx = filename.IndexOf(year.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
                if (idx <= 0)
                {
                    continue;
                }

                // Attempt tmdbAPI Check
                var title = filename.Substring(0, idx - 1).Trim(); // Strip garbage after year
                if (title.EndsWith("-"))
                {
                    title = title[0..^1].Trim();
                }
                title = title[(title.LastIndexOf(Path.DirectorySeparatorChar) + 1)..]; // Strip Folder
                var httpClient = new ProHttpClient();
                dynamic tmdb;
                try
                {
                    tmdb = JObject.Parse(httpClient.DownloadString("https://api.themoviedb.org/3/search/movie?query=" + WebUtility.UrlEncode(title) + "&language=en-US&year=" + year.ToString(CultureInfo.InvariantCulture) + "&include_adult=false&api_key=" + tmdbKey).Result);
                    if (tmdb.total_results == null || (int)(tmdb.total_results) == 0)
                    {
                        // Didn't find it, return a best guess
                        return title.ToLowerInvariant().ToTitleCase() + " (" + year.ToString(CultureInfo.InvariantCulture) + ").mkv";
                    }

                    // Found it, put it in the correct folder
                    var genres = string.Empty;

                    // Look up genre IDs to names
                    if (tmdb.results[0].genre_ids != null)
                    {
                        foreach(int genreID in tmdb.results[0].genre_ids)
                        {
                            genres += Enum.GetName(typeof(TmdbGenres), genreID) + ",";
                        }
                    }

                    var genre = "Action";
                    if (genres.Contains("Romance")) { genre = "Chick Flick"; }
                    else if (genres.Contains("Animation")) { genre = "Animation"; }
                    else if (genres.Contains("Horror")) { genre = "Horror"; }
                    else if (genres.Contains("Family")) { genre = "Family"; }
                    else if (genres.Contains("Science_Fiction")) { genre = "Science Fiction"; }
                    else if (genres.Contains("Fantasy")) { genre = "Science Fiction"; }
                    else if (genres.Contains("Comedy")) { genre = "Comedy"; }
                    else if (genres.Contains("Documentary")) { genre = "Documentary"; }
                    else if (genres.Contains("History")) { genre = "Documentary"; }
                    else if (genres.Contains("Drama")) { genre = "Drama"; }
                    else if (genres.Contains("Adventure")) { genre = "Adventure"; }
                    return genre + Path.DirectorySeparatorChar + ((string)tmdb.results[0].title + " (" + year.ToString(CultureInfo.InvariantCulture) + ").mkv").CleanFilePath();
                }
                catch (Exception ex)
                {
                    Console.Write("Exception during rename: " + ex);
                    // Download died, return a best guess
                    return title.ToLowerInvariant().ToTitleCase() + " (" + year.ToString(CultureInfo.InvariantCulture) + ").mkv";
                }
            }

            // Year not found, punt
            return filename[(filename.LastIndexOf(Path.DirectorySeparatorChar) + 1)..].CleanFilePath();
        }

        /// <summary>
        /// Rename TV shows
        /// </summary>
        /// <param name="filename">Full file path</param>
        /// <returns>Renamed Version</returns>
        public static string TV(string filename, string tmdbKey)
        {
            filename = filename.Clean();
            var culture = CultureInfo.CurrentCulture;
            var textInfo = culture.TextInfo;

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
                        // Attempt tmdbAPI Check
                        var title = filename.Substring(0, idx - 1).Trim(); // Strip S01E01 and trailing garbage
                        if (title.EndsWith("-"))
                        {
                            title = title[0..^1].Trim();
                        }
                        title = title[(title.LastIndexOf(Path.DirectorySeparatorChar) + 1)..].CleanFilePath().ToLowerInvariant().ToTitleCase(); // Strip Folder, junk, and set to Title Case
                        var httpClient = new ProHttpClient();
                        dynamic tmdb;
                        try
                        {
                            tmdb = JObject.Parse(httpClient.DownloadString("https://api.themoviedb.org/3/search/tv?query=" + WebUtility.UrlEncode(title) + "&language=en-US&api_key=" + tmdbKey).Result);
                            if (tmdb.total_results == null || (int)(tmdb.total_results) == 0)
                            {
                                // Didn't find it, return a best guess
                                return title + Path.DirectorySeparatorChar + "Season " + season.ToString(CultureInfo.InvariantCulture) + Path.DirectorySeparatorChar + title + " - " + episodeNumber.ToUpperInvariant() + ".mkv";
                            }

                            // Found it, use the corrected title
                            title = (string)tmdb.results[0].name;
                            title = title.CleanFilePath();
                            return title + Path.DirectorySeparatorChar + "Season " + season.ToString(CultureInfo.InvariantCulture) + Path.DirectorySeparatorChar + title + " - " + episodeNumber.ToUpperInvariant() + ".mkv";
                        }
                        catch(Exception ex)
                        {
                            Console.Write("Exception during rename: " + ex);
                            // Download died, return a best guess
                            return title + Path.DirectorySeparatorChar + "Season " + season.ToString(CultureInfo.InvariantCulture) + Path.DirectorySeparatorChar + title + " - " + episodeNumber.ToUpperInvariant() + ".mkv";
                        }
                    }
                }
            }

            // Season / episode not found, punt
            return filename[(filename.LastIndexOf(Path.DirectorySeparatorChar) + 1)..].CleanFilePath();
        }
    }
}
