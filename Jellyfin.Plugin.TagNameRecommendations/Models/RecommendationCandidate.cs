using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.TagNameRecommendations.Models;

/// <summary>
/// A recommended Jellyfin item.
/// </summary>
public class RecommendationCandidate
{
    /// <summary>
    /// Gets or sets the item id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the item name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jellyfin item type name.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the score from 0 to 100.
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Gets or sets the matched tags.
    /// </summary>
    public IReadOnlyList<string> MatchedTags { get; set; } = [];

    /// <summary>
    /// Gets or sets the matched title/name tokens.
    /// </summary>
    public IReadOnlyList<string> MatchedNameTokens { get; set; } = [];

    /// <summary>
    /// Gets or sets the recently watched seed names that contributed to the score.
    /// </summary>
    public IReadOnlyList<string> MatchedSeedNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the candidate tags.
    /// </summary>
    public IReadOnlyList<string> Tags { get; set; } = [];
}
