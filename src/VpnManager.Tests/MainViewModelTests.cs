using VpnManager.Ras;
using VpnManager.ViewModels;
using static VpnManager.Tests.FakeRasService;

namespace VpnManager.Tests;

public class MainViewModelTests {
	private static (MainViewModel vm, FakeRasService ras) Create(params VpnEntry[] entries) {
		var ras = new FakeRasService { Entries = entries.ToList() };
		var vm = new MainViewModel(ras);
		vm.Refresh();
		return (vm, ras);
	}

	[Fact]
	public void Refresh_PopulatesItems() {
		var (vm, _) = Create(Off("A"), Off("B"));
		Assert.Equal(2, vm.Items.Count);
		Assert.False(vm.IsEmpty);
	}

	[Fact]
	public void Refresh_NoEntries_IsEmpty() {
		var (vm, _) = Create();
		Assert.True(vm.IsEmpty);
	}

	[Fact]
	public void Refresh_Connected_ShowsDurationText() {
		var (vm, ras) = Create(On("A", new TimeSpan(1, 2, 3)));
		Assert.Equal("01:02:03", vm.Items[0].DurationText);
		Assert.Equal("Подключено", vm.Items[0].StatusText);

		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();
		Assert.Null(vm.Items[0].DurationText);
	}

	[Fact]
	public void Refresh_DroppedExternally_ResetsToDisconnected() {
		var (vm, ras) = Create(On("A", TimeSpan.FromSeconds(30)));
		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();
		Assert.Equal(VpnState.Disconnected, vm.Items[0].State);
		Assert.Equal("Отключено", vm.Items[0].StatusText);
		Assert.Null(vm.Items[0].DurationText);
	}

	[Fact]
	public void Refresh_KeepsItemInstances() {
		var (vm, ras) = Create(Off("A"));
		var first = vm.Items[0];
		ras.Entries = new List<VpnEntry> { On("A", TimeSpan.FromSeconds(1)) };
		vm.Refresh();
		Assert.Same(first, vm.Items[0]);
	}

	[Fact]
	public void Refresh_RemovesVanishedProfile() {
		var (vm, ras) = Create(Off("A"), Off("B"));
		ras.Entries = new List<VpnEntry> { Off("A") };
		vm.Refresh();
		Assert.Single(vm.Items);
	}

	[Fact]
	public async Task Toggle_WhenDisconnected_CallsConnect() {
		var (vm, ras) = Create(Off("A"));
		await vm.Items[0].ToggleAsync();
		Assert.Equal(new[] { "A" }, ras.ConnectCalls);
		Assert.Empty(ras.DisconnectCalls);
	}

	[Fact]
	public async Task Toggle_WhenConnected_CallsDisconnect() {
		var (vm, ras) = Create(On("A", TimeSpan.Zero));
		await vm.Items[0].ToggleAsync();
		Assert.Equal(new[] { "A" }, ras.DisconnectCalls);
		Assert.Empty(ras.ConnectCalls);
	}

	[Fact]
	public async Task Toggle_WhileConnecting_CancelsAttempt_WithoutSecondDial() {
		var tcs = new TaskCompletionSource<RasResult>();
		var (vm, ras) = Create(Off("A"));
		ras.ConnectBehavior = () => tcs.Task;

		var first = vm.Items[0].ToggleAsync();
		Assert.Equal(VpnState.Connecting, vm.Items[0].State);
		Assert.False(ras.LastToken.IsCancellationRequested);

		await vm.Items[0].ToggleAsync(); // второй щелчок = отмена

		Assert.True(ras.LastToken.IsCancellationRequested);
		Assert.Single(ras.ConnectCalls);

		tcs.SetResult(new RasResult(false, null, false, true));
		await first;

		Assert.Equal(VpnState.Disconnected, vm.Items[0].State);
		Assert.Null(vm.Items[0].ErrorText);
	}

	[Fact]
	public async Task Toggle_BackendThrows_ShowsErrorInsteadOfCrashing() {
		var (vm, ras) = Create(Off("A"));
		ras.ConnectBehavior = () => throw new InvalidOperationException("boom");

		await vm.Items[0].ToggleAsync();

		Assert.Equal(VpnState.Error, vm.Items[0].State);
		Assert.Equal("boom", vm.Items[0].ErrorText);
	}

