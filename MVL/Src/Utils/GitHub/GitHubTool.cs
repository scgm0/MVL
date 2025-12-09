using System.Text.Json;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;

namespace MVL.Utils.GitHub;

public static class GitHubTool {
	public static string GetProxyRequestUrl(GhProxyEnum proxy) {
		return proxy switch {
			GhProxyEnum.Cloudflare => "https://gh-proxy.com/",
			GhProxyEnum.V6 => "https://v6.gh-proxy.com/",
			GhProxyEnum.Hk => "https://hk.gh-proxy.com/",
			GhProxyEnum.Fastly => "https://cdn.gh-proxy.com/",
			GhProxyEnum.EdgeOne => "https://edgeone.gh-proxy.com/",
			_ => string.Empty,
		};
	}

	public static async Task<ApiRelease> GetLatestReleaseAsync(string gitHubApiUrl, GhProxyEnum proxy = GhProxyEnum.None) {
		var releaseUrl = $"https://api.github.com/repos/{gitHubApiUrl}/releases/latest";
		var proxyUrl = GetProxyRequestUrl(proxy);
		var requestUrl = $"{proxyUrl}{releaseUrl}";

		var stream = await new Url(requestUrl)
			.WithHeader("User-Agent", "MVL")
			.WithHeader("Accept", "application/vnd.github+json")
			.GetStreamAsync();

		var latestRelease = await JsonSerializer.DeserializeAsync(stream, SourceGenerationContext.Default.ApiRelease);

		if (proxy != GhProxyEnum.None) {
			latestRelease.Body = latestRelease.Body.Replace(proxyUrl, string.Empty);
		}

		return latestRelease;
	}
}