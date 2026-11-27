using VpnManager.ViewModels;

namespace VpnManager.Tests;

public class ByteFormatterTests {
	[Theory]
	[InlineData(0, "0 B")]
	[InlineData(1023, "1023 B")]
	[InlineData(1024, "1 KB")]
	[InlineData(1536, "1.5 KB")]
	[InlineData(5L * 1024 * 1024, "5 MB")]
	[InlineData(300L * 1024 * 1024, "300 MB")]
	[InlineData(3L * 1024 * 1024 * 1024, "3 GB")]
	public void Format_UsesBinaryUnits(long bytes, string expected) {
		Assert.Equal(expected, ByteFormatter.Format(bytes));
	}
}
