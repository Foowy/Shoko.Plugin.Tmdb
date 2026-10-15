# Shoko TMDB Metadata

The first-party plugin that serves the `tmdb` source: TMDB's shows, seasons,
episodes, movies and collections, and the people, companies and networks
behind them. Official server builds bundle its latest release in
`plugins/Shoko.Plugin.Tmdb/`, where it is enabled by default and cannot be
uninstalled. A server built from source can install it from the Shoko Core
Repository instead (Settings → Plugin Management → Browse).

Report problems in the [issue tracker](https://github.com/ShokoAnime/Shoko.Plugin.Tmdb/issues)
or on [Discord](https://discord.gg/vpeHDsg).

## What it does

`TmdbMetadataProvider` is an ordinary metadata provider. The core runs the
refresh, search, auto-link, image and entity jobs and calls in; the plugin
asks TMDB and writes into the core's stores, and the core reads everything
back from them. Up to four TMDB jobs of each kind run at once.

- **Series and movies.** A refresh stores the show with its seasons and
  episodes, or the movie, with their titles, overviews, credits, genres and
  keywords, content ratings, studios, networks, suggestions and episode
  groups (as global orderings, each group with TMDB's number). A movie names
  its collection by ID and never fetches it.
- **Collections.** Fetched by the core through `RefreshCollection` while the
  provider's `collection` kind is on: when a linked movie naming one is saved
  or refreshed, when a read finds it missing, and in the library refresh.
- **People, companies and networks.** Named on every refresh, credits
  included unless it is quick, and fetched one at a time through the entity
  provider while the provider's `creator`, `studio` and `network` kinds are on,
  for each one the stores lack or hold stale.
- **Genres.** Kept as tags by TMDB's IDs (`tmdb://tag/genre/16`). A genre
  joining two with `&`, such as `Action & Adventure`, is one tag per part
  (`tmdb://tag/genre/10759/1`, `…/2`), so filters match `Action` and
  `Adventure` alike.
- **Images.** Candidates in TMDB's order. The poster, backdrop, profile or
  logo TMDB names on an entry is its pinned default.
- **Linking.** `FindAutoLinks` searches by title, dates and episode count and
  hands back every candidate scored; the core reviews and links them.
  `MatchEpisodes` lines up AniDB's episodes with the show's.
- **Site URLs.** Pages on `www.themoviedb.org` for every kind with one. TMDB
  has no character pages.
- **Generic titles.** Passed on as TMDB gives them, such as "Season N" or
  "Episode N". The core drops them on write and synthesizes its own.

## Configuration

`TmdbConfiguration`, stored in `tmdb.json` in the plugin's configuration
folder and edited through the configuration API like any plugin's.

| Setting | Default | Environment variable |
|---|---|---|
| `UserApiKey`, your own API key, masked on the way out; a change needs a restart | none | `TMDB_API_KEY` |
| `IncrementalChangesWindowDays`, `0` to `14`, `0` turns incremental refreshes off | `1` | `TMDB_CHANGES_WINDOW_DAYS` |
| `RateLimit.MaxRequestsPerWindow`, `1` to `40` | `10` | `TMDB_RATE_LIMIT_MAX_REQUESTS_PER_WINDOW` |
| `RateLimit.WindowDurationMs`, `100` to `10000` | `1000` | `TMDB_RATE_LIMIT_WINDOW_DURATION_MS` |

The switches are all off by default. `DownloadAllTitles`,
`DownloadAllOverviews` and `DownloadAllContentRatings` store every language
TMDB has rather than the ones the server's language orders pick.
`AutoDownloadAlternateOrdering` switches whether a refresh downloads episode
groups. Whether people, companies, networks and collections are fetched is
the provider's `creator`, `studio`, `network` and `collection` kinds, on by
default and set with its other kinds
(`PUT /api/v3/Metadata/Provider/{providerID}`). An upgrade turned off
`creator`, `collection` and `network` where `AutoDownloadCrewAndCast`,
`AutoDownloadCollections` and `AutoDownloadNetworks` were off.
`ConsiderExistingOtherLinks` weighs other anime's links when linking. Last, `AutoSearchShowCandidateCount` and
`AutoSearchMovieCandidateCount` (`1` to `10`, default `5`) set how many
candidates an auto-search scores.

## API key

Official builds carry a key of their own: the release workflow stamps it
into `Constants.ApiKey` from the `TMDB_API` secret. A configured `UserApiKey`
takes precedence. Without either, the provider says it is not configured, and
the core answers its routes with `503`.

## Rate limiting

Every request goes through the plugin's own sliding-window rate limiter and a
bulkhead. While TMDB limits the rate or answers with server errors, the
plugin's `TmdbSuspensionProvider` (named "TMDB") reports a `RateLimited` or
`ServerErrors` suspension with its end, the two kept apart, and the core holds
the provider's jobs back until it runs out.

## Images

The plugin registers the image template URL for `tmdb` on every start:
TMDB's image server in the original size, or the server
`TMDB_IMAGE_CDN_URL` names. A template set in the server's image settings
takes precedence.

## Thumbnail and icon

`source/Assets/thumbnail.svg` and `source/Assets/icon.svg` are embedded resources. The icon
is the plugin's and the `tmdb` source's alike.

## Building

```
dotnet build Shoko.Plugin.Tmdb.slnx -c Release
dotnet test Shoko.Plugin.Tmdb.slnx -c Release
```

It builds against the published `Shoko.Abstractions` package. To build
against a Shoko checkout instead, which also runs the configuration analyzer,
pass `-p:ShokoRepositoryPath=/path/to/Shoko`. A local build has no built-in
TMDB key; set one in the plugin's settings.

Releases are cut by publishing a GitHub release at a `v<version>` tag on
`stable`: the workflow stamps the key, builds and tests the plugin, attaches
the archive and writes the release into the manifest on the `metadata`
branch.
