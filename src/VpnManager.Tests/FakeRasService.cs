using VpnManager.Ras;

namespace VpnManager.Tests;

public class FakeRasService : IRasService {
	public List<VpnEntry> Entries { get; set; } = new();
	public List<string> ConnectCalls { get; } = new();
	public List<string> DisconnectCalls { get; } = new();

	public Func<Task<RasResult>> ConnectBehavior { get; set; } =
		() => Task.FromResult(new RasResult(true, null, false));

	public IReadOnlyList<VpnEntry> GetEntries() {
		return Entries.ToList();
	}

	public CancellationToken LastToken { get; private set; }

	public Task<RasResult> ConnectAsync(string entryName, CancellationToken ct) {
		ConnectCalls.Add(entryName);
		LastToken = ct;
		return ConnectBehavior();
	}

	/// <summary>Если задан, Disconnect блокируется, пока гейт не открыт (имитация долгого RasHangUp).</summary>
	public ManualResetEventSlim? DisconnectGate { get; set; }

	public void Disconnect(string entryName) {
		DisconnectCalls.Add(entryName);
		DisconnectGate?.Wait();
	}

	public static VpnEntry Off(string name) {
		return new VpnEntry(name, VpnState.Disconnected, null, null);
	}

	public static VpnEntry On(string name, TimeSpan d) {
		return new VpnEntry(name, VpnState.Connected, d, null);
	}
}