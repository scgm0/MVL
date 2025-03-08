using HarmonyLib;
using Vintagestory.Client.MaxObf;
using Vintagestory.Client.NoObf;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

namespace VSRun;

[HarmonyPatch(typeof(SessionManager))]
[HarmonyPatchCategory(nameof(SessionManager))]
public static class SessionManagerPatcher {
	[HarmonyPrefix]
	[HarmonyPatch(nameof(SessionManager.IsCachedSessionKeyValid))]
	public static bool IsCachedSessionKeyValid(ref bool __result) {
		__result = true;
		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(nameof(SessionManager.ValidateSessionKeyWithServer))]
	public static bool ValidateSessionKeyWithServer(Action<EnumAuthServerResponse> OnValidationComplete) {
		OnValidationComplete(EnumAuthServerResponse.Offline);
		return false;
	}
}