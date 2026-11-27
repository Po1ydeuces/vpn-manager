using System.Runtime.CompilerServices;
using VpnManager.Localization;
using VpnManager.Logging;
using VpnManager.Ras;
using VpnManager.Settings;
using VpnManager.ViewModels;
using static VpnManager.Tests.FakeRasService;

namespace VpnManager.Tests;

internal static class TestLanguage {
	// Тесты проверяют русские строки независимо от языка системы.
	[ModuleInitializer]
	internal static void Init() {
		Loc.SetLanguage("ru");
	}
}

public class PreferencesTests {
	private static MainViewModel Create(FakeRasService ras, ISettingsStore? store = null) {
		var vm = new MainViewModel(ras, store: store, reconnectDelay: TimeSpan.Zero);
		vm.Refresh();
		return vm;
	}

	[Fact]
	public void Pin_MovesItemToTop_AndPersists() {
		var store = new MemorySettingsStore();
		var vm = Create(new FakeRasService { Entries = { Off("A"), Off("B"), Off("C") } }, store);

		vm.Items[2].TogglePinCommand.Execute(null);

		Assert.Equal(new[] { "C", "A", "B" }, vm.Items.Select(x => x.Name));
		Assert.Equal(new[] { "C" }, store.Load().Pinned);
	}

	[Fact]
	public void Unpin_RestoresNameOrder() {
		var vm = Create(new FakeRasService { Entries = { Off("A"), Off("B") } });
		var b = vm.Items[1];
		b.IsPinned = true;
		Assert.Equal(new[] { "B", "A" }, vm.Items.Select(x => x.Name));

		b.IsPinned = false;
		Assert.Equal(new[] { "A", "B" }, vm.Items.Select(x => x.Name));
	}

	[Fact]
	public void Settings_AreRestoredOnNewViewModel() {
		var store = new MemorySettingsStore();
		var ras = new FakeRasService { Entries = { Off("A"), Off("B") } };
		var first = Create(ras, store);
		var b = first.Items[1];
		b.IsPinned = true;
		b.AutoReconnect = true;

		var second = Create(ras, store);

		Assert.Equal("B", second.Items[0].Name);
		Assert.True(second.Items[0].IsPinned);
		Assert.True(second.Items[0].AutoReconnect);
		Assert.False(second.Items[1].AutoReconnect);
	}

	[Fact]
	public void SetLanguage_ChangesTexts_AndPersists() {
		var store = new MemorySettingsStore();
		var vm = Create(new FakeRasService { Entries = { Off("A") } }, store);
		try {
			vm.SetLanguage("en");
			Assert.Equal("Disconnected", vm.Items[0].StatusText);
			Assert.Equal("No VPN connections", vm.EmptyText);
			Assert.Equal("en", store.Load().Language);

			vm.SetLanguage("ru");
			Assert.Equal("Отключено", vm.Items[0].StatusText);
		} finally {
			Loc.SetLanguage("ru");
		}
	}

	[Fact]
	public async Task AutoReconnect_ConnectionDropped_Redials() {
		var ras = new FakeRasService { Entries = { On("A", TimeSpan.Zero) } };
		var vm = Create(ras);
		vm.Items[0].AutoReconnect = true;

		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();
		await Task.Delay(50);

		Assert.Equal(new[] { "A" }, ras.ConnectCalls);
		Assert.Equal(VpnState.Connected, vm.Items[0].State);
	}

	[Fact]
	public async Task AutoReconnect_Off_DoesNotRedial() {
		var ras = new FakeRasService { Entries = { On("A", TimeSpan.Zero) } };
		var vm = Create(ras);

		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();
		await Task.Delay(50);

		Assert.Empty(ras.ConnectCalls);
	}

	[Fact]
	public async Task AutoReconnect_ManualDisconnect_DoesNotRedial() {
		var ras = new FakeRasService { Entries = { On("A", TimeSpan.Zero) } };
		var vm = Create(ras);
		vm.Items[0].AutoReconnect = true;

		await vm.Items[0].ToggleAsync();
		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();
		await Task.Delay(50);

		Assert.Empty(ras.ConnectCalls);
	}

