using VpnManager.ViewModels;

namespace VpnManager.Tests;

public class DurationFormatterTests {
	[Theory]
	[InlineData(0, 0, 0, "00:00:00")]
	[InlineData(0, 5, 7, "00:05:07")]
	[InlineData(3, 2, 1, "03:02:01")]
	[InlineData(26, 0, 0, "26:00:00")]
	public void Format_ReturnsHoursMinutesSeconds(int h, int m, int s, string expected) {
		Assert.Equal(expected, DurationFormatter.Format(new TimeSpan(h, m, s)));
	}
}