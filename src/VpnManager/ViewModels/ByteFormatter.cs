using System.Globalization;

namespace VpnManager.ViewModels;

public static class ByteFormatter {
	private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

	/// <summary>1536 → "1.5 KB". Единицы по 1024.</summary>
	public static string Format(long bytes) {
		double v = bytes;
		var i = 0;
		while (v >= 1024 && i < Units.Length - 1) {
			v /= 1024;
			i++;
		}

		return i == 0
			? $"{bytes} B"
			: v.ToString(v >= 100 ? "0" : "0.#", CultureInfo.InvariantCulture) + " " + Units[i];
	}
}
