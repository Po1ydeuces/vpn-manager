using System.Windows;
using System.Windows.Threading;
using VpnManager.ViewModels;
using VpnManager.Views;
using Forms = System.Windows.Forms;

namespace VpnManager.Tray;

public sealed class TrayHost : IDisposable {
	private static readonly TimeSpan VisibleInterval = TimeSpan.FromSeconds(1);
	private static readonly TimeSpan HiddenInterval = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(300);

	private readonly MainViewModel _vm;
	private readonly PopupWindow _popup;
	private readonly Forms.NotifyIcon _icon;
	private readonly System.Drawing.Icon _iconOff = TrayIcons.Create(false);
	private readonly System.Drawing.Icon _iconOn = TrayIcons.Create(true);
	private readonly DispatcherTimer _timer = new();
	private readonly DispatcherTimer _trimTimer;
	private readonly Forms.ToolStripMenuItem _autostartItem;

	public TrayHost(MainViewModel vm, PopupWindow popup) {
		_vm = vm;
		_popup = popup;

		_autostartItem = new Forms.ToolStripMenuItem("Запускать при старте Windows") { CheckOnClick = false };
		_autostartItem.Click += (_, _) => {
			Autostart.SetEnabled(!Autostart.IsEnabled());
			_autostartItem.Checked = Autostart.IsEnabled();
		};
		var exitItem = new Forms.ToolStripMenuItem("Выход");
		exitItem.Click += (_, _) => Application.Current.Shutdown();

		var menu = new Forms.ContextMenuStrip();
		menu.Opening += (_, _) => _autostartItem.Checked = Autostart.IsEnabled();
		menu.Items.Add(_autostartItem);
		menu.Items.Add(exitItem);

		_icon = new Forms.NotifyIcon {
			Icon = _iconOff,
			Text = "VPN Manager",
			ContextMenuStrip = menu,
			Visible = true
		};
		_icon.MouseUp += OnIconMouseUp;

		// Через пару секунд после закрытия popup (и после старта) вернуть память системе.
		_trimTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
		_trimTimer.Tick += (_, _) => {
			_trimTimer.Stop();
			MemoryTrimmer.Trim();
		};

		_popup.IsVisibleChanged += (_, _) => {
			_timer.Interval = _popup.IsVisible ? VisibleInterval : HiddenInterval;
			if (_popup.IsVisible) {
				_trimTimer.Stop();
				Tick();
			} else {
				_trimTimer.Stop();
				_trimTimer.Start();
			}
		};
		_trimTimer.Start();

		_timer.Interval = HiddenInterval;
		_timer.Tick += (_, _) => Tick();
		_timer.Start();
		Tick();
	}

	private void OnIconMouseUp(object? sender, Forms.MouseEventArgs e) {
		if (e.Button != Forms.MouseButtons.Left) return;

		if (_popup.IsVisible) {
			_popup.Hide();
			return;
		}

		// Щелчок по иконке сначала деактивирует popup (Hide), потом приходит сюда: не открывать заново.
		if (DateTime.UtcNow - _popup.LastHiddenUtc < ReopenGuard) return;

		Tick();
		_popup.ShowAt(Forms.Cursor.Position);
	}

	private bool _refreshing;

	private async void Tick() {
		if (_refreshing) return; // предыдущий опрос ещё идёт (RAS занят дозвоном)
		_refreshing = true;
		try {
			await _vm.RefreshAsync();
			_icon.Icon = _vm.AnyConnected ? _iconOn : _iconOff;
		} catch (Exception) {
			// следующий тик попробует снова
		} finally {
			_refreshing = false;
		}
	}

	public void Dispose() {
		_timer.Stop();
		_trimTimer.Stop();
		_icon.Visible = false;
		_icon.Dispose();
		_iconOff.Dispose();
		_iconOn.Dispose();
	}
}