using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.TagNameRecommendations.Models;

/// <summary>
/// Recommendation API response.
/// </summary>
public class RecommendationResponse
{
    /// <summary>
    /// Gets or sets the seed item id.
    /// </summary>
    public Guid SeedItemId { get; set; }

    /// <summary>
    /// Gets or sets the seed item name.
    /// </summary>
    public string SeedItemName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the recent watched seed items used for recommendations.
    /// </summary>
    public IReadOnlyList<RecommendationSeed> Seeds { get; set; } = [];

    /// <summary>
    /// Gets or sets the recommendation items.
    /// </summary>
    public IReadOnlyList<RecommendationCandidate> Items { get; set; } = [];
}
