using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TagNameRecommendations.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        TagWeight = 0.75;
        NameWeight = 0.25;
        NameContainsBonus = 0.08;
        DefaultLimit = 20;
        RecentWatchedCount = 8;
        MinimumSeedPlaybackSeconds = 60;
        FavoriteVideoSeedWeight = 0.75;
        FavoriteActorWeight = 0.2;
        MaxResultsPerFavoriteActor = 2;
        DiversityCandidateMultiplier = 4;
        MaxCandidates = 500;
        MinimumScore = 5;
        ExcludePlayedItems = true;
    }

    /// <summary>
    /// Gets or sets the tag similarity weight.
    /// </summary>
    public double TagWeight { get; set; }

    /// <summary>
    /// Gets or sets the title/name similarity weight.
    /// </summary>
    public double NameWeight { get; set; }

    /// <summary>
    /// Gets or sets the bonus added when one title contains the other.
    /// </summary>
    public double NameContainsBonus { get; set; }

    /// <summary>
    /// Gets or sets the default recommendation limit.
    /// </summary>
    public int DefaultLimit { get; set; }

    /// <summary>
    /// Gets or sets how many recently watched items are used as recommendation seeds.
    /// </summary>
    public int RecentWatchedCount { get; set; }

    /// <summary>
    /// Gets or sets the minimum real playback duration, in seconds, for Playback Reporting seeds.
    /// </summary>
    public int MinimumSeedPlaybackSeconds { get; set; }

    /// <summary>
    /// Gets or sets the seed weight used for favorited videos.
    /// </summary>
    public double FavoriteVideoSeedWeight { get; set; }

    /// <summary>
    /// Gets or sets the bonus weight used when a candidate contains favorited actors.
    /// </summary>
    public double FavoriteActorWeight { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of returned items that may match the same favorited actor.
    /// </summary>
    public int MaxResultsPerFavoriteActor { get; set; }

    /// <summary>
    /// Gets or sets how many extra scored items are kept for diversity re-ranking.
    /// </summary>
    public int DiversityCandidateMultiplier { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of library candidates to score.
    /// </summary>
    public int MaxCandidates { get; set; }

    /// <summary>
    /// Gets or sets the minimum returned score, from 0 to 100.
    /// </summary>
    public double MinimumScore { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether already played items should be excluded.
    /// </summary>
    public bool ExcludePlayedItems { get; set; }
}
