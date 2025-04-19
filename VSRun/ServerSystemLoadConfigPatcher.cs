using HarmonyLib;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;
using Vintagestory.Server;

namespace VSRun;

[HarmonyPatch(typeof(ServerSystemLoadConfig))]
[HarmonyPatchCategory(nameof(ServerSystemLoadConfig))]
public class ServerSystemLoadConfigPatcher {
	[HarmonyPostfix]
	[HarmonyPatch(nameof(ServerSystemLoadConfig.LoadConfig))]
	public static void LoadConfig(ServerMain server) {
		server.Config.ModPaths = ClientSettings.ModPaths.ToArray();
	}
}