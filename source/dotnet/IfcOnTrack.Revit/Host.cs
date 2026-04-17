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
#if NET48
    // net48: Microsoft.Extensions.DependencyInjection's ServiceProvider implements
    // IAsyncDisposable, whose type identity conflicts with Revit's own loaded assemblies,
    // causing TypeLoadException at runtime. Bypass it entirely with a minimal container.
    private static Net48ServiceProvider? _net48Services;
#endif

    /// <summary>
    /// Gets the service provider for DI resolution.
    /// </summary>
    public static IServiceProvider ServiceProvider =>
#if NET48
        (IServiceProvider?)_net48Services ?? throw new InvalidOperationException("Host not started");
#else
        _host?.Services ?? throw new InvalidOperationException("Host not started");
#endif

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

#if NET48
        // net48 (Revit 2024): Microsoft.Extensions.DependencyInjection's ServiceProvider
        // implements IAsyncDisposable, which causes a TypeLoadException on net48 because
        // IAsyncDisposable's type identity from Microsoft.Bcl.AsyncInterfaces conflicts with
        // Revit's own loaded copy. Use a minimal hand-rolled DI container instead.
        var loggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(Log.Logger);
        _net48Services = new Net48ServiceProvider(loggerFactory);
        RegisterNet48Services(_net48Services, loggerFactory);
#else
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
#endif
    }

#if NET48
    private static void RegisterNet48Services(Net48ServiceProvider sp, ILoggerFactory loggerFactory)
    {
        sp.Singleton<ILoggerFactory>(_ => loggerFactory);
        sp.Singleton<LicenseManager>(p => new LicenseManager(
            loggerFactory.CreateLogger<LicenseManager>()));
        sp.Singleton<UiLoader>(p => new UiLoader(
            Application.PluginDirectory,
            loggerFactory.CreateLogger<UiLoader>()));
        sp.Singleton<SettingsManager>(p => new SettingsManager(
            loggerFactory.CreateLogger<SettingsManager>()));
        sp.Singleton<ParametersManager>(p => new ParametersManager(
            loggerFactory.CreateLogger<ParametersManager>()));
        sp.Singleton<ParameterDataManagement>(p => new ParameterDataManagement(
            loggerFactory.CreateLogger<ParameterDataManagement>(),
            p.GetRequiredService<ParametersManager>(),
            p.GetRequiredService<SettingsManager>()));
        sp.Singleton<ElementsManager>(p => new ElementsManager(
            loggerFactory.CreateLogger<ElementsManager>(),
            p.GetRequiredService<SettingsManager>(),
            p.GetRequiredService<ParameterDataManagement>(),
            p.GetRequiredService<ParametersManager>()));
        sp.Singleton<LastSelectionCache>(_ => new LastSelectionCache());
        sp.Singleton<SelectionEventHandler>(p => new SelectionEventHandler(
            p.GetRequiredService<ElementsManager>(),
            p.GetRequiredService<LastSelectionCache>(),
            loggerFactory.CreateLogger<SelectionEventHandler>()));
        sp.Singleton<SelectionEventManager>(_ => new SelectionEventManager());
        sp.Singleton<IfcClassificationManager>(p => new IfcClassificationManager(
            loggerFactory.CreateLogger<IfcClassificationManager>(),
            p.GetRequiredService<SettingsManager>()));
        sp.Singleton<IfcPostprocessor>(p => new IfcPostprocessor(
            loggerFactory.CreateLogger<IfcPostprocessor>()));
        sp.Singleton<IfcExportService>(p => new IfcExportService(
            loggerFactory.CreateLogger<IfcExportService>(),
            p.GetRequiredService<SettingsManager>(),
            p.GetRequiredService<ElementsManager>(),
            p.GetRequiredService<IfcClassificationManager>(),
            p.GetRequiredService<IfcPostprocessor>()));
        sp.Singleton<BsddSelectionBridge>(p => new BsddSelectionBridge(
            loggerFactory.CreateLogger<BsddSelectionBridge>(),
            p.GetRequiredService<ElementsManager>(),
            p.GetRequiredService<SettingsManager>()));
        sp.Transient<BsddSearchBridge>(p => new BsddSearchBridge(
            loggerFactory.CreateLogger<BsddSearchBridge>(),
            p.GetRequiredService<ElementsManager>(),
            p.GetRequiredService<SettingsManager>()));
        sp.Transient<BsddViewModel>(p => new BsddViewModel(
            loggerFactory.CreateLogger<BsddViewModel>(),
            p.GetRequiredService<LicenseManager>()));
        sp.Transient<BsddSearchView>(p => new BsddSearchView(
            loggerFactory.CreateLogger<BsddSearchView>(),
            p.GetRequiredService<BsddSearchBridge>(),
            p.GetRequiredService<UiLoader>(),
            p.GetRequiredService<SettingsManager>()));
        sp.Singleton<BsddSelectionView>(p => new BsddSelectionView(
            loggerFactory.CreateLogger<BsddSelectionView>(),
            p.GetRequiredService<BsddSelectionBridge>(),
            p.GetRequiredService<UiLoader>(),
            p.GetRequiredService<SelectionEventManager>(),
            p.GetRequiredService<SettingsManager>()));
    }
#else
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
#endif

    /// <summary>
    /// Stops the host and handles IHostedService services.
    /// </summary>
    public static void Stop()
    {
#if NET48
        _net48Services = null;
#else
        _host?.StopAsync().GetAwaiter().GetResult();
#endif
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
#if NET48
        return _net48Services?.GetService<T>();
#else
        return _host?.Services.GetService<T>();
#endif
    }

#if NET48
    /// <summary>
    /// Minimal DI container for net48. Avoids Microsoft.Extensions.DependencyInjection.ServiceProvider
    /// which implements IAsyncDisposable in a way that causes TypeLoadException on net48.
    /// </summary>
    private sealed class Net48ServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, Func<IServiceProvider, object>> _registrations = new();
        private readonly Dictionary<Type, object> _singletonCache = new();
        private readonly ILoggerFactory _loggerFactory;

        public Net48ServiceProvider(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public void Singleton<T>(Func<IServiceProvider, T> factory) where T : class
            => _registrations[typeof(T)] = sp =>
            {
                if (!_singletonCache.TryGetValue(typeof(T), out var v))
                    _singletonCache[typeof(T)] = v = factory(sp);
                return v;
            };

        public void Transient<T>(Func<IServiceProvider, T> factory) where T : class
            => _registrations[typeof(T)] = sp => factory(sp);

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(ILoggerFactory))
                return _loggerFactory;
            if (serviceType.IsGenericType &&
                serviceType.GetGenericTypeDefinition() == typeof(ILogger<>))
                return _loggerFactory.CreateLogger(
                    serviceType.GetGenericArguments()[0].FullName!);

            return _registrations.TryGetValue(serviceType, out var f) ? f(this) : null;
        }
    }
#endif
}
