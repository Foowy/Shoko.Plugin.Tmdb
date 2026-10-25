using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Plugin;
using Shoko.Plugin.Tmdb.Api;
using Shoko.Plugin.Tmdb.Metadata;
using Shoko.Plugin.Tmdb.Services;

namespace Shoko.Plugin.Tmdb;

/// <summary>
///   The bundled first-party plugin for TMDB, which serves the <c>tmdb</c>
///   source. Its identity and images are in the project.
/// </summary>
public partial class Plugin : IPlugin<TmdbConfiguration>, IPluginServiceRegistration
{
    /// <summary>
    ///   The embedded resource of the plugin's icon, which is TMDB's source icon
    ///   too.
    /// </summary>
    internal const string IconResourceName = "Shoko.Plugin.Tmdb.PluginIcon.svg";

    /// <summary>
    ///   Registers the plugin's services and provider, as singletons the
    ///   server and the plugin share.
    /// </summary>
    /// <param name="serviceCollection">The services.</param>
    /// <param name="applicationPaths">Unused.</param>
    public static void RegisterServices(IServiceCollection serviceCollection, IApplicationPaths applicationPaths)
    {
        serviceCollection.AddSingleton<TmdbApiClient>();
        serviceCollection.AddSingleton<TmdbStores>();
        serviceCollection.AddSingleton<TmdbTagService>();
        serviceCollection.AddSingleton<TmdbLinkingService>();
        serviceCollection.AddSingleton<TmdbRefreshService>();
        serviceCollection.AddSingleton<TmdbEntityRefreshService>();
        serviceCollection.AddSingleton<TmdbImageService>();
        serviceCollection.AddSingleton<TmdbSearchService>();
        serviceCollection.AddSingleton<TmdbMetadataProvider>();
        serviceCollection.AddHostedService<TmdbBackgroundService>();
    }
}
