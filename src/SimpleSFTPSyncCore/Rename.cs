using System.Net; // For URL Encode
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SimpleSFTPSyncCore;

internal static partial class Rename
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

	[GeneratedRegex(@"S(?<season>\d{1,2})E(?<episode>\d{1,2})", RegexOptions.IgnoreCase)]
	private static partial Regex TvEpisodeRegex();

	[GeneratedRegex(@"\b(?<year>(18|19|20)\d{2})\b")]
	private static partial Regex MovieYearRegex();

	private static readonly JsonSerializerOptions _jsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
	};

	private class TmdbMovieResult
	{
		public string Title { get; set; } = string.Empty;
		public int[]? GenreIds { get; set; }
	}

	private class TmdbMovieResponse
	{
		public int? TotalResults { get; set; }
		public List<TmdbMovieResult>? Results { get; set; }
	}

	private class TmdbTvResult
	{
		public string Name { get; set; } = string.Empty;
	}

	private class TmdbTvResponse
	{
		public int? TotalResults { get; set; }
		public List<TmdbTvResult>? Results { get; set; }
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
				var match = TvEpisodeRegex().Match(filename);
				var found = match.Success && match.Index > 0;

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
		var match = TvEpisodeRegex().Match(filename);
		return match.Success && match.Index > 0;
	}

	/// <summary>
	/// Rename Movies
	/// </summary>
	/// <param name="filename">Full file path</param>
	/// <returns>Renamed Version</returns>
	public static async Task<string> Movie(string filename, string tmdbKey)
	{
		filename = filename.Clean();

		// Usually 'Movie Name yyyy' followed by garbage
		var matches = MovieYearRegex().Matches(filename);
		foreach (Match match in matches)
		{
			var idx = match.Index;
			if (idx <= 0)
			{
				continue;
			}

			var year = match.Value;
			// Attempt tmdbAPI Check
			var title = filename[..idx].Trim(); // Strip garbage after year
			if (title.EndsWith('-'))
			{
				title = title[..^1].Trim();
			}
			title = title[(title.LastIndexOf(Path.DirectorySeparatorChar) + 1)..]; // Strip Folder
			var httpClient = new ProHttpClient();
			try
			{
				var responseText = await httpClient.DownloadString("https://api.themoviedb.org/3/search/movie?query=" + WebUtility.UrlEncode(title) + "&language=en-US&year=" + year + "&include_adult=false&api_key=" + tmdbKey);
				var tmdb = JsonSerializer.Deserialize<TmdbMovieResponse>(responseText, _jsonOptions);
				if (tmdb == null || tmdb.TotalResults == null || tmdb.TotalResults == 0 || tmdb.Results == null || tmdb.Results.Count == 0)
				{
					// Didn't find it, return a best guess
					return title.ToLowerInvariant().ToTitleCase() + " (" + year + ").mkv";
				}

				// Found it, put it in the correct folder
				var genres = string.Empty;

				// Look up genre IDs to names
				var firstResult = tmdb.Results[0];
				if (firstResult.GenreIds != null)
				{
					foreach (int genreID in firstResult.GenreIds)
					{
						genres += Enum.GetName((TmdbGenres)genreID) + ",";
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
				return genre + Path.DirectorySeparatorChar + (tmdb.Results[0].Title + " (" + year + ").mkv").CleanFilePath();
			}
			catch (Exception ex)
			{
				Console.Write("Exception during rename: " + ex);
				// Download died, return a best guess
				return title.ToLowerInvariant().ToTitleCase() + " (" + year + ").mkv";
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
	public static async Task<string> TV(string filename, string tmdbKey)
	{
		filename = filename.Clean();

		// Usually 'Show Name s##e##' followed by garbage
		filename = filename.Replace("HDTV", string.Empty).Replace("Webrip", string.Empty);
		var match = TvEpisodeRegex().Match(filename);
		if (match.Success && match.Index > 0)
		{
			var seasonNum = int.Parse(match.Groups["season"].Value);
			var episodeNum = int.Parse(match.Groups["episode"].Value);
			var episodeNumber = $"S{seasonNum:D2}E{episodeNum:D2}";

			var idx = match.Index;
			// Attempt tmdbAPI Check
			var title = filename[..idx].Trim(); // Strip S01E01 and trailing garbage
			if (title.EndsWith('-'))
			{
				title = title[..^1].Trim();
			}
			title = title[(title.LastIndexOf(Path.DirectorySeparatorChar) + 1)..].CleanFilePath().ToLowerInvariant().ToTitleCase(); // Strip Folder, junk, and set to Title Case
			var httpClient = new ProHttpClient();
			try
			{
				var responseText = await httpClient.DownloadString("https://api.themoviedb.org/3/search/tv?query=" + WebUtility.UrlEncode(title) + "&language=en-US&api_key=" + tmdbKey);
				var tmdb = JsonSerializer.Deserialize<TmdbTvResponse>(responseText, _jsonOptions);
				if (tmdb == null || tmdb.TotalResults == null || tmdb.TotalResults == 0 || tmdb.Results == null || tmdb.Results.Count == 0)
				{
					// Didn't find it, return a best guess
					return title + Path.DirectorySeparatorChar + "Season " + seasonNum.ToString(CultureInfo.InvariantCulture) + Path.DirectorySeparatorChar + title + " - " + episodeNumber + ".mkv";
				}

				// Found it, use the corrected title
				title = tmdb.Results[0].Name.CleanFilePath();
				return title + Path.DirectorySeparatorChar + "Season " + seasonNum.ToString(CultureInfo.InvariantCulture) + Path.DirectorySeparatorChar + title + " - " + episodeNumber + ".mkv";
			}
			catch (Exception ex)
			{
				Console.Write("Exception during rename: " + ex);
				// Download died, return a best guess
				return title + Path.DirectorySeparatorChar + "Season " + seasonNum.ToString(CultureInfo.InvariantCulture) + Path.DirectorySeparatorChar + title + " - " + episodeNumber + ".mkv";
			}
		}

		// Season / episode not found, punt
		return filename[(filename.LastIndexOf(Path.DirectorySeparatorChar) + 1)..].CleanFilePath();
	}
}
