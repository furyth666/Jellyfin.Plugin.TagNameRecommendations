using System;
using Jellyfin.Plugin.TagNameRecommendations.Models;

namespace Jellyfin.Plugin.TagNameRecommendations.Services;

/// <summary>
/// Produces simple item recommendations.
/// </summary>
public interface IRecommendationService
{
    /// <summary>
    /// Gets recommendations similar to a seed item.
    /// </summary>
    /// <param name="itemId">The seed item id.</param>
    /// <param name="userId">Optional user id for access filtering.</param>
    /// <param name="limit">Optional result limit.</param>
    /// <returns>The recommendation response, or null if the seed item was not found.</returns>
    RecommendationResponse? GetRecommendations(Guid itemId, Guid? userId, int? limit);

    /// <summary>
    /// Gets recommendations based on a user's recently watched items.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="limit">Optional result limit.</param>
    /// <param name="seedLimit">Optional number of recently watched items to use.</param>
    /// <returns>The recommendation response, or null if the user was not found.</returns>
    RecommendationResponse? GetRecommendationsForRecentlyWatched(Guid userId, int? limit, int? seedLimit);
}
