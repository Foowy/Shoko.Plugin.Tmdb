using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using TMDbLib.Objects.General;

namespace Shoko.Plugin.Tmdb.Mapping;

/// <summary>
///   Picks the trailers an entry keeps from the videos TMDB lists for it,
///   as links to YouTube.
/// </summary>
/// <remarks>
///   Only official trailers and teasers on YouTube are kept, at most
///   <see cref="MaxPerEntry"/> of them: those in the earliest language of the
///   order first, then trailers before teasers, then the newest first.
/// </remarks>
public static class TmdbTrailers
{
    /// <summary>
    ///   How many trailers an entry keeps at most.
    /// </summary>
    public const int MaxPerEntry = 3;

    /// <summary>
    ///   The site TMDB names for YouTube.
    /// </summary>
    public const string YouTube = "YouTube";

    /// <summary>
    ///   The order to prefer the trailers' languages in: the given
    ///   languages, then English, then the entry's original language, each
    ///   as an ISO 639-1 code, once.
    /// </summary>
    /// <param name="preferred">The languages the server prefers, best first.</param>
    /// <param name="originalLanguageCode">The entry's original language, if known.</param>
    /// <returns>The language codes, best first.</returns>
    public static IReadOnlyList<string> LanguageOrder(IEnumerable<TitleLanguage>? preferred, string? originalLanguageCode)
        => [
            .. (preferred ?? [])
                .Select(language => language.GetSpokenLanguage().GetLanguageAndCountryCode().languageCode)
                .Append("en")
                .Append(TmdbTexts.Clean(originalLanguageCode) ?? string.Empty)
                .Select(code => code.ToLowerInvariant())
                .Where(code => code.Length is 2)
                .Distinct(StringComparer.Ordinal),
        ];

    /// <summary>
    ///   The trailers to keep from the videos TMDB lists, as resources.
    /// </summary>
    /// <param name="videos">The videos, if any were asked for.</param>
    /// <param name="languageOrder">The language codes to prefer, best first; any other language comes after them.</param>
    /// <returns>The trailers, best first, or none.</returns>
    public static IReadOnlyList<Resource> Select(IEnumerable<Video>? videos, IReadOnlyList<string> languageOrder)
    {
        ArgumentNullException.ThrowIfNull(languageOrder);

        return
        [
            .. (videos ?? [])
                .Where(video => video is not null && video.Official && TypeRank(video.Type) is not null && TmdbTexts.Clean(video.Key) is not null)
                .Where(video => string.Equals(TmdbTexts.Clean(video.Site), YouTube, StringComparison.OrdinalIgnoreCase))
                .DistinctBy(video => TmdbTexts.Clean(video.Key)!, StringComparer.Ordinal)
                .OrderBy(video => LanguageRank(video, languageOrder))
                .ThenBy(video => TypeRank(video.Type))
                .ThenByDescending(video => video.PublishedAt)
                .Take(MaxPerEntry)
                .Select(ToResource),
        ];
    }

    /// <summary>
    ///   A YouTube video, as a trailer resource.
    /// </summary>
    /// <param name="video">The video.</param>
    /// <returns>The resource.</returns>
    public static Resource ToResource(Video video)
    {
        ArgumentNullException.ThrowIfNull(video);

        var key = TmdbTexts.Clean(video.Key) ?? string.Empty;
        return new()
        {
            Type = ResourceType.Trailer,
            Name = YouTube,
            Title = TmdbTexts.Clean(video.Name),
            Url = $"https://www.youtube.com/watch?v={Uri.EscapeDataString(key)}",
            ID = key,
            LanguageCode = LanguageOf(video),
        };
    }

    /// <summary>
    ///   Whether a stored resource is a trailer this plugin keeps, so a
    ///   refresh that fetches no videos can carry it over.
    /// </summary>
    /// <param name="resource">The resource.</param>
    /// <returns><c>true</c> for a YouTube trailer.</returns>
    public static bool IsTrailer(Resource resource)
        => resource is { Type: ResourceType.Trailer } && resource.Url.StartsWith("https://www.youtube.com/watch?v=", StringComparison.Ordinal);

    private static int LanguageRank(Video video, IReadOnlyList<string> languageOrder)
    {
        var code = LanguageOf(video);
        for (var index = 0; code is not null && index < languageOrder.Count; index++)
        {
            if (string.Equals(languageOrder[index], code, StringComparison.Ordinal))
                return index;
        }

        return languageOrder.Count;
    }

    private static int? TypeRank(string? type)
        => TmdbTexts.Clean(type)?.ToLowerInvariant() switch
        {
            "trailer" => 0,
            "teaser" => 1,
            _ => null,
        };

    private static string? LanguageOf(Video video)
        => TmdbTexts.Clean(video.Iso_639_1)?.ToLowerInvariant() is { Length: 2 } code ? code : null;
}
