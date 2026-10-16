using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Plugin.Tmdb.Mapping;

/// <summary>
///   The languages a refresh keeps TMDB's translations and content ratings
///   in. A <c>null</c> set keeps them all.
/// </summary>
/// <param name="SeriesTitles">The languages of the titles of a show, a movie or a collection.</param>
/// <param name="EpisodeTitles">The languages of the titles of an episode, a season or a person.</param>
/// <param name="Overviews">The languages of the overviews and biographies.</param>
/// <param name="ContentRatings">The languages of the countries whose content ratings are kept.</param>
public sealed record TmdbTextLanguages(
    IReadOnlySet<TitleLanguage>? SeriesTitles,
    IReadOnlySet<TitleLanguage>? EpisodeTitles,
    IReadOnlySet<TitleLanguage>? Overviews,
    IReadOnlySet<TitleLanguage>? ContentRatings
)
{
    /// <summary>
    ///   Keeps everything TMDB lists.
    /// </summary>
    public static TmdbTextLanguages All { get; } = new(null, null, null, null);

    /// <summary>
    ///   The languages the configuration and the server's title and overview
    ///   selections keep, as the core's TMDB settings did. Seasons and people
    ///   take the episode title languages, which the server names them by.
    /// </summary>
    /// <param name="configuration">The plugin's configuration.</param>
    /// <param name="textManager">The core's text manager, which builds the selections.</param>
    /// <returns>The languages.</returns>
    public static TmdbTextLanguages From(TmdbConfiguration configuration, IMetadataTextManager textManager)
    {
        // Read for TMDB alone, so every language is a step whatever the source order holds.
        IReadOnlyList<MetadataSource> sources = [MetadataSource.TMDB];
        var series = textManager.GetTitleSelection(MetadataEntityType.Series, sources).Steps.Select(step => step.Language).ToList();
        var episodes = textManager.GetTitleSelection(MetadataEntityType.Episode, sources).Steps.Select(step => step.Language).ToList();
        var overviews = textManager.GetOverviewSelection(sources).Steps.Select(step => step.Language);
        return new(
            TmdbTexts.KeptLanguages(configuration.DownloadAllTitles, series),
            TmdbTexts.KeptLanguages(configuration.DownloadAllTitles, episodes),
            TmdbTexts.KeptLanguages(configuration.DownloadAllOverviews, overviews),
            configuration.DownloadAllContentRatings
                ? null
                : TmdbTexts.KeptLanguages(false, series.Concat(episodes).Append(TitleLanguage.EnglishAmerican))
        );
    }
}