	[Fact]
	public async Task AutoReconnect_GivesUpAfterMaxAttempts() {
		var ras = new FakeRasService {
			Entries = { On("A", TimeSpan.Zero) },
			ConnectBehavior = () => Task.FromResult(new RasResult(false, "boom", false))
		};
		var vm = Create(ras);
		vm.Items[0].AutoReconnect = true;

		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();
		await Task.Delay(100);

		Assert.Equal(VpnItemViewModel.MaxReconnectAttempts, ras.ConnectCalls.Count);
		Assert.Equal(VpnState.Error, vm.Items[0].State);
	}

	[Fact]
	public async Task AutoReconnect_UserClickCancelsPendingRedial() {
		var ras = new FakeRasService { Entries = { On("A", TimeSpan.Zero) } };
		var vm = new MainViewModel(ras, reconnectDelay: TimeSpan.FromMilliseconds(200));
		vm.Refresh();
		vm.Items[0].AutoReconnect = true;

		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();
		vm.Items[0].AutoReconnect = false; // пользователь выключил до истечения паузы
		await Task.Delay(400);

		Assert.Empty(ras.ConnectCalls);
	}

	[Fact]
	public async Task ConnectOnStart_ConnectsOnlyFlaggedDisconnectedProfiles() {
		var ras = new FakeRasService { Entries = { Off("A"), Off("B"), On("C", TimeSpan.Zero) } };
		var vm = Create(ras);
		vm.Items[0].ConnectOnStart = true; // A
		vm.Items[2].ConnectOnStart = true; // C уже подключён

		await vm.ConnectOnStartAsync();

		Assert.Equal(new[] { "A" }, ras.ConnectCalls);
	}

	[Fact]
	public async Task ConnectAll_ConnectsVisibleDisconnectedProfiles() {
		var ras = new FakeRasService { Entries = { Off("A"), Off("B"), On("C", TimeSpan.Zero), Off("D") } };
		var vm = Create(ras);
		vm.Items[3].IsHidden = true; // D скрыт

		await vm.ConnectAllAsync();

		Assert.Equal(new[] { "A", "B" }, ras.ConnectCalls.OrderBy(x => x));
	}

	[Fact]
	public async Task DisconnectAll_HangsUpConnectedProfiles() {
		var ras = new FakeRasService { Entries = { Off("A"), On("B", TimeSpan.Zero), On("C", TimeSpan.Zero) } };
		var vm = Create(ras);

		await vm.DisconnectAllAsync();

		Assert.Equal(new[] { "B", "C" }, ras.DisconnectCalls.OrderBy(x => x));
		Assert.Empty(ras.ConnectCalls);
	}

	[Fact]
	public void ConnectOnStart_IsPersisted() {
		var store = new MemorySettingsStore();
		var ras = new FakeRasService { Entries = { Off("A"), Off("B") } };
		Create(ras, store).Items[1].ConnectOnStart = true;

		var second = Create(ras, store);

		Assert.False(second.Items[0].ConnectOnStart);
		Assert.True(second.Items[1].ConnectOnStart);
	}

	[Fact]
	public void Hotkey_EnabledByDefault_AndPersisted() {
		var store = new MemorySettingsStore();
		var vm = Create(new FakeRasService(), store);
		Assert.True(vm.HotkeyEnabled);

		vm.SetHotkeyEnabled(false);

		Assert.False(Create(new FakeRasService(), store).HotkeyEnabled);
	}

	[Fact]
	public void Hide_RemovesFromVisibleSet_PersistsAndUnhides() {
		var store = new MemorySettingsStore();
		var ras = new FakeRasService { Entries = { Off("A"), Off("B") } };
		var vm = Create(ras, store);

		vm.Items[0].HideCommand.Execute(null);

		Assert.Equal(new[] { "A" }, vm.HiddenNames);
		Assert.False(vm.IsEmpty);
		Assert.Equal(new[] { "A" }, Create(ras, store).HiddenNames);

		vm.Unhide("A");
		Assert.Empty(vm.HiddenNames);
		Assert.Empty(store.Load().Hidden);
	}

