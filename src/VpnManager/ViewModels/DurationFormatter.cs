namespace VpnManager.ViewModels;

public static class DurationFormatter {
	public static string Format(TimeSpan duration) {
		return $"{(int) duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
	}
}