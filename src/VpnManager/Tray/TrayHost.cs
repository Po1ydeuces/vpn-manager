using System.Windows;
using System.Windows.Threading;
using VpnManager.Localization;
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
	private readonly Forms.ToolStripMenuItem _exitItem;
	private readonly Forms.ToolStripMenuItem _languageItem;
	private readonly Forms.ToolStripMenuItem _hotkeyItem;
	private readonly GlobalHotkey _hotkey = new();

	// null = язык системы; названия языков не переводятся
	private static readonly (string? Code, string Name)[] Languages = { (null, ""), ("en", "English"), ("ru", "Русский") };

	private void UpdateMenuTexts() {
		_autostartItem.Text = Loc.T("Autostart");
		_exitItem.Text = Loc.T("Exit");
		_languageItem.Text = Loc.T("Language");
		_hotkeyItem.Text = Loc.T("Hotkey");
		for (var i = 0; i < Languages.Length; i++) {
			var item = (Forms.ToolStripMenuItem)_languageItem.DropDownItems[i];
			item.Text = Languages[i].Code == null ? Loc.T("LanguageAuto") : Languages[i].Name;
			item.Checked = Languages[i].Code == _vm.Language;
		}
	}

	public TrayHost(MainViewModel vm, PopupWindow popup) {
		_vm = vm;
		_popup = popup;

		_autostartItem = new Forms.ToolStripMenuItem { CheckOnClick = false };
		_autostartItem.Click += (_, _) => {
			Autostart.SetEnabled(!Autostart.IsEnabled());
			_autostartItem.Checked = Autostart.IsEnabled();
		};
		_exitItem = new Forms.ToolStripMenuItem();
		_exitItem.Click += (_, _) => Application.Current.Shutdown();

		_hotkeyItem = new Forms.ToolStripMenuItem();
		_hotkeyItem.Click += (_, _) => {
			if (_hotkey.IsRegistered) _hotkey.Unregister();
			else _hotkey.Register();
			_vm.SetHotkeyEnabled(_hotkey.IsRegistered);
			_hotkeyItem.Checked = _hotkey.IsRegistered;
		};
		_hotkey.Pressed += OnHotkey;
		if (_vm.HotkeyEnabled) _hotkey.Register();
		_hotkeyItem.Checked = _hotkey.IsRegistered;

		_languageItem = new Forms.ToolStripMenuItem();
		foreach (var (code, _) in Languages) {
			var item = new Forms.ToolStripMenuItem { Tag = code };
			item.Click += (_, _) => {
				_vm.SetLanguage(code);
				UpdateMenuTexts();
			};
			_languageItem.DropDownItems.Add(item);
		}

		var menu = new Forms.ContextMenuStrip();
		menu.Opening += (_, _) => _autostartItem.Checked = Autostart.IsEnabled();
		menu.Items.Add(_autostartItem);
		menu.Items.Add(_languageItem);
		menu.Items.Add(_hotkeyItem);
		menu.Items.Add(_exitItem);
		UpdateMenuTexts();

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

	// С клавиатуры иконка не под рукой: popup открывается у правого нижнего угла основного экрана, где обычно трей.
	private void OnHotkey() {
		if (_popup.IsVisible) {
			_popup.Hide();
			return;
		}

		var wa = Forms.Screen.PrimaryScreen!.WorkingArea;
		Tick();
		_popup.ShowAt(new System.Drawing.Point(wa.Right, wa.Bottom));
	}

	private bool _startupDone;
	private bool _refreshing;

	private async void Tick() {
		if (_refreshing) return; // предыдущий опрос ещё идёт (RAS занят дозвоном)
		_refreshing = true;
		try {
			await _vm.RefreshAsync();
			if (!_startupDone) {
				_startupDone = true;
				_ = _vm.ConnectOnStartAsync(); // профили с флагом подключаются после первого опроса
			}
			_icon.Icon = _vm.AnyConnected ? _iconOn : _iconOff;
		} catch (Exception) {
			// следующий тик попробует снова
		} finally {
			_refreshing = false;
		}
	}

	public void Dispose() {
		_timer.Stop();
		_hotkey.Dispose();
		_trimTimer.Stop();
		_icon.Visible = false;
		_icon.Dispose();
		_iconOff.Dispose();
		_iconOn.Dispose();
	}
}