	[Fact]
	public void Hide_AllProfiles_IsEmpty_ButConnectedStateStillTracked() {
		var vm = Create(new FakeRasService { Entries = { On("A", TimeSpan.Zero) } });

		vm.Items[0].IsHidden = true;

		Assert.True(vm.IsEmpty);
		Assert.True(vm.AnyConnected);
	}

	[Fact]
	public void Traffic_ShownWhileConnected_ClearedAfterDisconnect() {
		var ras = new FakeRasService {
			Entries = { new VpnEntry("A", VpnState.Connected, TimeSpan.Zero, null, 2048, 5L * 1024 * 1024) }
		};
		var vm = Create(ras);
		Assert.Equal("↓ 5 MB  ↑ 2 KB", vm.Items[0].TrafficText);

		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();

		Assert.Null(vm.Items[0].TrafficText);
	}

	private static (MainViewModel vm, List<ConnectionEvent> events) WithEvents(FakeRasService ras) {
		var vm = Create(ras);
		var events = new List<ConnectionEvent>();
		vm.Happened += events.Add;
		return (vm, events);
	}

	[Fact]
	public void Notifications_EnabledByDefault_AndPersisted() {
		var store = new MemorySettingsStore();
		var vm = Create(new FakeRasService(), store);
		Assert.True(vm.NotificationsEnabled);

		vm.SetNotificationsEnabled(false);

		Assert.False(Create(new FakeRasService(), store).NotificationsEnabled);
	}

	[Fact]
	public void Event_DroppedConnection_IsReported() {
		var ras = new FakeRasService { Entries = { On("A", TimeSpan.Zero) } };
		var (vm, events) = WithEvents(ras);

		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();

		var e = Assert.Single(events);
		Assert.Equal(ConnectionEventKind.Dropped, e.Kind);
		Assert.Equal("A", e.Name);
	}

	[Fact]
	public async Task Event_ManualDisconnect_IsNotReportedAsDrop() {
		var ras = new FakeRasService { Entries = { On("A", TimeSpan.Zero) } };
		var (vm, events) = WithEvents(ras);

		await vm.Items[0].ToggleAsync();
		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();

		var e = Assert.Single(events);
		Assert.Equal(ConnectionEventKind.Disconnected, e.Kind);
		Assert.False(e.Notify);
	}

	[Fact]
	public async Task Event_ConnectFailure_IsReportedWithDetail() {
		var ras = new FakeRasService {
			Entries = { Off("A") },
			ConnectBehavior = () => Task.FromResult(new RasResult(false, "boom", false))
		};
		var (vm, events) = WithEvents(ras);

		await vm.Items[0].ToggleAsync();

		var e = Assert.Single(events);
		Assert.Equal(ConnectionEventKind.Failed, e.Kind);
		Assert.Equal("boom", e.Detail);
		Assert.True(e.Notify);
	}

	[Fact]
	public async Task Event_ReconnectFailures_AreQuietUntilGaveUp() {
		var ras = new FakeRasService {
			Entries = { On("A", TimeSpan.Zero) },
			ConnectBehavior = () => Task.FromResult(new RasResult(false, "boom", false))
		};
		var (vm, events) = WithEvents(ras);
		vm.Items[0].AutoReconnect = true;

		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();
		await Task.Delay(100);

		Assert.Equal(ConnectionEventKind.Dropped, events[0].Kind);
		Assert.All(events.Where(e => e.Kind == ConnectionEventKind.Failed), e => Assert.False(e.Notify));
		var last = events[^1];
		Assert.Equal(ConnectionEventKind.GaveUp, last.Kind);
		Assert.True(last.Notify);
		Assert.Equal(1, events.Count(e => e.Notify && e.Kind != ConnectionEventKind.Dropped));
	}

	[Fact]
	public void Tooltip_ListsConnectedProfiles() {
		var ras = new FakeRasService { Entries = { On("Work", TimeSpan.Zero), Off("Mid"), On("Home", TimeSpan.Zero) } };
		var vm = Create(ras);

		Assert.Equal("VPN Manager \u2014 Подключено: Home, Work", vm.TooltipText);

		ras.Entries = new List<VpnEntry> { Off("Work"), Off("Mid"), Off("Home") };
		vm.Refresh();
		Assert.Equal("VPN Manager", vm.TooltipText);
	}