	[Fact]
	public async Task Cancel_ShowsDisconnectedImmediately_WithoutWaitingForBackend() {
		var tcs = new TaskCompletionSource<RasResult>(); // бэкенд «завис» и не возвращается
		var (vm, ras) = Create(Off("A"));
		ras.ConnectBehavior = () => tcs.Task;

		var first = vm.Items[0].ToggleAsync();
		await vm.Items[0].ToggleAsync();

		Assert.Equal(VpnState.Disconnected, vm.Items[0].State);
		vm.Refresh(); // опрос не возвращает «Подключение…»
		Assert.Equal(VpnState.Disconnected, vm.Items[0].State);

		tcs.SetResult(new RasResult(false, null, false, true));
		await first;
	}

	[Fact]
	public async Task Cancel_ThenReconnect_StaleResultDoesNotOverrideNewAttempt() {
		var oldTcs = new TaskCompletionSource<RasResult>();
		var newTcs = new TaskCompletionSource<RasResult>();
		var (vm, ras) = Create(Off("A"));

		ras.ConnectBehavior = () => oldTcs.Task;
		var oldAttempt = vm.Items[0].ToggleAsync();
		await vm.Items[0].ToggleAsync(); // отмена

		ras.ConnectBehavior = () => newTcs.Task;
		var newAttempt = vm.Items[0].ToggleAsync(); // новая попытка
		Assert.Equal(VpnState.Connecting, vm.Items[0].State);

		oldTcs.SetResult(new RasResult(false, "stale", false)); // поздний результат отменённой попытки
		await oldAttempt;
		Assert.Equal(VpnState.Connecting, vm.Items[0].State);
		Assert.Null(vm.Items[0].ErrorText);

		newTcs.SetResult(new RasResult(true, null, false));
		await newAttempt;
		Assert.Equal(VpnState.Connected, vm.Items[0].State);
	}

	[Fact]
	public async Task Toggle_ConnectThrowsCanceled_ReturnsToDisconnectedWithoutError() {
		var tcs = new TaskCompletionSource<RasResult>();
		var (vm, ras) = Create(Off("A"));
		ras.ConnectBehavior = () => tcs.Task;

		var first = vm.Items[0].ToggleAsync();
		await vm.Items[0].ToggleAsync();
		tcs.SetCanceled();
		await first;

		Assert.Equal(VpnState.Disconnected, vm.Items[0].State);
		Assert.Null(vm.Items[0].ErrorText);
	}

	[Fact]
	public async Task Toggle_AfterCancel_CanConnectAgain() {
		var tcs = new TaskCompletionSource<RasResult>();
		var (vm, ras) = Create(Off("A"));
		ras.ConnectBehavior = () => tcs.Task;

		var first = vm.Items[0].ToggleAsync();
		await vm.Items[0].ToggleAsync();
		tcs.SetResult(new RasResult(false, null, false, true));
		await first;

		ras.ConnectBehavior = () => Task.FromResult(new RasResult(true, null, false));
		await vm.Items[0].ToggleAsync();

		Assert.Equal(2, ras.ConnectCalls.Count);
		Assert.False(ras.LastToken.IsCancellationRequested); // новая попытка со свежим токеном
		Assert.Equal(VpnState.Connected, vm.Items[0].State);
	}

	[Fact]
	public async Task Refresh_WhileLocalConnecting_KeepsConnecting() {
		var tcs = new TaskCompletionSource<RasResult>();
		var (vm, ras) = Create(Off("A"));
		ras.ConnectBehavior = () => tcs.Task;

		var t = vm.Items[0].ToggleAsync();
		vm.Refresh(); // RAS ещё говорит Disconnected
		Assert.Equal(VpnState.Connecting, vm.Items[0].State);

		tcs.SetResult(new RasResult(true, null, false));
		await t;
	}

	[Fact]
	public async Task Toggle_ConnectFails_ShowsErrorState() {
		var (vm, ras) = Create(Off("A"));
		ras.ConnectBehavior = () => Task.FromResult(new RasResult(false, "text", false));
		await vm.Items[0].ToggleAsync();

		Assert.Equal(VpnState.Error, vm.Items[0].State);
		Assert.Equal("text", vm.Items[0].ErrorText);

		vm.Refresh(); // RAS: Disconnected, ошибка остаётся до следующей попытки
		Assert.Equal(VpnState.Error, vm.Items[0].State);
		Assert.Equal("text", vm.Items[0].ErrorText);
	}

	private sealed class ManualTime : TimeProvider {
		public DateTimeOffset Now { get; set; } = new(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);

		public override DateTimeOffset GetUtcNow() {
			return Now;
		}
	}

