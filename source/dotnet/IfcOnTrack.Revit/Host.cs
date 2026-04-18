// Purpose: Provides DI host for the application's services and manages their lifetimes
using System.IO;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using IfcOnTrack.Core.License;
using IfcOnTrack.Core.UI;
using IfcOnTrack.Revit.Bridge;
using IfcOnTrack.Revit.Commands;
using IfcOnTrack.Revit.IfcExport;
using IfcOnTrack.Revit.Model;
using IfcOnTrack.Revit.UI;
using IfcOnTrack.Revit.ViewModels;

namespace IfcOnTrack.Revit;

/// <summary>
/// Provides a host for the application's services and manages their lifetimes.
/// </summary>
public static class Host
{
    private static IHost? _host;

    /// <summary>
    /// Gets the service provider for DI resolution.
    /// </summary>
    public static IServiceProvider ServiceProvider =>
        _host?.Services ?? throw new InvalidOperationException("Host not started");

    /// <summary>
    /// Starts the host and configures the application's services.
    /// </summary>
    public static void Start()
    {
        // Logging (Serilog → Debug output + rolling file)
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IFC.On-Track.nl", "logs", "plugin-.log");
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Debug(LogEventLevel.Debug)
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7, restrictedToMinimumLevel: LogEventLevel.Debug)
            .MinimumLevel.Debug()
            .CreateLogger();

        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            DisableDefaults = true
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger);

        ConfigureServices(builder.Services);

        _host = builder.Build();
        _host.Start();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core services
        services.AddSingleton<LicenseManager>();
        services.AddSingleton(sp => new UiLoader(
            Application.PluginDirectory,
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<UiLoader>>()
        ));

        // Settings (singleton – one settings state per Revit session)
        services.AddSingleton<SettingsManager>();

        // Parameter management
        services.AddSingleton<ParametersManager>();
        services.AddSingleton<ParameterDataManagement>();

        // Element data management
        services.AddSingleton<ElementsManager>();
        services.AddSingleton<LastSelectionCache>();

        // Selection event infrastructure (ExternalEvent created in Application.OnStartup)
        services.AddSingleton<SelectionEventHandler>();
        services.AddSingleton<SelectionEventManager>();

        // IFC Export pipeline
        services.AddSingleton<IfcClassificationManager>();
        services.AddSingleton<IfcPostprocessor>();
        services.AddSingleton<IfcExportService>();

        // JavaScript bridges (singleton for persistent state)
        services.AddSingleton<BsddSelectionBridge>();
        services.AddTransient<BsddSearchBridge>();

        // ViewModels
        services.AddTransient<BsddViewModel>();

        // Views
        services.AddTransient<BsddSearchView>();
        services.AddSingleton<BsddSelectionView>(); // Singleton: same instance in dockable pane
    }

    /// <summary>
    /// Stops the host and handles IHostedService services.
    /// </summary>
    public static void Stop()
    {
        _host?.StopAsync().GetAwaiter().GetResult();
        Log.CloseAndFlush();
    }

    /// <summary>
    /// Get service of type T. Throws if not found.
    /// </summary>
    public static T GetService<T>() where T : class
    {
        return ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Try to get service of type T – returns null if not found or host not started.
    /// </summary>
    public static T? TryGetService<T>() where T : class
    {
        return _host?.Services.GetService<T>();
    }
}
