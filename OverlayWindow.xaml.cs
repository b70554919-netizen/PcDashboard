using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace PcDashboard;

public partial class OverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExLayered = 0x00080000;
    private const int WsExNoActivate = 0x08000000;

    private string _corner = "TopRight";
    private bool _repositionQueued;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => Reposition();
    }

    public void SetCorner(string corner)
    {
        _corner = corner;
        QueueReposition();
    }

    public void SetFontSize(double fontSize)
    {
        ValuesText.FontSize = Math.Clamp(fontSize, 10, 28);
        ValuesText.LineHeight = ValuesText.FontSize + 6;
        QueueReposition();
    }

    public void UpdateValues(
        float? cpuTemp,
        float? cpuLoad,
        float? cpuPower,
        float? gpuTemp,
        float? gpuLoad,
        float? memUsed,
        float? memTotal,
        float? diskTemp,
        bool fahrenheit)
    {
        ValuesText.Text =
            $"CPU {FormatTemperature(cpuTemp, fahrenheit)}  {Format(cpuLoad, "%", 0)}  {Format(cpuPower, "W", 0)}{Environment.NewLine}" +
            $"GPU {FormatTemperature(gpuTemp, fahrenheit)}  {Format(gpuLoad, "%", 0)}{Environment.NewLine}" +
            $"RAM {Format(memUsed, "", 1)}/{Format(memTotal, " GB", 1)}{Environment.NewLine}" +
            $"SSD {FormatTemperature(diskTemp, fahrenheit)}";

        QueueReposition();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, style | WsExTransparent | WsExToolWindow | WsExLayered | WsExNoActivate);
    }

    private void QueueReposition()
    {
        if (_repositionQueued)
        {
            return;
        }

        _repositionQueued = true;
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                _repositionQueued = false;
                Reposition();
            }),
            DispatcherPriority.Loaded);
    }

    private void Reposition()
    {
        UpdateLayout();

        const double margin = 16;
        var width = ActualWidth > 0 ? ActualWidth : 220;
        var height = ActualHeight > 0 ? ActualHeight : 80;
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        var right = _corner.EndsWith("Right", StringComparison.OrdinalIgnoreCase);
        var bottom = _corner.StartsWith("Bottom", StringComparison.OrdinalIgnoreCase);

        Left = right ? screenWidth - width - margin : margin;
        Top = bottom ? screenHeight - height - margin : margin;
    }

    private static string FormatTemperature(float? celsius, bool fahrenheit)
    {
        if (!celsius.HasValue)
        {
            return "--";
        }

        var value = fahrenheit ? celsius.Value * 9 / 5 + 32 : celsius.Value;
        return $"{value:0} {(fahrenheit ? "°F" : "°C")}";
    }

    private static string Format(float? value, string unit, int decimals)
    {
        return value.HasValue ? $"{value.Value.ToString($"F{decimals}")}{unit}" : "--";
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
