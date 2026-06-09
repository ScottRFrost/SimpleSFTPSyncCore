namespace SimpleSFTPSyncCore;

public class ProHttpClient
{
	private static readonly HttpClient _httpClient = new(new SocketsHttpHandler
	{
		PooledConnectionLifetime = TimeSpan.FromMinutes(15)
	})
	{
		Timeout = TimeSpan.FromSeconds(30)
	};

	static ProHttpClient()
	{
		_httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.5");
		_httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Connection", "keep-alive");
		_httpClient.DefaultRequestHeaders.TryAddWithoutValidation("DNT", "1");
		_httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:151.0) Gecko/20100101 Firefox/151.0");
	}

	public string AuthorizationHeader { get; set; } = string.Empty;

	public string ReferrerUri { get; set; } = "https://duckduckgo.com";

	public async Task<string> DownloadString(string uri)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, uri);
		BuildHeaders(request);
		using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
	}

	public async Task<Stream> DownloadData(string uri)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, uri);
		BuildHeaders(request);
		var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
	}

	private void BuildHeaders(HttpRequestMessage request)
	{
		request.Headers.Referrer = new Uri(ReferrerUri);
		if (!string.IsNullOrEmpty(AuthorizationHeader))
		{
			request.Headers.TryAddWithoutValidation("Authorization", AuthorizationHeader);
		}
	}
}
