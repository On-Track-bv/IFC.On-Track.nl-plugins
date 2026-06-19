// Purpose: WPF UserControl for the bSDD Selection dockable pane
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using IfcOnTrack.Core.Bridge;
using IfcOnTrack.Core.UI;
using IfcOnTrack.Revit.Bridge;
using IfcOnTrack.Revit.Commands;
using IfcOnTrack.Revit.Model;
using WpfColor = System.Windows.Media.Color;
using WpfGrid = System.Windows.Controls.Grid;
using RevitSelectionMode = IfcOnTrack.Revit.Commands.SelectionMode;

namespace IfcOnTrack.Revit.UI;

/// <summary>
/// WPF UserControl that hosts the bSDD Selection UI in a dockable pane.
/// This panel shows all element types and their bSDD classifications.
/// </summary>
public class BsddSelectionView : UserControl
{
    private readonly ILogger<BsddSelectionView> _logger;
    private readonly BsddSelectionBridge _bridge;
    private readonly UiLoader _uiLoader;
    private readonly SelectionEventManager _selectionEventManager;
    private readonly SettingsManager _settingsManager;
    private System.Windows.Controls.Grid? _mainGrid;
    private System.Windows.Controls.Grid? _browserContainer;
    private bool _updateNotificationShown;

    // Pending data: cached so they can be re-pushed after NavigationCompleted
    private List<IfcEntity>? _pendingEntities;
    private BridgeSettings? _pendingSettings;

    private Microsoft.Web.WebView2.Wpf.WebView2? _webView;

    public BsddSelectionView(
        ILogger<BsddSelectionView> logger,
        BsddSelectionBridge bridge,
        UiLoader uiLoader,
        SelectionEventManager selectionEventManager,
        SettingsManager settingsManager)
    {
        _logger = logger;
        _bridge = bridge;
        _uiLoader = uiLoader;
        _selectionEventManager = selectionEventManager;
        _settingsManager = settingsManager;

        // Wire: JS calls bsddSearch → gets BridgeData (entities+settings+propMap) → open search window
        _bridge.SetSearchCallback(bridgeData => Dispatcher.Invoke(() => OpenSearchWindow(bridgeData)));
        
        // Wire: after search-window save, push refreshed entities via window.updateSelection([...])
        _bridge.SetRefreshCallback(entities => Dispatcher.InvokeAsync(() => PushSelectionToJs(entities)));

        // Wire: after saveSettings, push new settings via window.updateSettings({...})
        _bridge.SetPushSettingsCallback(settings => Dispatcher.InvokeAsync(() => PushSettingsToJs(settings)));

        // Wire: selection event → push result to JS
        if (selectionEventManager.Handler != null)
        {
            selectionEventManager.Handler.OnComplete = entities =>
                Dispatcher.InvokeAsync(() => PushSelectionToJs(entities));
        }

        InitializeControl();
        LoadBrowser();
    }

