using System;

namespace Jellyfin.Plugin.TagNameRecommendations.HomeScreen;

/// <summary>
/// Payload sent by the Home Screen Sections plugin.
/// </summary>
public class HomeScreenRecommendationPayload
{
    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets optional section data.
    /// </summary>
    public string AdditionalData { get; set; } = string.Empty;
}
