# Jellyfin Random Movie

A plugin for [Jellyfin](https://jellyfin.org) that adds a 🎲 **dice button** to the web UI. Click it, pick a **genre**, **year** and a **minimum rating**, and the plugin suggests a random movie from **TMDB** that matches your filters — even if the movie is not in your library.

## Features

- Floating dice button injected into the Jellyfin web UI (visible to all users).
- Dialog with three filters:
  - **Genre** (from a list of common TMDB genres)
  - **Year** (any year from 1950 to the current year, or "Any")
  - **Minimum rating** (0–10, e.g. `≥ 7.0`)
- Picks a truly random movie from the TMDB *Discover* API.
- Results show the title, year, TMDB/TMDB rating, a short overview and a direct link to IMDb.
- Works with movies **not** present in your Jellyfin library.
- **Hungarian / English** UI language — switchable right inside the dialog (default configurable by the admin).
- The TMDB API key stays on the server; it is never exposed to the browser.

> **Note on ratings:** the filter uses TMDB's `vote_average` (0–10), because TMDB's public *Discover* API is the reliable, official way to filter by genre + year + rating. The exact IMDB rating is not usable as a server-side filter, but each result links straight to its real IMDb page.

## Requirements

- Jellyfin **10.11.x** (built against `10.11.11`)
- .NET SDK **9.0** (only needed to build from source)
- A free **TMDB API key** — get one at <https://www.themoviedb.org/settings/api>

## Installation

### Add repository (one-click, recommended)

Add the plugin catalog repository, then install from the catalog — no manual download:

1. In Jellyfin open **Dashboard → Plugins → Catalog → ⚙️** (top-left gear).
2. Click **Add Repository** and paste:
   ```
   https://raw.githubusercontent.com/flekyszko/jellyfin-random-movie/main/manifest.json
   ```
3. Click **Save**, go to the **Catalog** tab, find **Random Movie** and click **Install**.
4. Restart the Jellyfin server.

### From a release zip (manual)

1. Download the latest `.zip` from **Releases** and unpack it.
2. Copy the resulting folder (e.g. `RandomMovie_1.0.0.0`) containing
   `Jellyfin.Plugin.RandomMovie.dll`, `meta.json` and `runtimeconfig.json`
   into your Jellyfin **plugins** folder:
   - Windows (manual install): `%ProgramData%\Jellyfin\Server\plugins`
   - Direct install: `%UserProfile%\AppData\Local\jellyfin\plugins`
   - Linux: `/var/lib/jellyfin/plugins`
3. Restart the Jellyfin server.

## Configuration

1. Open **Dashboard → Plugins** and click **Random Movie**.
2. Paste your **TMDB API key** (required).
3. (Optional) Set the default UI language and the max number of random sample pages.
4. Save, then refresh your browser.

The dice button appears in the bottom-right corner of the web UI.

## Usage

1. Click the 🎲 button.
2. Pick a **genre**, a **year** and a **minimum rating** (leave one "Any" to skip a filter).
3. Click **Get Movie**.
4. The plugin returns a random matching film — click the IMDb link to open it.

> The rating filter uses TMDB's `vote_average`. Tip: a mid rating (e.g. `≥ 6`) gives the best variety.

## Building from source

```bash
# On Windows
build.bat

# Or manually
dotnet build -c Release src\Jellyfin.Plugin.RandomMovie\Jellyfin.Plugin.RandomMovie.csproj
```

The output DLL is written to
`src\Jellyfin.Plugin.RandomMovie\bin\Release\net9.0\`, and `build.bat` additionally
packages a ready-to-install zip into `dist\`.

### Project layout

```
src/Jellyfin.Plugin.RandomMovie/
├── Plugin.cs                 # Main plugin class (config page)
├── UiStartupService.cs       # Boot-time script injection into the web UI
├── UiInjector.cs             # Adds the <script> tag to index.html
├── Api/
│   └── RandomMovieController.cs  # GET /RandomMovie/random  + inject.js
├── Services/
│   └── TmdbClient.cs         # TMDB Discover API + random pick
├── Configuration/
│   └── configPage.html       # Admin settings page
│   └── PluginConfiguration.cs
└── Web/
    └── randommovie.js        # Injected button + dialog (HU/EN)
```

### How the button is injected

On server startup the plugin appends a small `<script src="/RandomMovie/inject.js">`
tag to the Jellyfin web client's `index.html` (inside a `<!-- RandomMovie:BEGIN/END -->`
marker, so it is never injected twice). That script drops the floating button and the
dialog into the page.

## Companion: repository manifest

If you host releases, point the Jellyfin plugin catalog at your `manifest.json`
so it can be installed directly from the **Catalog** tab (see
[official plugin repo docs](https://jellyfin.org/docs/general/server/plugins/)). Adjust the
`sourceUrl` and `checksum` in `manifest.json` to your GitHub release URLs.

## License

MIT — see [LICENSE](LICENSE).