    private void InitializeControl()
    {
        _mainGrid = new WpfGrid { Background = Brushes.White };
        _mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // ── Determine language from settings ──────────────────────────────────
        var currentLanguage = "nl"; // Default to Dutch
        try
        {   
            var settings = _settingsManager.GetSettings();
            // Parse language: "nl-NL" → "nl", "en-US" → "en"
            if (!string.IsNullOrEmpty(settings?.Language))
            {
                currentLanguage = settings.Language.Split('-')[0].ToLowerInvariant();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load language from settings, using default 'nl'");
        }

        // ── Selection toolbar with tile buttons ───────────────────────────────
        var headerBorder = new Border
        {
            Background = new SolidColorBrush(WpfColor.FromRgb(0xF8, 0xF9, 0xFA)),
            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(0xDE, 0xE2, 0xE6)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 10, 12, 10)
        };

        var tilesPanel = new UniformGrid
        {
            Rows = 1,
            Columns = 3,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Tile 1: Make Selection
        var selectTile = CreateSelectionTile(
            "✋", 
            "Selecteer elementen", "Pick elements",
            "Selecteer elementen handmatig", "Select elements manually",
            currentLanguage,
            () => TriggerSelection(RevitSelectionMode.MakeSelection));
        tilesPanel.Children.Add(selectTile);

        // Tile 2: Select All
        var allTile = CreateSelectionTile(
            "🗂️", 
            "Alle elementen", "All elements",
            "Laad alle element types", "Load all element types",
            currentLanguage,
            () => TriggerSelection(RevitSelectionMode.SelectAll));
        tilesPanel.Children.Add(allTile);

        // Tile 3: Visible in View
        var viewTile = CreateSelectionTile(
            "👁️", 
            "Alle elementen in view", "All elements in view",
            "Laad de element types van elementen zichtbaar in de huidige view", "Load element types of elements visible in the current view",
            currentLanguage,
            () => TriggerSelection(RevitSelectionMode.SelectVisibleInView));
        tilesPanel.Children.Add(viewTile);

        headerBorder.Child = tilesPanel;
        WpfGrid.SetRow(headerBorder, 0);
        _mainGrid.Children.Add(headerBorder);

        // ── Browser container ─────────────────────────────────────────────────
        _browserContainer = new WpfGrid();
        _browserContainer.Children.Add(new TextBlock
        {
            Text = "Loading bSDD Selection...",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 14
        });
        WpfGrid.SetRow(_browserContainer, 1);
        _mainGrid.Children.Add(_browserContainer);

        Content = _mainGrid;
    }

    private Border CreateSelectionTile(string emoji, string titleNl, string titleEn, string tooltipNl, string tooltipEn, string language, Action onClick)
    {
        var isEnabled = _selectionEventManager.Event != null;

        // Select title and tooltip based on language setting ("nl" uses Dutch, otherwise English)
        var title = language == "nl" && !string.IsNullOrEmpty(titleNl) ? titleNl : titleEn;
        var tooltip = language == "nl" && !string.IsNullOrEmpty(tooltipNl) ? tooltipNl : tooltipEn;

        var tile = new Border
        {
            Background = isEnabled 
                ? new SolidColorBrush(WpfColor.FromRgb(0xFF, 0xFF, 0xFF))
                : new SolidColorBrush(WpfColor.FromRgb(0xF1, 0xF3, 0xF5)),
            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(0xDE, 0xE2, 0xE6)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = isEnabled ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
            ToolTip = tooltip
        };

        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var emojiText = new TextBlock
        {
            Text = emoji,
            FontSize = 20,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };

        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 11,
            FontWeight = FontWeights.Medium,
            Foreground = isEnabled
                ? new SolidColorBrush(WpfColor.FromRgb(0x49, 0x50, 0x57))
                : new SolidColorBrush(WpfColor.FromRgb(0xAD, 0xB5, 0xBD)),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        stackPanel.Children.Add(emojiText);
        stackPanel.Children.Add(titleText);
        tile.Child = stackPanel;

        if (isEnabled)
        {
            // Hover effect
            tile.MouseEnter += (s, e) =>
            {
                tile.Background = new SolidColorBrush(WpfColor.FromRgb(0xE7, 0xF5, 0xFF));
                tile.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(0x0D, 0x6E, 0xFD));
            };
            tile.MouseLeave += (s, e) =>
            {
                tile.Background = new SolidColorBrush(WpfColor.FromRgb(0xFF, 0xFF, 0xFF));
                tile.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(0xDE, 0xE2, 0xE6));
            };
            tile.MouseDown += (s, e) =>
            {
                tile.Background = new SolidColorBrush(WpfColor.FromRgb(0xCC, 0xE5, 0xFF));
            };
            tile.MouseUp += (s, e) =>
            {
                tile.Background = new SolidColorBrush(WpfColor.FromRgb(0xE7, 0xF5, 0xFF));
                onClick();
            };
        }

