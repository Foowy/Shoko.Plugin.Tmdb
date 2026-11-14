using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Plugin.Tmdb.Mapping;
using TMDbLib.Objects.General;
using Xunit;

namespace Shoko.Plugin.Tmdb.Tests;

/// <summary>
///   Which of TMDB's videos are kept as trailers, and how they reach the
///   stores as resources of shows, episodes and movies.
/// </summary>
public sealed class TmdbTrailerTests : IDisposable
{
    private static readonly MetadataGuid _show = TmdbIds.Series(1001);

    private static readonly MetadataGuid _movie = TmdbIds.Movie(7001);

    private readonly TmdbServiceHarness _harness = new();

    public void Dispose()
        => _harness.Dispose();

    #region Selection

    [Fact]
    public void TheLanguageOrderIsThePreferredOnesThenEnglishThenTheOriginal()
    {
        // A transcription stands for the language it transcribes, and each language comes once.
        Assert.Equal(
            ["de", "ja", "en", "ko"],
            TmdbTrailers.LanguageOrder([TitleLanguage.German, TitleLanguage.Romaji, TitleLanguage.Japanese, TitleLanguage.EnglishAmerican], "ko")
        );
        Assert.Equal(["en"], TmdbTrailers.LanguageOrder([], null));
        Assert.Equal(["en", "ja"], TmdbTrailers.LanguageOrder(null, " JA "));
    }

    [Fact]
    public void TrailersAreRankedByLanguageThenTypeThenAge()
    {
        var trailers = TmdbTrailers.Select(
            [
                Video("fr-trailer", "fr", "Trailer", new(2024, 5, 1)),
                Video("ja-trailer", "ja", "Trailer", new(2024, 4, 1)),
                Video("en-teaser", "en", "Teaser", new(2024, 6, 1)),
                Video("en-old", "en", "Trailer", new(2023, 1, 1)),
                Video("en-new", "en", "Trailer", new(2024, 1, 1)),
                Video("de-teaser", "de", "Teaser", new(2022, 1, 1)),
            ],
            ["de", "en", "ja"]
        );

        // German first though it is only a teaser, then English trailers newest first; the cap leaves the rest out.
        Assert.Equal(["de-teaser", "en-new", "en-old"], trailers.Select(trailer => trailer.ID));
    }

    [Fact]
    public void ALanguageOutsideTheOrderComesLast()
    {
        var trailers = TmdbTrailers.Select(
            [
                Video("none", null, "Trailer", new(2024, 9, 1)),
                Video("fr", "fr", "Trailer", new(2024, 8, 1)),
                Video("ja", "ja", "Teaser", new(2020, 1, 1)),
            ],
            ["en", "ja"]
        );

        Assert.Equal(["ja", "none", "fr"], trailers.Select(trailer => trailer.ID));
    }

    [Fact]
    public void OnlyOfficialTrailersAndTeasersOnYouTubeAreKept()
    {
        var trailers = TmdbTrailers.Select(
            [
                Video("unofficial", "en", "Trailer", new(2024, 1, 1), official: false),
                Video("vimeo", "en", "Trailer", new(2024, 1, 1), site: "Vimeo"),
                Video("clip", "en", "Clip", new(2024, 1, 1)),
                Video("featurette", "en", "Featurette", new(2024, 1, 1)),
                Video("", "en", "Trailer", new(2024, 1, 1)),
                Video("kept", "en", "teaser", new(2024, 1, 1), site: "youtube"),
                Video("kept", "en", "Trailer", new(2023, 1, 1)),
            ],
            ["en"]
        );

        // The same video listed twice is kept once.
        Assert.Equal(["kept"], trailers.Select(trailer => trailer.ID));
    }

    [Fact]
    public void ATrailerIsAYouTubeLinkWithItsTitleAndLanguage()
    {
        var trailer = TmdbTrailers.ToResource(Video("abc_DEF-123", "JA", "Trailer", new(2024, 1, 1), name: " 本予告 "));

        Assert.Equal(ResourceType.Trailer, trailer.Type);
        Assert.Equal(("YouTube", "本予告"), (trailer.Name, trailer.Title));
        Assert.Equal("https://www.youtube.com/watch?v=abc_DEF-123", trailer.Url);
        Assert.Equal("abc_DEF-123", trailer.ID);
        Assert.Equal("ja", trailer.LanguageCode);

        // A video without a title or language has none, and is in no language.
        var bare = TmdbTrailers.ToResource(Video("xyz", "", "Teaser", new(2024, 1, 1), name: ""));
        Assert.Equal(("YouTube", null, null), (bare.Name, bare.Title, bare.LanguageCode));
    }

    #endregion

    #region Refreshes

    [Fact]
    public async Task AShowStoresItsTrailersFromTheRecordedVideos()
    {
        _harness.RouteShow();

        await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken);

