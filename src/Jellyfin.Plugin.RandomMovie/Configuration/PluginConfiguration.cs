using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.RandomMovie.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public string TmdbApiKey { get; set; } = string.Empty;

    public string UiLanguage { get; set; } = "hu";

    public int MaxRandomPages { get; set; } = 10;
}