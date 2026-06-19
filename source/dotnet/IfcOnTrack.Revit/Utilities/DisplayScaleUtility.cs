using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace IfcOnTrack.Revit.Utilities;

/// <summary>
/// Computes display scale for the current monitor so the embedded web UI can
/// adjust its zoom level to match. Formula: (dpi / 96.0) * 0.85
/// </summary>
internal static class DisplayScaleUtility
{
    [DllImport("user32.dll")]
    private static extern int GetDpiForWindow(nint hwnd);

    public static double GetScale()
    {
        try
        {
            nint hwnd = 0;

            // Must access WPF objects on the UI thread.
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var window = System.Windows.Application.Current?.MainWindow;
                if (window != null)
                    hwnd = new WindowInteropHelper(window).Handle;
            });

            if (hwnd != 0)
            {
                var dpi = GetDpiForWindow(hwnd);
                if (dpi > 0)
                    return (dpi / 96.0) * 0.85;
            }
        }
        catch { /* non-critical; fall through to default */ }

        return 1.0;
    }
}
