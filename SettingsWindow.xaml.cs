using System.Globalization;
using System.Windows;
using PcDashboard.Models;

namespace PcDashboard;

public partial class SettingsWindow : Window
{
    public AppSettings Result { get; private set; }

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();

        var current = settings.Clone();
        Result = current.Clone();

        RefreshBox.Text = current.RefreshSeconds.ToString(CultureInfo.InvariantCulture);
        CpuThresholdBox.Text = current.CpuTempThreshold.ToString(CultureInfo.InvariantCulture);
        GpuThresholdBox.Text = current.GpuTempThreshold.ToString(CultureInfo.InvariantCulture);
        CsvCheckBox.IsChecked = current.CsvLogging;
        StartupCheckBox.IsChecked = current.StartWithWindows;
        TopmostCheckBox.IsChecked = current.AlwaysOnTop;
        TrayCheckBox.IsChecked = current.MinimizeToTray;
        FahrenheitCheckBox.IsChecked = current.Fahrenheit;
        OverlayCheckBox.IsChecked = current.OverlayEnabled;
        OverlayCornerBox.SelectedValue = current.OverlayCorner;
        if (OverlayCornerBox.SelectedIndex < 0)
        {
            OverlayCornerBox.SelectedValue = "TopRight";
        }

        OverlayFontSizeBox.Text = current.OverlayFontSize.ToString(CultureInfo.InvariantCulture);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(RefreshBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var refresh) ||
            refresh is < 1 or > 10)
        {
            System.Windows.MessageBox.Show(this, "Refresh interval must be an integer from 1 to 10.", "PcDashboard", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(CpuThresholdBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var cpuThreshold) ||
            cpuThreshold is < 30 or > 110)
        {
            System.Windows.MessageBox.Show(this, "CPU threshold must be a number from 30 to 110.", "PcDashboard", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(GpuThresholdBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var gpuThreshold) ||
            gpuThreshold is < 30 or > 110)
        {
            System.Windows.MessageBox.Show(this, "GPU threshold must be a number from 30 to 110.", "PcDashboard", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(OverlayFontSizeBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var overlayFontSize) ||
            overlayFontSize is < 10 or > 28)
        {
            System.Windows.MessageBox.Show(this, "Overlay font size must be an integer from 10 to 28.", "PcDashboard", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var overlayCorner = OverlayCornerBox.SelectedValue as string ?? "TopRight";

        Result = new AppSettings
        {
            RefreshSeconds = refresh,
            CpuTempThreshold = cpuThreshold,
            GpuTempThreshold = gpuThreshold,
            CsvLogging = CsvCheckBox.IsChecked == true,
            StartWithWindows = StartupCheckBox.IsChecked == true,
            AlwaysOnTop = TopmostCheckBox.IsChecked == true,
            MinimizeToTray = TrayCheckBox.IsChecked == true,
            Fahrenheit = FahrenheitCheckBox.IsChecked == true,
            OverlayEnabled = OverlayCheckBox.IsChecked == true,
            OverlayCorner = overlayCorner,
            OverlayFontSize = overlayFontSize
        };

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
