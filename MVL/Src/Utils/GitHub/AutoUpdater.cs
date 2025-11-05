using System;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Flurl.Http;
using Godot;

namespace MVL.Utils.GitHub;

public static class AutoUpdater {
	private const string GitHubApiUrl = "https://api.github.com/repos/scgm0/MVL";
	private const string GitHubProxy = "https://gh-proxy.com";

	public static async Task<ApiRelease?> CheckForUpdatesAsync(bool proxy = true) {
		try {
			var currentVersion = Version.Parse(ProjectSettings.GetSetting("application/config/version").AsString());
			GD.Print($"当前版本: {currentVersion}");

			var latestRelease = await GetLatestReleaseAsync(proxy);
			var latestVersion = Version.Parse(latestRelease.TagName.TrimStart('v'));

			if (latestVersion > currentVersion) {
				GD.Print($"发现新版本: {latestVersion}");
				if (proxy) {
					latestRelease.Body = latestRelease.Body.Replace($"{GitHubProxy}/", "");
				}

				return latestRelease;
			}

			GD.Print("当前已是最新版本。");
			return null;
		} catch (Exception ex) {
			GD.PrintErr($"检查更新失败: {ex}");
			return null;
		}
	}

	public static async Task<ApiRelease> GetLatestReleaseAsync(bool proxy = true) {
		var stream = await (proxy ? $"{GitHubProxy}/{GitHubApiUrl}/releases/latest" : $"{GitHubApiUrl}/releases/latest")
			.WithHeader("User-Agent", "MVL")
			.GetStreamAsync();
		var latestRelease = await JsonSerializer.DeserializeAsync(stream, SourceGenerationContext.Default.ApiRelease);
		GD.Print($"最新版本: {latestRelease.TagName.TrimStart('v')}");
		return latestRelease;
	}
}