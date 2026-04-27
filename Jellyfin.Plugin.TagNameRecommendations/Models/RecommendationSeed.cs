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
}
