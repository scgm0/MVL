namespace MVL.Utils.Extensions;

public static class NumberExtensions {
	static private readonly string[] Suffixes = ["", "k", "M", "G", "T", "P"];

	extension(int number) {
		
		public string FormatNumber() {
			if (number == 0) {
				return "0";
			}

			var isNegative = number < 0;
			var absValue = isNegative ? (long)-number : number;

			double value = absValue;
			var unitIndex = 0;

			while (value >= 1000.0 && unitIndex < Suffixes.Length - 1) {
				value /= 1000.0;
				unitIndex++;
			}

			if (value >= 999.95 && unitIndex < Suffixes.Length - 1) {
				value /= 1000.0;
				unitIndex++;
			}

			var formattedValue = value.ToString("0.#");

			return (isNegative ? "-" : "") + formattedValue + Suffixes[unitIndex];
		}
	}
}