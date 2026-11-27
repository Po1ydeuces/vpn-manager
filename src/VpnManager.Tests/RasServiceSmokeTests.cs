using VpnManager.Ras;
using Xunit.Abstractions;

namespace VpnManager.Tests;

// Только чтение: проверяет, что P/Invoke-структуры совпадают с системными (dwSize принят API).
public class RasServiceSmokeTests(ITestOutputHelper output) {
	[Fact]
	public void GetEntries_ReadsRealProfiles() {
		var entries = new RasService().GetEntries();
		foreach (var e in entries)
			output.WriteLine($"{e.Name} | {e.State} | {e.Duration}");
		Assert.NotNull(entries);
	}
}