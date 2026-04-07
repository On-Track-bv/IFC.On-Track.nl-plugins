// Purpose: Provides DI host for the application's services and manages their lifetimes
using System.IO;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IfcOnTrack.Core.License;
using IfcOnTrack.Core.UI;
using IfcOnTrack.Revit.Bridge;
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
        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            DisableDefaults = true
        });

        // Configure logging
        builder.Services.AddLogging(logging =>
        {
            logging.AddDebug();
            logging.SetMinimumLevel(LogLevel.Debug);
        });

        // Core services
        builder.Services.AddSingleton<LicenseManager>();
        builder.Services.AddSingleton(sp => new UiLoader(
            Application.PluginDirectory,
            sp.GetRequiredService<ILogger<UiLoader>>()
        ));

        // Model managers
        builder.Services.AddSingleton<ElementsManager>();
        builder.Services.AddSingleton<SettingsManager>();

        // JavaScript bridges (singleton for persistent state)
        builder.Services.AddSingleton<BsddSelectionBridge>();
        builder.Services.AddTransient<BsddSearchBridge>();

        // ViewModels
        builder.Services.AddTransient<BsddViewModel>();

        // Views
        builder.Services.AddTransient<BsddWindow>();
        builder.Services.AddTransient<BsddSelectionView>();
        builder.Services.AddTransient<BsddSearchView>();

        _host = builder.Build();
        _host.Start();
    }

    /// <summary>
    /// Stops the host and handles IHostedService services.
    /// </summary>
    public static void Stop()
    {
        _host?.StopAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Get service of type T.
    /// </summary>
    /// <typeparam name="T">The type of service object to get</typeparam>
    /// <exception cref="InvalidOperationException">There is no service of type T</exception>
    public static T GetService<T>() where T : class
    {
        return _host?.Services.GetRequiredService<T>() 
            ?? throw new InvalidOperationException($"Host not started. Call Host.Start() first.");
    }

    /// <summary>
    /// Try to get service of type T.
    /// </summary>
    /// <typeparam name="T">The type of service object to get</typeparam>
    /// <returns>Service instance or null if not found</returns>
    public static T? TryGetService<T>() where T : class
    {
        return _host?.Services.GetService<T>();
    }
}
