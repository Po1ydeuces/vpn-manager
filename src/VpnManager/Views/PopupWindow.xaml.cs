using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using VpnManager.Tray;
using VpnManager.ViewModels;

namespace VpnManager.Views;

public partial class PopupWindow : Window {
	private ResourceDictionary? _theme;

	public PopupWindow(MainViewModel vm) {
		InitializeComponent();
		DataContext = vm;
	}

	/// <summary>Время последнего скрытия: TrayHost по нему отличает «щелчок закрыл popup» от «щелчок открыл».</summary>
	public DateTime LastHiddenUtc { get; private set; } = DateTime.MinValue;

	/// <param name="anchorPx">Точка иконки/курсора в физических пикселях экрана.</param>
	public void ShowAt(System.Drawing.Point anchorPx) {
		ApplyTheme();

		// Приложение system-DPI-aware: DIP = пиксели / системный масштаб.
		var dpi = VisualTreeHelper.GetDpi(this);
		double sx = dpi.DpiScaleX, sy = dpi.DpiScaleY;

		var screen = System.Windows.Forms.Screen.FromPoint(anchorPx);
		var wa = screen.WorkingArea;
		var workArea = new Rect(wa.Left / sx, wa.Top / sy, wa.Width / sx, wa.Height / sy);
		var anchor = new Point(anchorPx.X / sx, anchorPx.Y / sy);

		_workArea = workArea;
		_anchor = anchor;
		MaxHeight = workArea.Height; // длинный список прокручивается, а не уезжает за экран

		// Высота зависит от содержимого: измерить до показа.
		Measure(new Size(Width, double.PositiveInfinity));
		Reposition(DesiredSize.Height);
		Show();
		Activate();
	}

	private Rect _workArea;
	private Point _anchor;

	private void Reposition(double height) {
		var pos = PopupPositioner.Calculate(_workArea, _anchor, new Size(Width, Math.Min(height, _workArea.Height)));
		Left = pos.X;
		Top = pos.Y;
	}

	// Высота меняется, пока popup открыт (ошибки, новые профили): удерживать окно в рабочей области.
	private void Window_SizeChanged(object sender, SizeChangedEventArgs e) {
		if (IsVisible && e.HeightChanged) Reposition(e.NewSize.Height);
	}

	private void ApplyTheme() {
		var systemLight = Registry.GetValue(
			@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
			"AppsUseLightTheme", 1) is not int v || v != 0;
		var light = (DataContext as MainViewModel)?.Theme switch {
			"light" => true,
			"dark" => false,
			_ => systemLight
		};

		if (_theme != null) Resources.MergedDictionaries.Remove(_theme);
		_theme = new ResourceDictionary {
			Source = new Uri($"/VpnManager;component/Views/Themes/{(light ? "Light" : "Dark")}.xaml", UriKind.Relative)
		};
		Resources.MergedDictionaries.Add(_theme);
	}

	private void Window_KeyDown(object sender, KeyEventArgs e) {
		if (e.Key == Key.Escape) Hide();
	}

	private void Window_Deactivated(object? sender, EventArgs e) {
		Hide();
	}

	private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
		if (!IsVisible) LastHiddenUtc = DateTime.UtcNow;
	}
}