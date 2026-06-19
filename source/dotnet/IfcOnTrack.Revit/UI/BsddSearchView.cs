// Purpose: WPF Window for bSDD Search modal dialog
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using IfcOnTrack.Core.Bridge;
using IfcOnTrack.Core.UI;
using IfcOnTrack.Revit.Bridge;
using IfcOnTrack.Revit.Model;

namespace IfcOnTrack.Revit.UI;

/// <summary>
/// Modal WPF Window for bSDD Search and classification editing.
/// Opens with selected elements and allows editing their bSDD data.
/// </summary>
public class BsddSearchView : Window
{
    private readonly ILogger<BsddSearchView> _logger;
    private readonly BsddSearchBridge _bridge;
    private readonly UiLoader _uiLoader;
    private readonly SettingsManager _settingsManager;
    private System.Windows.Controls.Grid? _mainGrid;

    /// <summary>Exposes the underlying bridge so callers can wire callbacks.</summary>
    public BsddSearchBridge Bridge => _bridge;

    public BsddSearchView(
        ILogger<BsddSearchView> logger,
        BsddSearchBridge bridge,
        UiLoader uiLoader,
        SettingsManager settingsManager)
    {
        _logger = logger;
        _bridge = bridge;
        _uiLoader = uiLoader;
        _settingsManager = settingsManager;
        
        // Set callbacks
        _bridge.SetCloseCallback(() => Dispatcher.Invoke(Close));
        
        InitializeWindow();
    }

    /// <summary>
    /// Initialize with the full bridge data (IFC entities + settings) to edit.
    /// </summary>
    public void Initialize(BridgeData bridgeData)
    {
        _bridge.Initialize(bridgeData);
        LoadBrowser();
    }

    private void InitializeWindow()
    {
        Title = "IFC.On-Track.nl - bSDD Search";
        Width = 1200;
        Height = 800;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;
        
        _mainGrid = new System.Windows.Controls.Grid { Background = Brushes.White };
        
        // Add loading indicator
        var loadingText = new TextBlock
        {
            Text = "Loading bSDD Search...",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 14
        };
        _mainGrid.Children.Add(loadingText);
        
        Content = _mainGrid;
    }

    private void LoadBrowser()
    {
        var uiUrl = _uiLoader.GetUiUrl(UiModule.BsddSearch);
        _logger.LogInformation("Loading bSDD Search UI from: {Url}", uiUrl);

        LoadWebView2Browser(uiUrl);
    }

    private async void LoadWebView2Browser(string url)
    {
        _logger.LogInformation("Initializing WebView2 browser for search window");
        
        if (_mainGrid == null) return;
        _mainGrid.Children.Clear();
        
        try
        {
            // Revit's process context blocks the default UDF location → supply an explicit one.
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IFC.On-Track.nl", "WebView2", "Search");
            Directory.CreateDirectory(userDataFolder);

            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment
                .CreateAsync(null, userDataFolder);

            var webView = new Microsoft.Web.WebView2.Wpf.WebView2();
            _mainGrid.Children.Add(webView);
            
            await webView.EnsureCoreWebView2Async(env);

            // Register bridge for JavaScript calls
            webView.CoreWebView2.AddHostObjectToScript("bsddBridge", _bridge);

            // Polyfill window.CefSharp so the CDN page's useCefSharpBridge.ts finds the bridge.
            // WebView2 uses chrome.webview.hostObjects instead of CefSharp.BindObjectAsync.
            await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                if (!window.CefSharp && window.chrome && window.chrome.webview) {
                    window.CefSharp = {
                        BindObjectAsync: function(name) {
                            var proxy = chrome.webview.hostObjects[name];
                            window[name] = {
                                loadBridgeData:  function()    { return proxy.loadBridgeData(); },
                                save:            function(j)   { return proxy.save(j); },
                                closeWindow:     function()    { return proxy.closeWindow(); },
                            };
                            return Promise.resolve();
                        }
                    };
                }
            ");

            webView.CoreWebView2.NavigationCompleted += (s, e) =>
            {
                if (e.IsSuccess)
                    webView.Focus();
            };

            webView.Source = new Uri(url);
            _logger.LogInformation("WebView2 browser initialized for search");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize WebView2 browser");
            ShowError($"Failed to load browser: {ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        if (_mainGrid == null) return;
        
        _mainGrid.Children.Clear();
        _mainGrid.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = Brushes.Red,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(20)
        });
    }
}
