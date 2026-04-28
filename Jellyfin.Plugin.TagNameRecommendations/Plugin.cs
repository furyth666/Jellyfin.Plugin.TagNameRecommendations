using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Jellyfin.Plugin.TagNameRecommendations.Configuration;
using Jellyfin.Plugin.TagNameRecommendations.HomeScreen;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.TagNameRecommendations;

/// <summary>
/// Main plugin entry point.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private const string HomeScreenSectionId = "f57a21c8-6b4a-4e87-8546-cc9db8db5f3a";

    private readonly ILogger<Plugin> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="xmlSerializer">The XML serializer.</param>
    /// <param name="serverApplicationHost">The server application host.</param>
    /// <param name="logger">The logger.</param>
    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        IServerApplicationHost serverApplicationHost,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        _logger = logger;
        Instance = this;
        ConfigurationChanged += RegisterHomeScreenSection;
        RegisterHomeScreenSection(this, Configuration);
    }

    /// <inheritdoc />
    public override string Name => "Tag Name Recommendations";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("69d4f8cf-0a27-45b6-8e5f-fae06fbf9b6c");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    internal void RegisterHomeScreenSection(object? sender, BasePluginConfiguration configuration)
    {
        var homeScreenSectionsAssembly = AssemblyLoadContext.All
            .SelectMany(context => context.Assemblies)
            .FirstOrDefault(assembly => assembly.FullName?.Contains(".HomeScreenSections", StringComparison.OrdinalIgnoreCase) == true);

        if (homeScreenSectionsAssembly is null)
        {
            _logger.LogInformation("Home Screen Sections plugin is not loaded; skipping homepage recommendation section registration.");
            return;
        }

        var pluginInterfaceType = homeScreenSectionsAssembly.GetType("Jellyfin.Plugin.HomeScreenSections.PluginInterface");
        var registerSectionMethod = pluginInterfaceType?.GetMethod("RegisterSection", BindingFlags.Public | BindingFlags.Static);
        if (registerSectionMethod is null)
        {
            _logger.LogWarning("Home Screen Sections plugin is loaded, but PluginInterface.RegisterSection was not found.");
            return;
        }

        var payload = new JObject
        {
            ["id"] = HomeScreenSectionId,
            ["displayText"] = "Recommended for You",
            ["limit"] = 1,
            ["additionalData"] = "recently-watched",
            ["resultsAssembly"] = GetType().Assembly.FullName,
            ["resultsClass"] = typeof(HomeScreenRecommendationResults).FullName,
            ["resultsMethod"] = nameof(HomeScreenRecommendationResults.GetRecentlyWatchedRecommendations)
        };

        registerSectionMethod.Invoke(null, [payload]);
        _logger.LogInformation("Registered Home Screen Sections recommendation row.");
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Configuration.configPage.html",
                    GetType().Namespace)
            }
        ];
    }
}
