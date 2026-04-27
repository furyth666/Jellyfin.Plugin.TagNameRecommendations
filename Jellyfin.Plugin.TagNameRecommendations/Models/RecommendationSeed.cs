using System;

namespace Jellyfin.Plugin.TagNameRecommendations.Models;

/// <summary>
/// A seed item used to produce recommendations.
/// </summary>
public class RecommendationSeed
{
    /// <summary>
    /// Gets or sets the seed item id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the seed item name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last played date for the user.
    /// </summary>
    public DateTime? LastPlayedDate { get; set; }

    /// <summary>
    /// Gets or sets the latest Playback Reporting activity date for the user.
    /// </summary>
    public DateTime? LastPlaybackActivityDate { get; set; }

    /// <summary>
    /// Gets or sets the real playback duration reported by Playback Reporting, in seconds.
    /// </summary>
    public int ActualPlaybackSeconds { get; set; }

    /// <summary>
    /// Gets or sets how many Playback Reporting records contributed to the real playback duration.
    /// </summary>
    public int PlaybackReportPlayCount { get; set; }

    /// <summary>
    /// Gets or sets the saved playback position in ticks.
    /// </summary>
    public long PlaybackPositionTicks { get; set; }

    /// <summary>
    /// Gets or sets the saved playback position in seconds.
    /// </summary>
    public double PlaybackPositionSeconds { get; set; }

    /// <summary>
    /// Gets or sets the media runtime in ticks.
    /// </summary>
    public long? RunTimeTicks { get; set; }

    /// <summary>
    /// Gets or sets the watched percentage inferred from playback position and runtime.
    /// </summary>
    public double? PlayedPercentage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item is marked played.
    /// </summary>
    public bool Played { get; set; }
}
