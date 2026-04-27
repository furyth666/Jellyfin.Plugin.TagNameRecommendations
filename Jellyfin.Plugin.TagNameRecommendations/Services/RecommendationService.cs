using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.TagNameRecommendations.Configuration;
using Jellyfin.Plugin.TagNameRecommendations.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.TagNameRecommendations.Services;

/// <inheritdoc />
public class RecommendationService : IRecommendationService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendationService"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="userManager">The user manager.</param>
    public RecommendationService(ILibraryManager libraryManager, IUserManager userManager)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
    }

    /// <inheritdoc />
    public RecommendationResponse? GetRecommendations(Guid itemId, Guid? userId, int? limit)
    {
        var user = userId.HasValue ? _userManager.GetUserById(userId.Value) : null;
        var seed = user is null
            ? _libraryManager.GetItemById(itemId)
            : _libraryManager.GetItemById<BaseItem>(itemId, user);

        if (seed is null)
        {
            return null;
        }

        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var resultLimit = Math.Clamp(limit ?? config.DefaultLimit, 1, 100);
        var maxCandidates = Math.Clamp(config.MaxCandidates, resultLimit, 5000);
        var seedProfile = new SeedProfile(seed, 1);

        var candidates = _libraryManager.GetItemList(new InternalItemsQuery(user)
        {
            Recursive = true,
            Limit = maxCandidates,
            ExcludeItemIds = [itemId],
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series, BaseItemKind.Episode, BaseItemKind.MusicVideo],
            MediaTypes = [MediaType.Video],
            IsVirtualItem = false,
            EnableTotalRecordCount = false
        });

        var scoredItems = candidates
            .Select(item => ScoreItem([seedProfile], item, config))
            .Where(item => item.Score >= config.MinimumScore)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(resultLimit)
            .ToArray();

        return new RecommendationResponse
        {
            SeedItemId = seed.Id,
            SeedItemName = seed.Name,
            Seeds = [new RecommendationSeed { Id = seed.Id, Name = seed.Name }],
            Items = scoredItems
        };
    }

    /// <inheritdoc />
    public RecommendationResponse? GetRecommendationsForRecentlyWatched(Guid userId, int? limit, int? seedLimit)
    {
        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            return null;
        }

        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var resultLimit = Math.Clamp(limit ?? config.DefaultLimit, 1, 100);
        var maxCandidates = Math.Clamp(config.MaxCandidates, resultLimit, 5000);
        var recentLimit = Math.Clamp(seedLimit ?? config.RecentWatchedCount, 1, 50);

        var recentItems = _libraryManager.GetItemList(new InternalItemsQuery(user)
        {
            Recursive = true,
            Limit = recentLimit,
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series, BaseItemKind.Episode, BaseItemKind.MusicVideo],
            MediaTypes = [MediaType.Video],
            IsVirtualItem = false,
            IsPlayed = true,
            OrderBy = [(ItemSortBy.DatePlayed, SortOrder.Descending), (ItemSortBy.SortName, SortOrder.Ascending)],
            EnableTotalRecordCount = false
        });

        if (recentItems.Count == 0)
        {
            return new RecommendationResponse();
        }

        var seedProfiles = recentItems
            .Select((item, index) => new SeedProfile(item, 1 / (1 + index * 0.35)))
            .ToArray();
        var seedIds = recentItems.Select(item => item.Id).ToArray();

        var candidateQuery = new InternalItemsQuery(user)
        {
            Recursive = true,
            Limit = maxCandidates,
            ExcludeItemIds = seedIds,
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series, BaseItemKind.Episode, BaseItemKind.MusicVideo],
            MediaTypes = [MediaType.Video],
            IsVirtualItem = false,
            EnableTotalRecordCount = false
        };

        if (config.ExcludePlayedItems)
        {
            candidateQuery.IsPlayed = false;
        }

        var candidates = _libraryManager.GetItemList(candidateQuery);
        var scoredItems = candidates
            .Select(item => ScoreItem(seedProfiles, item, config))
            .Where(item => item.Score >= config.MinimumScore)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(resultLimit)
            .ToArray();

        return new RecommendationResponse
        {
            SeedItemId = recentItems[0].Id,
            SeedItemName = recentItems[0].Name,
            Seeds = recentItems.Select(item => new RecommendationSeed { Id = item.Id, Name = item.Name }).ToArray(),
            Items = scoredItems
        };
    }

    private static RecommendationCandidate ScoreItem(
        IReadOnlyList<SeedProfile> seeds,
        BaseItem candidate,
        PluginConfiguration config)
    {
        var candidateTags = NormalizeSet(candidate.Tags);
        var candidateNameTokens = TokenizeName(candidate.Name);
        var matchedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matchedNameTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matchedSeedNames = new List<string>();
        var totalWeight = 0.0;
        var totalScore = 0.0;

        foreach (var seed in seeds)
        {
            var tagSimilarity = Jaccard(seed.Tags, candidateTags);
            var nameSimilarity = Jaccard(seed.NameTokens, candidateNameTokens);
            var containsBonus = HasNameContainment(seed.Item.Name, candidate.Name) ? config.NameContainsBonus : 0;
            var seedScore = (config.TagWeight * tagSimilarity) + (config.NameWeight * nameSimilarity) + containsBonus;

            if (seedScore > 0)
            {
                matchedSeedNames.Add(seed.Item.Name);

                foreach (var tag in seed.Tags.Intersect(candidateTags, StringComparer.OrdinalIgnoreCase))
                {
                    matchedTags.Add(tag);
                }

                foreach (var token in seed.NameTokens.Intersect(candidateNameTokens, StringComparer.OrdinalIgnoreCase))
                {
                    matchedNameTokens.Add(token);
                }
            }

            totalScore += seedScore * seed.Weight;
            totalWeight += seed.Weight;
        }

        var weightedScore = totalWeight == 0 ? 0 : totalScore / totalWeight;
        var score = Math.Round(Math.Clamp(weightedScore * 100, 0, 100), 2);

        return new RecommendationCandidate
        {
            Id = candidate.Id,
            Name = candidate.Name,
            Type = candidate.GetType().Name,
            Score = score,
            Tags = candidateTags.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase).ToArray(),
            MatchedTags = matchedTags.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase).ToArray(),
            MatchedNameTokens = matchedNameTokens.OrderBy(token => token, StringComparer.OrdinalIgnoreCase).ToArray(),
            MatchedSeedNames = matchedSeedNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static HashSet<string> NormalizeSet(IEnumerable<string>? values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLower(CultureInfo.InvariantCulture))
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
    }

    private static HashSet<string> TokenizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return [];
        }

        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var token = new List<char>();

        foreach (var character in name)
        {
            if (char.IsLetterOrDigit(character))
            {
                token.Add(char.ToLower(character, CultureInfo.InvariantCulture));
                continue;
            }

            AddToken(tokens, token);
        }

        AddToken(tokens, token);
        return tokens;
    }

    private static void AddToken(ISet<string> tokens, List<char> token)
    {
        if (token.Count >= 2)
        {
            tokens.Add(new string(token.ToArray()));
        }

        token.Clear();
    }

    private static double Jaccard(IReadOnlySet<string> left, IReadOnlySet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var intersection = left.Intersect(right, StringComparer.OrdinalIgnoreCase).Count();
        var union = left.Union(right, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static bool HasNameContainment(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return left.Contains(right, StringComparison.OrdinalIgnoreCase)
            || right.Contains(left, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class SeedProfile
    {
        public SeedProfile(BaseItem item, double weight)
        {
            Item = item;
            Weight = weight;
            Tags = NormalizeSet(item.Tags);
            NameTokens = TokenizeName(item.Name);
        }

        public BaseItem Item { get; }

        public double Weight { get; }

        public IReadOnlySet<string> Tags { get; }

        public IReadOnlySet<string> NameTokens { get; }
    }
}