	[Fact]
	public void Tooltip_IsTruncatedTo127Characters() {
		var ras = new FakeRasService { Entries = { On(new string('A', 200), TimeSpan.Zero) } };

		var text = Create(ras).TooltipText;

		Assert.Equal(127, text.Length);
		Assert.EndsWith("\u2026", text);
	}

	[Fact]
	public void Theme_DefaultsToSystem_AndPersists() {
		var store = new MemorySettingsStore();
		var vm = Create(new FakeRasService(), store);
		Assert.Null(vm.Theme);

		vm.SetTheme("dark");

		Assert.Equal("dark", Create(new FakeRasService(), store).Theme);
	}

	private sealed class FakeClock : TimeProvider {
		public DateTimeOffset Now { get; set; } = new(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);

		public override DateTimeOffset GetUtcNow() {
			return Now;
		}
	}

	[Fact]
	public void Speed_IsComputedFromCounterDeltaBetweenPolls() {
		var clock = new FakeClock();
		var ras = new FakeRasService {
			Entries = { new VpnEntry("A", VpnState.Connected, TimeSpan.Zero, null, 0, 0) }
		};
		var vm = new MainViewModel(ras, clock);
		vm.Refresh();
		Assert.Equal("↓ 0 B  ↑ 0 B", vm.Items[0].TrafficText); // первый опрос: скорости ещё нет

		clock.Now += TimeSpan.FromSeconds(2);
		ras.Entries = new List<VpnEntry> {
			new("A", VpnState.Connected, TimeSpan.FromSeconds(2), null, 2048, 10 * 1024 * 1024)
		};
		vm.Refresh();

		Assert.Equal("↓ 10 MB (5 MB/s)  ↑ 2 KB (1 KB/s)", vm.Items[0].TrafficText);
	}

	[Fact]
	public void Speed_CounterWrap_DoesNotGoNegative() {
		var clock = new FakeClock();
		var ras = new FakeRasService {
			Entries = { new VpnEntry("A", VpnState.Connected, TimeSpan.Zero, null, 0, uint.MaxValue - 1023) }
		};
		var vm = new MainViewModel(ras, clock);
		vm.Refresh();

		clock.Now += TimeSpan.FromSeconds(1);
		ras.Entries = new List<VpnEntry> { new("A", VpnState.Connected, TimeSpan.Zero, null, 0, 1024) };
		vm.Refresh();

		Assert.Contains("(2 KB/s)", vm.Items[0].TrafficText);
	}

	[Fact]
	public void Speed_ResetsAfterDisconnect() {
		var clock = new FakeClock();
		var ras = new FakeRasService {
			Entries = { new VpnEntry("A", VpnState.Connected, TimeSpan.Zero, null, 0, 0) }
		};
		var vm = new MainViewModel(ras, clock);
		vm.Refresh();
		clock.Now += TimeSpan.FromSeconds(1);
		ras.Entries = new List<VpnEntry> { new("A", VpnState.Connected, TimeSpan.Zero, null, 1000, 1000) };
		vm.Refresh();

		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();
		ras.Entries = new List<VpnEntry> { new("A", VpnState.Connected, TimeSpan.Zero, null, 5, 5) };
		vm.Refresh();

		Assert.Equal("↓ 5 B  ↑ 5 B", vm.Items[0].TrafficText);
	}

	private sealed class ListLog : IConnectionLog {
		public List<ConnectionEvent> Events { get; } = new();

		public void Write(ConnectionEvent e) {
			Events.Add(e);
		}
	}