        return tile;
    }

    private void TriggerSelection(RevitSelectionMode mode)
    {
        _logger.LogInformation("TriggerSelection: mode={Mode}, Handler={HasHandler}, Event={HasEvent}",
            mode,
            _selectionEventManager.Handler != null,
            _selectionEventManager.Event != null);

        if (_selectionEventManager.Handler == null || _selectionEventManager.Event == null)
        {
            _logger.LogWarning("TriggerSelection: ExternalEvent not initialized");
            return;
        }

        _selectionEventManager.Handler.Mode = mode;
        var result = _selectionEventManager.Event.Raise();
        _logger.LogInformation("ExternalEvent.Raise() result: {Result}", result);
    }

    private void LoadBrowser()
    {
        var uiUrl = _uiLoader.GetUiUrl(UiModule.BsddSelection);
        _logger.LogInformation("Loading bSDD Selection UI from: {Url}", uiUrl);

        LoadWebView2Browser(uiUrl);
    }

private async void LoadWebView2Browser(string url)
    {
        _logger.LogInformation("Initializing WebView2 browser for .NET 8+");
        
        if (_browserContainer == null) return;
        _browserContainer.Children.Clear();
        
        try
        {
            // Revit's process context blocks the default UDF location → supply an explicit one.
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IFC.On-Track.nl", "WebView2", "Selection");
            Directory.CreateDirectory(userDataFolder);

            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment
                .CreateAsync(null, userDataFolder);

            _webView = new Microsoft.Web.WebView2.Wpf.WebView2();
            _browserContainer.Children.Add(_webView);
            
            await _webView.EnsureCoreWebView2Async(env);

            // Register bridge for JavaScript calls
            _webView.CoreWebView2.AddHostObjectToScript("bsddBridge", _bridge);

            // Polyfill window.CefSharp so the CDN page's useCefSharpBridge.ts finds the bridge.
            // We wrap each method explicitly so that typeof checks (e.g. typeof saveSettings === 'function')
            // return true — WebView2 async proxy methods are objects, not native functions.
            await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                if (!window.CefSharp && window.chrome && window.chrome.webview) {
                    window.CefSharp = {
                        BindObjectAsync: function(name) {
                            var proxy = chrome.webview.hostObjects[name];
                            window[name] = {
                                loadBridgeData:  function()    { return proxy.loadBridgeData(); },
                                loadSettings:    function()    { return proxy.loadSettings(); },
                                saveSettings:    function(j)   { return proxy.saveSettings(j); },
                                bsddSearch:      function(j)   { return proxy.bsddSearch(j); },
                                bsddSelect:      function(j)   { return proxy.bsddSelect(j); },
                            };
                            return Promise.resolve();
                        }
                    };
                }
            ");

            // Push pending data once the page has fully loaded
            _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

            _webView.Source = new Uri(url);
            _logger.LogInformation("WebView2 browser initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize WebView2 browser");
            ShowError($"Failed to load browser: {ex.Message}");
        }
    }

    private async void OnNavigationCompleted(object? sender,
        Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
    {
        _logger.LogInformation("NavigationCompleted: Success={Success}", e.IsSuccess);

        if (!e.IsSuccess)
        {
            _logger.LogWarning("Navigation failed with status: {Status}", e.WebErrorStatus);
            return;
        }

        _webView?.Focus();

        // Push pending data to the newly loaded page
        if (_pendingEntities != null)
        {
            await PushSelectionToJsAsync(_pendingEntities);
        }

        if (_pendingSettings != null)
        {
            await PushSettingsToJsAsync(_pendingSettings);
        }
    }

    private void ShowError(string message)
    {
        if (_browserContainer == null) return;
        
        _browserContainer.Children.Clear();
        _browserContainer.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = Brushes.Red,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(20)
        });
    }

    private void OpenSearchWindow(BridgeData bridgeData)
    {
        _logger.LogInformation("Opening search window with {Count} entities", bridgeData.IfcData?.Count ?? 0);

        try
        {
            var searchWindow = Host.GetService<BsddSearchView>();

            // Get the search bridge from the search window (it is wired inside BsddSearchView)
            var searchBridge = searchWindow.Bridge;

            // Wire the refresh callback: after save, push ONLY the edited elements to the panel
            // This ensures we don't reload ALL elements from the model
            searchBridge.SetRefreshCallback(
                entities => Dispatcher.InvokeAsync(() => PushSelectionToJs(entities)));

            searchWindow.Initialize(bridgeData);
            searchWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open search window");
        }
    }

    /// <summary>
    /// Pushes a refreshed element list to the browser panel via window.updateSelection([...]).
    /// Must be called on the WPF dispatcher thread.
    /// </summary>
    public async void PushSelectionToJs(List<IfcEntity> entities)
    {
        _pendingEntities = entities;
        _logger.LogInformation("Pushing {Count} entities to JS via updateSelection", entities.Count);
        try
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(entities);
            if (_webView?.CoreWebView2 != null)
                await _webView.CoreWebView2.ExecuteScriptAsync(
                    $"if (window.updateSelection) window.updateSelection({json});");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PushSelectionToJs failed");
        }
    }

