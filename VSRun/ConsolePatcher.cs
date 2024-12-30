using HarmonyLib;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace VSRun;

[HarmonyPatch(typeof(Console))]
[HarmonyPatchCategory(nameof(Console))]
public static class ConsolePatcher {
	[HarmonyPostfix]
	[HarmonyPatch(nameof(Console.ForegroundColor), MethodType.Setter)]
	public static void SetForegroundColor(ConsoleColor value) {
		var color = value switch {
			ConsoleColor.Gray => "\e[90m",
			ConsoleColor.Red => "\e[91m",
			ConsoleColor.Green => "\e[92m",
			ConsoleColor.Yellow => "\e[93m",
			ConsoleColor.Blue => "\e[94m",
			ConsoleColor.Magenta => "\e[95m",
			ConsoleColor.Cyan => "\e[96m",
			ConsoleColor.White => "\e[97m",
			ConsoleColor.Black => "\e[30m",
			ConsoleColor.DarkRed => "\e[31m",
			ConsoleColor.DarkGreen => "\e[32m",
			ConsoleColor.DarkYellow => "\e[33m",
			ConsoleColor.DarkBlue => "\e[34m",
			ConsoleColor.DarkMagenta => "\e[35m",
			ConsoleColor.DarkCyan => "\e[36m",
			ConsoleColor.DarkGray => "\e[37m",
			_ => "\e[39m"
		};
		Console.Write(color);
	}

	[HarmonyPostfix]
	[HarmonyPatch(nameof(Console.BackgroundColor), MethodType.Setter)]
	public static void SetBackgroundColor(ConsoleColor value) {
		var color = value switch {
			ConsoleColor.Gray => "\e[100m",
			ConsoleColor.Red => "\e[101m",
			ConsoleColor.Green => "\e[102m",
			ConsoleColor.Yellow => "\e[103m",
			ConsoleColor.Blue => "\e[104m",
			ConsoleColor.Magenta => "\e[105m",
			ConsoleColor.Cyan => "\e[106m",
			ConsoleColor.White => "\e[107m",
			ConsoleColor.Black => "\e[40m",
			ConsoleColor.DarkRed => "\e[41m",
			ConsoleColor.DarkGreen => "\e[42m",
			ConsoleColor.DarkYellow => "\e[43m",
			ConsoleColor.DarkBlue => "\e[44m",
			ConsoleColor.DarkMagenta => "\e[45m",
			ConsoleColor.DarkCyan => "\e[46m",
			ConsoleColor.DarkGray => "\e[47m",
			_ => "\e[49m"
		};
		Console.Write(color);
	}

	[HarmonyPostfix]
	[HarmonyPatch(nameof(Console.ResetColor))]
	public static void ResetColor() => Console.Write("\e[0m");
}