	[Fact]
	public async Task Error_ClearsAfter30Seconds() {
		var time = new ManualTime();
		var ras = new FakeRasService { Entries = new List<VpnEntry> { Off("A") } };
		var vm = new MainViewModel(ras, time);
		vm.Refresh();
		ras.ConnectBehavior = () => Task.FromResult(new RasResult(false, "text", false));
		await vm.Items[0].ToggleAsync();
		Assert.Equal(VpnState.Error, vm.Items[0].State);

		time.Now += TimeSpan.FromSeconds(29);
		vm.Refresh();
		Assert.Equal(VpnState.Error, vm.Items[0].State);
		Assert.Equal("text", vm.Items[0].ErrorText);

		time.Now += TimeSpan.FromSeconds(1);
		vm.Refresh();
		Assert.Equal(VpnState.Disconnected, vm.Items[0].State);
		Assert.Null(vm.Items[0].ErrorText);
		Assert.Equal("Отключено", vm.Items[0].StatusText);
	}

	[Fact]
	public async Task Error_TimerRestartsOnNewFailure() {
		var time = new ManualTime();
		var ras = new FakeRasService { Entries = new List<VpnEntry> { Off("A") } };
		var vm = new MainViewModel(ras, time);
		vm.Refresh();
		ras.ConnectBehavior = () => Task.FromResult(new RasResult(false, "e", false));
		await vm.Items[0].ToggleAsync();

		time.Now += TimeSpan.FromSeconds(25);
		await vm.Items[0].ToggleAsync(); // повторная неудача: отсчёт заново
		time.Now += TimeSpan.FromSeconds(25);
		vm.Refresh();
		Assert.Equal(VpnState.Error, vm.Items[0].State);
	}

	[Fact]
	public async Task Toggle_AfterError_RetriesAndClearsError() {
		var (vm, ras) = Create(Off("A"));
		ras.ConnectBehavior = () => Task.FromResult(new RasResult(false, "text", false));
		await vm.Items[0].ToggleAsync();

		ras.ConnectBehavior = () => Task.FromResult(new RasResult(true, null, false));
		await vm.Items[0].ToggleAsync();

		Assert.Equal(2, ras.ConnectCalls.Count);
		Assert.Null(vm.Items[0].ErrorText);
		Assert.Equal(VpnState.Connected, vm.Items[0].State);
	}

	[Fact]
	public async Task Toggle_CredentialsRequired_SetsErrorText() {
		var (vm, ras) = Create(Off("A"));
		ras.ConnectBehavior = () => Task.FromResult(new RasResult(false, "691", true));
		await vm.Items[0].ToggleAsync();
		Assert.Equal("Требуется ввод учётных данных", vm.Items[0].ErrorText);
	}

	[Fact]
	public void Refresh_SortsItemsByName() {
		var (vm, ras) = Create(Off("VPN-C"), Off("vpn-a"), Off("VPN-B"));
		Assert.Equal(new[] { "vpn-a", "VPN-B", "VPN-C" }, vm.Items.Select(i => i.Name));

		ras.Entries = new List<VpnEntry> { Off("VPN-C"), Off("VPN-0"), Off("vpn-a"), Off("VPN-B") };
		vm.Refresh();
		Assert.Equal(new[] { "VPN-0", "vpn-a", "VPN-B", "VPN-C" }, vm.Items.Select(i => i.Name));
	}

	[Fact]
	public async Task Disconnect_RunsOffCallerThread_AndRefreshDoesNotRevertWhileHangingUp() {
		var (vm, ras) = Create(On("A", TimeSpan.FromSeconds(5)));
		ras.DisconnectGate = new ManualResetEventSlim(false);

		var toggle = vm.Items[0].ToggleAsync(); // не должен блокировать вызывающего
		Assert.False(toggle.IsCompleted);
		Assert.Equal(VpnState.Disconnected, vm.Items[0].State);

		vm.Refresh(); // RAS ещё показывает Connected: интерфейс не мигает
		Assert.Equal(VpnState.Disconnected, vm.Items[0].State);

		ras.DisconnectGate.Set();
		await toggle;
		Assert.Equal(new[] { "A" }, ras.DisconnectCalls);
	}

	[Fact]
	public async Task RefreshAsync_PopulatesAndSortsItems() {
		var ras = new FakeRasService { Entries = new List<VpnEntry> { Off("B"), Off("A") } };
		var vm = new MainViewModel(ras);
		await vm.RefreshAsync();
		Assert.Equal(new[] { "A", "B" }, vm.Items.Select(i => i.Name));
	}

	[Fact]
	public void AnyConnected_TrueWhenAnyConnected() {
		var (vm, ras) = Create(Off("A"), On("B", TimeSpan.Zero));
		Assert.True(vm.AnyConnected);
		ras.Entries = new List<VpnEntry> { Off("A"), Off("B") };
		vm.Refresh();
		Assert.False(vm.AnyConnected);
	}
}