private async System.Threading.Tasks.Task PushSelectionToJsAsync(List<IfcEntity> entities)
{
    _logger.LogInformation("Pushing {Count} entities to JS (post-navigation)", entities.Count);
    try
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(entities);
        if (_webView?.CoreWebView2 != null)
            await _webView.CoreWebView2.ExecuteScriptAsync(
                $"if (window.updateSelection) window.updateSelection({json});");
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "PushSelectionToJsAsync failed");
    }
}

/// <summary>
    /// Pushes updated settings to the browser panel via window.updateSettings({...}).
    /// Must be called on the WPF dispatcher thread.
    /// </summary>
    public async void PushSettingsToJs(BridgeSettings settings)
    {
        _pendingSettings = settings;
        _logger.LogInformation("Pushing settings to JS via updateSettings");
        try
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(settings);
            if (_webView?.CoreWebView2 != null)
                await _webView.CoreWebView2.ExecuteScriptAsync(
                    $"if (window.updateSettings) window.updateSettings({json});");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PushSettingsToJs failed");
        }
    }

    private async System.Threading.Tasks.Task PushSettingsToJsAsync(BridgeSettings settings)
    {
        _logger.LogInformation("Pushing settings to JS (post-navigation)");
        try
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(settings);
            if (_webView?.CoreWebView2 != null)
                await _webView.CoreWebView2.ExecuteScriptAsync(
                    $"if (window.updateSettings) window.updateSettings({json});");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PushSettingsToJsAsync failed");
        }
    }

    // ── Update notifications ──────────────────────────────────────────────────

    /// <summary>
    /// Show update notification bar at the top of the pane.
    /// </summary>
    public void ShowUpdateNotification(IfcOnTrack.Core.Update.UpdateInfo updateInfo)
    {
        if (_updateNotificationShown) return;
        _updateNotificationShown = true;
        try
        {
            Dispatcher.Invoke(() =>
            {
                // Insert notification bar at the top (row 0)
                var notificationBar = new UpdateNotificationBar(updateInfo);

                // Shift existing rows down
                _mainGrid.RowDefinitions.Insert(0, new RowDefinition { Height = GridLength.Auto });

                // Move existing content down one row
                foreach (var child in _mainGrid.Children.Cast<UIElement>().ToList())
                {
                    var currentRow = WpfGrid.GetRow(child);
                    WpfGrid.SetRow(child, currentRow + 1);
                }

                // Add notification at row 0
                WpfGrid.SetRow(notificationBar, 0);
                _mainGrid.Children.Insert(0, notificationBar);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show update notification");
        }
    }
}
