using HarmonyLib;
using Vintagestory.API.Server;
using Vintagestory.Server;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

namespace VSRun;

[HarmonyPatch(typeof(AuthServerComm))]
[HarmonyPatchCategory("Offline")]
public static class AuthServerCommPatcher {
	[HarmonyPrefix]
	[HarmonyPatch(nameof(AuthServerComm.ValidatePlayerWithServer))]
	public static bool ValidatePlayerWithServer(
		string mptokenv2,
		string playerName,
		string playerUID,
		string serverLoginToken,
		ValidationCompleteDelegate OnValidationComplete) {
		OnValidationComplete(EnumServerResponse.Good, string.Empty, null);
		return false;
	}
}