        // The videos ride along on the show's own request.
        Assert.Contains(_harness.Routes.Requests, request => request.AbsolutePath.EndsWith("/tv/1001", StringComparison.Ordinal) && AppendsVideos(request));
        var series = _harness.StoreData.Series[_show];
        Assert.Equal(
            [
                ("Official Trailer", "https://www.youtube.com/watch?v=en-trailer-1", "en-trailer-1", "en"),
                ("Announcement Trailer", "https://www.youtube.com/watch?v=en-trailer-0", "en-trailer-0", "en"),
                ("Teaser", "https://www.youtube.com/watch?v=en-teaser-1", "en-teaser-1", "en"),
            ],
            series.Resources.Where(resource => resource.Type is ResourceType.Trailer).Select(resource => (resource.Title, resource.Url, resource.ID, resource.LanguageCode))
        );

        // An episode fetched on its own keeps its own, and one with none has none.
        Assert.Contains(_harness.Routes.Requests, request => request.AbsolutePath.EndsWith("/season/1/episode/1", StringComparison.Ordinal) && AppendsVideos(request));
        var episode = series.Episodes.Single(episode => episode.ID == TmdbIds.Episode(3001));
        Assert.Equal(["ep-1-preview"], episode.Resources.Select(resource => resource.ID));
        Assert.Empty(series.Episodes.Single(episode => episode.ID == TmdbIds.Episode(3002)).Resources);
    }

    [Fact]
    public async Task TheServersLanguagesComeFirst()
    {
        _harness.RouteShow();
        _harness.StoreData.LanguageOrder.Clear();
        _harness.StoreData.LanguageOrder.Add(TitleLanguage.Japanese);

        await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["ja-pv-1", "en-trailer-1", "en-trailer-0"],
            _harness.StoreData.Series[_show].Resources.Where(resource => resource.Type is ResourceType.Trailer).Select(resource => resource.ID)
        );
    }

    [Fact]
    public async Task AMovieStoresItsTrailers()
    {
        _harness.RouteMovie();

        await _harness.Refresh.RefreshMovie(7001, new(), TestContext.Current.CancellationToken);

        Assert.True(AppendsVideos(_harness.Routes.Requests.First(request => request.AbsolutePath.EndsWith("/movie/7001", StringComparison.Ordinal))));
        Assert.Equal(
            [("movie-en-teaser", "en"), ("movie-ja-trailer", "ja")],
            _harness.StoreData.Movies[_movie].Resources.Select(resource => (resource.ID, resource.LanguageCode))
        );
    }

    [Fact]
    public async Task AQuickRefreshKeepsTheEpisodesTrailers()
    {
        _harness.RouteShow();
        await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken);

        await _harness.Refresh.RefreshShow(1001, new() { QuickRefresh = true }, TestContext.Current.CancellationToken);

        var episode = _harness.StoreData.Series[_show].Episodes.Single(episode => episode.ID == TmdbIds.Episode(3001));
        Assert.Equal(["ep-1-preview"], episode.Resources.Select(resource => resource.ID));
    }

    [Fact]
    public async Task WithTheSettingOffNoVideosAreAskedForOrStored()
    {
        _harness.RouteShow().RouteMovie();
        _harness.Configuration.StoreTrailerLinks = false;

        await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken);
        await _harness.Refresh.RefreshMovie(7001, new(), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(_harness.Routes.Requests, AppendsVideos);
        var series = _harness.StoreData.Series[_show];
        Assert.DoesNotContain(series.Resources, resource => resource.Type is ResourceType.Trailer);
        Assert.All(series.Episodes, episode => Assert.Empty(episode.Resources));
        Assert.Empty(_harness.StoreData.Movies[_movie].Resources);
    }

    [Fact]
    public async Task TurningTheSettingOffDropsTheStoredTrailersAtTheNextRefresh()
    {
        _harness.RouteShow().RouteMovie();
        await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken);
        await _harness.Refresh.RefreshMovie(7001, new(), TestContext.Current.CancellationToken);
        _harness.Configuration.StoreTrailerLinks = false;
        _harness.Routes.Json("movie/7001/changes", """{"changes":[]}""");

        // A quick refresh fetches no episode, and drops the episodes' trailers all the same.
        await _harness.Refresh.RefreshShow(1001, new() { QuickRefresh = true }, TestContext.Current.CancellationToken);
        // A movie TMDB did not change is fetched again while it holds trailers.
        Assert.True(await _harness.Refresh.RefreshMovie(7001, new() { LastRefreshedAt = DateTime.UtcNow.AddHours(-1) }, TestContext.Current.CancellationToken));

        var series = _harness.StoreData.Series[_show];
        Assert.DoesNotContain(series.Resources, resource => resource.Type is ResourceType.Trailer);
        Assert.All(series.Episodes, episode => Assert.Empty(episode.Resources));
        Assert.Empty(_harness.StoreData.Movies[_movie].Resources);
    }

    #endregion

    private static bool AppendsVideos(Uri request)
        => request.Query.TrimStart('?').Split('&').Any(part => part.StartsWith("append_to_response=", StringComparison.Ordinal) && Uri.UnescapeDataString(part).Split('=', ',').Contains("videos"));

    private static Video Video(string key, string? language, string type, DateTime published, bool official = true, string site = "YouTube", string? name = null)
        => new()
        {
            Id = key,
            Key = key,
            Iso_639_1 = language!,
            Iso_3166_1 = "US",
            Name = name ?? $"{type} {key}",
            Site = site,
            Type = type,
            Official = official,
            PublishedAt = published,
        };
}