	[Fact]
	public async Task Log_RecordsConnectAndManualDisconnectWithDuration() {
		var clock = new FakeClock();
		var log = new ListLog();
		var ras = new FakeRasService { Entries = { Off("A") } };
		var vm = new MainViewModel(ras, clock, log: log);
		vm.Refresh();

		await vm.Items[0].ToggleAsync(); // подключение
		clock.Now += TimeSpan.FromMinutes(5);
		await vm.Items[0].ToggleAsync(); // отключение

		Assert.Equal(new[] { ConnectionEventKind.Connected, ConnectionEventKind.Disconnected },
			log.Events.Select(e => e.Kind));
		Assert.Equal(TimeSpan.FromMinutes(5), log.Events[1].Duration);
	}

	[Fact]
	public void Log_DropOfConnectionFoundAtStartup_UsesDurationFromRas() {
		var clock = new FakeClock();
		var log = new ListLog();
		var ras = new FakeRasService { Entries = { On("A", TimeSpan.FromMinutes(10)) } };
		var vm = new MainViewModel(ras, clock, log: log);
		vm.Refresh();

		clock.Now += TimeSpan.FromMinutes(2);
		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();

		Assert.Equal(new[] { ConnectionEventKind.Connected, ConnectionEventKind.Dropped },
			log.Events.Select(e => e.Kind));
		Assert.Equal(TimeSpan.FromMinutes(12), log.Events[1].Duration);
	}

	[Fact]
	public void Log_Format_IsReadableAndEnglish() {
		var at = new DateTimeOffset(2026, 11, 27, 12, 31, 5, TimeSpan.Zero);

		string Line(ConnectionEventKind k, TimeSpan? d = null, string? detail = null) {
			var text = ConnectionLogFormatter.Format(new ConnectionEvent(at, "Work", k, d, detail));
			return text[(text.IndexOf("  ", StringComparison.Ordinal) + 2)..]; // без локального времени
		}

		Assert.Equal("Work  Connected", Line(ConnectionEventKind.Connected));
		Assert.Equal("Work  Connection dropped after 01:02:03", Line(ConnectionEventKind.Dropped, new TimeSpan(1, 2, 3)));
		Assert.Equal("Work  Disconnected by user after 00:00:05", Line(ConnectionEventKind.Disconnected, TimeSpan.FromSeconds(5)));
		Assert.Equal("Work  Connection failed: boom", Line(ConnectionEventKind.Failed, detail: "boom"));
		Assert.Equal("Work  Auto-reconnect gave up: boom", Line(ConnectionEventKind.GaveUp, detail: "boom"));
	}

	[Fact]
	public void FileLog_AppendsLinesAndTrimsHugeFileOnStart() {
		var path = Path.Combine(Path.GetTempPath(), $"vpnmgr-{Guid.NewGuid():N}.log");
		try {
			File.WriteAllLines(path, Enumerable.Range(0, 30000).Select(i => $"line {i:D6} " + new string('x', 60)));
			Assert.True(new FileInfo(path).Length > 1024 * 1024);

			var log = new FileConnectionLog(path);
			log.Write(new ConnectionEvent(DateTimeOffset.UtcNow, "A", ConnectionEventKind.Connected));

			var lines = File.ReadAllLines(path);
			Assert.Equal(2001, lines.Length);
			Assert.EndsWith("A  Connected", lines[^1]);
			Assert.StartsWith("line 028000", lines[0]);
		} finally {
			File.Delete(path);
		}
	}

	[Fact]
	public void SpeedText_ShowsZeroOnFirstPoll_AndClearsWhenDisconnected() {
		var clock = new FakeClock();
		var ras = new FakeRasService {
			Entries = { new VpnEntry("A", VpnState.Connected, TimeSpan.Zero, null, 10, 10) }
		};
		var vm = new MainViewModel(ras, clock);
		vm.Refresh();
		Assert.Equal("↓ 0 B/s  ↑ 0 B/s", vm.Items[0].SpeedText);

		clock.Now += TimeSpan.FromSeconds(1);
		ras.Entries = new List<VpnEntry> { new("A", VpnState.Connected, TimeSpan.Zero, null, 2058, 3 * 1024 * 1024 + 10) };
		vm.Refresh();
		Assert.Equal("↓ 3 MB/s  ↑ 2 KB/s", vm.Items[0].SpeedText);

		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();
		Assert.Null(vm.Items[0].SpeedText);
	}
}
