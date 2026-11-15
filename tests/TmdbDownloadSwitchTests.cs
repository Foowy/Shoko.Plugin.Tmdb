using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata;
using Shoko.Plugin.Tmdb.Mapping;
using Xunit;

namespace Shoko.Plugin.Tmdb.Tests;

/// <summary>
///   The switches for what a refresh stores: with cast and crew or networks
///   turned off, TMDB isn't asked for them and the stored ones go at the next
///   refresh.
/// </summary>
public sealed class TmdbDownloadSwitchTests : IDisposable
{
    private static readonly MetadataGuid _show = TmdbIds.Series(1001);

    private static readonly MetadataGuid _movie = TmdbIds.Movie(7001);

    private readonly TmdbServiceHarness _harness = new();

    public void Dispose()
        => _harness.Dispose();

    [Fact]
    public async Task TurningCastAndCrewOffAsksForNoCreditsAndDropsTheStoredOnes()
    {
        _harness.RouteShow().RouteMovie();
        await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken);
        await _harness.Refresh.RefreshMovie(7001, new(), TestContext.Current.CancellationToken);
        Assert.NotEmpty(_harness.StoreData.Cast.GetValueOrDefault(_show, []));
        Assert.NotEmpty(_harness.StoreData.Cast.GetValueOrDefault(_movie, []));

        _harness.Configuration.DownloadCastAndCrew = false;
        _harness.Routes.Json("movie/7001/changes", """{"changes":[]}""");
        var before = _harness.Routes.Requests.Count;
        await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken);
        await _harness.Refresh.RefreshMovie(7001, new() { LastRefreshedAt = DateTime.UtcNow }, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(_harness.Routes.Requests.Skip(before), AppendsCredits);
        Assert.All(_harness.StoreData.Cast, entry => Assert.Empty(entry.Value));
        Assert.All(_harness.StoreData.Crew, entry => Assert.Empty(entry.Value));
    }

    [Fact]
    public async Task TurningNetworksOffDropsTheShowsNetworks()
    {
        _harness.RouteShow();
        await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken);
        Assert.NotEmpty(_harness.StoreData.EntryNetworks[_show]);

        _harness.Configuration.DownloadNetworks = false;
        await _harness.Refresh.RefreshShow(1001, new(), TestContext.Current.CancellationToken);

        Assert.Empty(_harness.StoreData.EntryNetworks[_show]);
    }

    private static bool AppendsCredits(Uri request)
        => request.Query.TrimStart('?').Split('&').Any(part => part.StartsWith("append_to_response=", StringComparison.Ordinal) && Uri.UnescapeDataString(part).Split('=', ',').Contains("credits"));
}
