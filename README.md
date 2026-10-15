# Custom-Fonts

* this fork: [ZCube/rw-customfonts](https://github.com/ZCube/rw-customfonts)
* fork from [nmkj-io/rw-customfonts](https://github.com/nmkj-io/rw-customfonts)
* Steam Workshop: [Custom Fonts - Forked](https://steamcommunity.com/sharedfiles/filedetails/?id=3231727915)

# Build

Requires Git LFS, .NET SDK (10 for Waylith), task, curl and 7zip. Copy `.env.sample` to
`.env` and configure the installed game paths. Unity builds need the corresponding
1.4, 1.5 or 1.6 installation. Waylith reads assemblies from the 1.7 game root.

DLLs and TTF, TTC, OTF, OTC, WOFF, WOFF2 and EOT font files use Git LFS, including
uppercase extensions. Run `git lfs pull` before building or packaging; release ZIPs
and local mod exports contain the actual binaries.

```bash
git lfs pull
task prepare
task build
# Or build only the installed engine/version:
task build_waylith
task build_unity VERSION=1.6 STEAM_APPS="/path/to/1.6/steamapps"
task build_version VERSION=1.7 STEAM_APPS="/path/to/current/steamapps"
task test
```

Set `DOTNET` in `.env` or on the task command line to select a particular SDK executable.
The 1.7 build references Harmony's `1.7/Assemblies/0Harmony.dll`; 1.6 uses its `1.6`
folder, with a fallback to `Current` for older Harmony downloads.

## Local deployment and Steam Workshop upload

Run the deployment tasks in this order from the repository root:

```bash
task build
task export
task release
```

1. `task build` builds the 1.4, 1.5, 1.6 and 1.7 assemblies using their configured
   installations. Run `task test` to validate the shared font code and license records.
2. `task export` copies the current built assemblies, font files, metadata and notices
   into the local RimWorld mod folder. Close RimWorld before replacing loaded DLLs.
3. `task release` packages committed runtime files, creates the ZIP checksum, and
   installs that package into the same local mod folder. Commit the validated source,
   documentation and rebuilt DLLs before this step; release requires a clean tracked
   tree and uses `HEAD`, so uncommitted changes are not included.

`STEAM_APPS` selects the deployment installation; `STEAM_APPS_1_4` through
`STEAM_APPS_1_7` select build references. Export and release use the same target:

* Windows/Linux and macOS 1.7 (Waylith): `<STEAM_APPS>/common/RimWorld/Mods/rw-customfonts`
* macOS 1.4–1.6 (Unity): `<STEAM_APPS>/common/RimWorld/RimWorldMac.app/Mods/rw-customfonts`

On macOS, export and release detect the 1.7 executable at
`RimWorld.app/Contents/MacOS/RimWorld.dll` to select the correct mod directory.
After deploying a new local mod, restart RimWorld to refresh its mod list.

To target another installation, pass the same `STEAM_APPS=...` to both `task export`
and `task release`. The deployed folder contains all four game-version assemblies.

After release completes, open RimWorld and use its Workshop upload/update function
for the **local mod in that Mods folder**. The existing Workshop ID is retained in
`About/PublishedFileId.txt`. Upload from the deployed local mod, not from the source
checkout, the ZIP or another staging folder. Task prepares the files locally;
Steam Workshop publication is performed inside RimWorld. Git push is separate.

## Engine implementations

* `Source/CustomFonts/Shared`: system/user roots, file/folder search, OpenType metadata,
  TTC first-face conversion, source catalog, content cache, lazy bundled-file selection,
  settings and UI, text metrics, variant retirement, shared Harmony hooks and diagnostics.
* `Source/CustomFonts/Engines/Unity`: Font creation through an imported Unity bundle,
  native validation and resource destruction. Builds target .NET Framework 4.7.2.
* `Source/CustomFonts/Engines/Waylith`: native renderer registration, file-backed Font creation,
  ownership rules and world-label compatibility patches. Builds target .NET 10.

Each configuration compiles the shared directory and only its own engine directory.
File/folder sources, recursion, system-font inclusion and refresh are available in 1.4–1.7.
Both engines use the same source paths, metadata, selection and cache code. Only creation,
native validation, ownership and rendering compatibility depend on the engine.
`Shared/FontResources.cs` owns unused-variant pruning, bundled-font references and cache
diagnostics for both engines. The adapters release their engine resources after the
same shared reference and deferred-destruction checks.

Engine selection compares `RimWorldVersion`: versions below 1.7 compile Unity with
`net472`; 1.7 and above compile Waylith with `net10.0`. `Release <version>` sets this
property automatically. `build_version` uses that rule; the engine-specific task aliases
reject a version belonging to the other engine. A future version's engine can be selected
by this rule, but its game API compatibility still needs verification against that install.

The **etc** menu shares its source-management implementation. Each engine appends its own
diagnostics: Unity keeps resource diagnostics; Waylith
reports registration reuse. The source settings retain their existing `Waylith*` XML keys
for compatibility, and both engines read/write them, preserving paths when switching versions.

## Font sources (1.4–1.7)

The etc tab contains shared source discovery and developer diagnostics controls across 1.4–1.7.
Backend resource counters remain engine-specific in the log. Font selection portability
requires the same family to exist in the target machine's OS or configured folders;
custom search folders still need to exist there (environment variables or `~` help).

Open **Mod settings → Custom Fonts → etc** to add a `.ttf`, `.otf` or `.ttc` file,
or a folder. Absolute paths, paths relative to the installed mod folder, environment
variables such as `%LOCALAPPDATA%`, `$HOME` or `${HOME}`, and `~` are supported. Folder recursion and system
font discovery can be enabled separately. Click **Refresh font list / apply search
options** after changing these options or replacing a font file.

Saved source lists may be shared across Windows, macOS and Linux. Missing or
inaccessible locations are skipped without stopping other sources. Windows drive/UNC
paths are skipped on Unix, and Unix absolute paths are skipped on Windows, rather
than reinterpreted as local locations. Relative and home paths accept both `/` and
`\` separators. Newly added paths retain their relative, `~` or environment-variable
expression in settings; expansion happens on the current machine, and equivalent
locations are deduplicated. Existing absolute entries remain unchanged.

System fonts and recursive folder scanning are enabled by default. Additional file/folder
sources start empty. Shared discovery supplies Windows' system and per-user font directories,
macOS' `/System/Library/Fonts`, `/Library/Fonts` and `~/Library/Fonts`, and Linux's
`/usr/share/fonts`, `/usr/local/share/fonts`, `~/.fonts`, `~/.local/share/fonts` and
Flatpak host font directories `/run/host/fonts` and `/run/host/user-fonts` (including Steam Deck). Linux discovery also supplements configured
`XDG_DATA_HOME/fonts` and each `XDG_DATA_DIRS` entry's `fonts` directory; relative XDG
values are ignored as required by the [XDG Base Directory specification](https://specifications.freedesktop.org/basedir/0.8/).
Nonstandard directories can be added manually. File/folder sources include font-file
symlinks, but do not descend into directory symlinks, preventing recursive link loops.

System and configured file fonts appear once per OpenType family in the UI, world and
fallback lists. Selections save the family name, without the font file's path. The
current search roots resolve that family on each machine, so the same installed or
configured family remains selectable after moving files or changing OS. A Regular,
Normal, Roman or Book face is preferred; if none exists, the first available face is
used. Equally suitable configured faces precede OS faces, then configured source order
and sorted file paths break ties. Bundled choices retain their explicit family/style
names, which are already independent of filesystem paths. Fonts do not have to be
installed in the OS. Missing or invalid sources are skipped and reported
in settings and the game log. TTC collections currently use their first face, repacked
in memory as a standalone font for both engines.
The original file is
unchanged. Fonts above 128 MiB and name tables above 1 MiB are rejected.

Bundled fonts are distributed as 15 unchanged TTF files in `Fonts/`, with individual
notices and exact hashes in `About/FontLicenses`. The small embedded
`Shared/BundledFonts.tsv` index preserves existing `(Bundled) Family (Style)` settings
and import sizes without reading font payloads. A selected primary or defined fallback
uses the same `FontFiles` reader as OS/user sources. No compressed font archive,
extraction cache or asset-processing package is needed by either engine.

Unity IMGUI requires a dynamic Font resource: a plain `new Font(path)` is only a TMP
data source. `UnityFontBundle` assembles an uncompressed in-memory Unity resource from
a fixed 9,938-byte metadata template plus only the selected font and fallbacks. Unity's
normal dynamic-font/TMP loaders then render it. The template contains no font payloads;
its format and engine constraints are documented in
[`FontTemplate.md`](Source/CustomFonts/Engines/Unity/FontTemplate.md). Waylith registers
the TTF bytes directly. Both paths support Unicode filenames without OS installation.

System and custom file fonts use a content cache with ordered fallback policies.
Unity shares dynamic fonts across UI sizes and world text; Waylith also keys registrations
by their pixel size, sharing the original source buffer across variants. Refresh invalidates path revisions and reuses unchanged
content. Unity unloads unreferenced imports, including dependent materials and atlases;
it keeps sources still bound to either UI or world text. Removed or unreadable selections
render with Default while retaining the saved family for a later refresh or another OS.
Old Unity family-only selections remain valid; old face and `(File)` selections migrate
to family names, even when their former paths no longer exist. Unknown old engine names
can also migrate through a still-configured path. Unity's OS-font APIs
and the UnityPlayer binary patch are no longer used. Flatpak host paths are scanned directly.

Both engines reuse active fonts by source content and ordered fallback policy, with
the registered size included for Waylith. Unity
unloads unused imported bundles and releases owned TMP materials and atlas textures.
Waylith retires unused mod registrations, their locale/style copies and source buffers
before a subsequent native frame. It preserves active UI/world indices and reuses freed
slots, invalidating engine layout and glyph caches when a slot changes. Registered faces
already contain their initial locale's fallbacks, so rendering does not duplicate them
for the same locale. Changing language still resolves the corresponding locale copy.
Game-owned fonts and the engine's shared atlas remain protected. Freed custom fallback
file buffers are released only when no native atlas source borrows them; empty-glyph
exclusion pins are released when the retired source bytes become collectible.
Size variants share their managed source buffer, but each native registration and
its merged fallbacks can still allocate separate resources. Scale changes retire
unused variants; returning to unscaled Default releases the shared buffer cache too.
Waylith world text uses the already merged native face; it does not create a second set
of fallback font registrations for the TMP compatibility facade. Unity retains its real
TMP fallback assets and releases them when the world policy changes.

`-customfonts-memory-verify="<two Hangul font folder>" -quicktest` in an isolated profile
switches UI/world fonts and four fallback policies 96 times, includes file/bundled/Default
fonts, native bold/italic rendering and repeated source refreshes. Four settled snapshots
of the same configuration check that font/asset counts, caches, native sources, atlas
capacity and pinned ranges do not keep growing. Returning to Default with no defined
fallbacks must release every custom import/registration and its cache aliases. Process memory also includes unrelated
game resources and allocator reserves; font resource counters identify the owned allocations.

Font discovery reads only the OpenType directory and name table, rather than allocating
each complete file. The bundled-file index holds metadata only; opening/searching its
list does not load the 100,663,608 bytes of font source data (about 96 MiB). Selecting
a primary/fallback reads that file only. There is no archive decompression or disk
payload cache. Existing obsolete bundle/cache files are never read.

Default startup skips font discovery; file selections scan only their configured sources,
and opening settings discovers the full list. OS/user names are cached locally under
the profile's `Config/CustomFontsCache`, validated by source size and modification time.
Missing/corrupt name caches fall back to the source. Defined fallback files still use
content-addressed local materialization for native loader compatibility; this is a copy
of a selected font, with no archive extraction. Lists and caches are not serialized into
shared mod settings. CJK coverage cache keys are weak references; native glyph-exclusion
ranges stay pinned while the engine may borrow them. Dynamic glyph generation still
requires registered font data to remain available.

World labels use Waylith's TMP renderer even with the Default selection. The 1.7.4977
renderer preloads only Latin-1 into its world-text glyph cache, so this implementation
requests CJK glyphs on demand from the same language-specific fonts as the UI and
refreshes the shared atlas. Supplementary ideographs such as `𠮷` use complete Unicode
scalars, including during world-label UV updates.
The locale-specific 48-pixel bakes are kept active while world meshes draw. Otherwise
ImGui can evict their atlas pixels after layout stops changing, leaving valid geometry
that no longer displays its text. Glyph UV changes also invalidate the captured atlas.

The selected font and the game's current-language fallback take priority. Missing
characters can then use the other Chinese (Simplified/Traditional), Japanese and Korean
fallback faces available on the machine. On Windows, installed supplementary CJK faces
are also included. Coverage still depends on the available fonts. TrueType fonts that
map CJK characters to empty outlines are allowed to fall back; intentional spaces,
combining marks and Hangul fillers are preserved. Font files are not modified.

### Defined fallback fonts

Open a **Tiny, Small, Medium or world font tab**. Search the existing candidate list
and click the candidate's **+** button to add it as a fallback for that tab. The right
pane lists priorities: use up/down to reorder and × to remove. Each UI size and world
text has its own list. With **Uniform UI fonts** enabled, the Medium list also applies
to Tiny and Small; world settings remain independent. **Merge another tab (union)**
appends another tab's list, or all tab lists, while keeping current priority and removing
duplicate names. Merging copies entries; subsequent edits remain independent.
The shared keys are `FallbackFontsTiny`, `FallbackFontsSmall`, `FallbackFontsMedium`
and `FallbackFontsWorld` across 1.4–1.7. Old `FallbackFontNames` settings are copied to
all tabs on migration; an explicitly empty new tab list remains empty.
**Reset to default** also clears the Tiny/Small/Medium/world fallback lists and legacy
fallback settings, applies the original fonts immediately and saves the reset.
The primary face keeps priority; missing characters use defined fallbacks in list order,
then the engine's existing automatic fallbacks. A missing/removed source is skipped and
reported. Identical file contents are deduplicated, and a primary never references itself.

Unity imports only the primary and defined fallback data into a small runtime bundle,
with ordered Font references and separate material/texture dependencies. It does not
require OS installation. TMP world assets use the same ordered fallback asset table.
With defined fallbacks, Default world text also uses TMP; clearing the list restores
Unity's usual Default legacy renderer. Default UI wrappers retain the original font
names before the defined fallbacks. Unity's Default world font does not contain source
font bytes: its existing baked TMP asset is cloned, with borrowed game-owned atlas and
material retained, rather than attempting to recreate it from that source Font.
Defined TMP fallbacks precede any existing engine fallback assets. Waylith prepends the defined sources to a separate
copy of its language/CJK list for each font policy. It does not mutate the engine's
cached locale lists. Fonts with different ordered fallback contents have different
registration identities; matching active policies reuse the same content font.
World glyphs use that same merged source chain.
Only explicitly defined fallback source bytes are loaded in addition to the primary;
opening or searching the candidate list does not load the other bundled fonts. Fallback definitions
therefore have their own memory cost. The distributed TTF bytes remain unchanged.

In an isolated Windows profile, add `-customfonts-fallback-verify="<two Hangul font folder>"`
with `-quicktest` to exercise Arial with private fonts in both priority orders, removal,
Default, bundled HangeulNuri and system Malgun Gothic fallbacks. The test compares actual
glyphs in all three UI sizes, checks mixed-script meshes and actual visible world labels,
and verifies Default and restored text again after 1,200 frames of resource cleanup.
It migrates old face and moved-file selections to family identities and leaves the
private list saved. `-customfonts-fallback-restart-verify=1 -quicktest` verifies those saved
definitions and rendered glyphs, including visible world labels, on the next launch. These checks do not run normally.
The implementation passes all four builds and 149 assertions with the real bundle,
including private fallback payload preservation, duplicate/self exclusion and 16
fallbacks beyond the template's original font count. Windows live checks pass in
1.6.4871 and 1.7.4977 for private, bundled and system definitions, priority changes,
removal and Default UI/world restoration. Unity's check verifies the actual TMP
character uses the first defined fallback asset. Native UI glyph advances match the
defined face; one-pixel horizontal raster differences are allowed in Waylith merges.
Restart checks in both engines also confirm saved order and rendered glyphs. Computer
Use confirms the settings list, live priority changes, three Korean UI previews and
rendered Hangul world labels. Waylith world assets now register their CJK glyph path
when created, so a newly created asset can render before world-view initialization.

The Default world-font test previously checked fallback-table length alone. That was
insufficient: vanilla fallback entries could satisfy it even when rebuilding the Default
TMP asset had failed. The check now renders Hangul through the first configured fallback,
checks live label geometry/textures after delayed cleanup, and checks every visible character in Waylith atlas pixels
and UVs, plus Unity Hangul character identities to detect replacement squares. The removal stage now uses a Hangul-capable primary before clearing all defined
fallbacks. Keeping Arial in that stage could intentionally remove Korean coverage
and display replacement squares in Unity.

The scoped integration check uses `-customfonts-scoped-fallback-verify="<two Hangul font folder>"`
and `-quicktest` in an isolated profile. It verifies different native Hangul glyph
priorities in all UI sizes and world labels, uniform sharing, ordered union, independent
world removal and Default/restored rendering after 1,200 frames. UI-only changes retain
the existing world asset and label objects. Saved independent priorities can be checked
with `-customfonts-scoped-fallback-restart-verify=1 -quicktest`.

Waylith 1.7's input shim ignores `GUIStyle.fontSize` and passes size zero to its
renderer and editing metrics. A custom face imported at 24px consequently made
the info-card search field larger than its 14px Small style, although labels used
the correct size. UI fonts are now registered at each style's actual pixel size,
including the configured scale, so the engine's size-zero drawing and editing metrics
agree with labels without patching input methods. File, bundled and scaled Default
fonts follow the same rule; unchanged variants are reused, and obsolete sizes are retired.
Other mods' input styles that override the size of an existing font independently
still follow the engine's size-zero behavior. Windows Computer Use checks
compare the same Latin/Hangul text in the 1.6 and 1.7 info-card search fields.
`-customfonts-textfield-verify="<Hangul font folder>" -quicktest` checks 36 actual
single-line/multiline input layouts against label layouts for file/bundled/Default
fonts at 12/14/21/24/28/42px. It also checks shared source buffers, registration reuse,
Hangul glyphs and live world labels. Use an isolated profile for this check.
The current Waylith general renderer check performs 20 family/scale switches across
frames so native retirement can run between changes; the separate memory check covers
96 switches and verifies that settled resources stop growing.

UI offsets are relative to the original game styles, including Small labels' -1px
baseline adjustment. Explicit original style sizes take precedence over font import
sizes. Returning to unscaled Default must preserve the original label dimensions and
offsets. Font-slot retirement invalidates only the affected native pointer; changing a
UI fallback does not discard unchanged world glyph caches.

Mod constructors may run on the loading worker. Original UI fonts/styles are captured
on the first StartOfOnGUI, and saved families, sizes and scoped fallbacks are applied
before that UI is drawn. Engine lifecycle hooks remain installed at mod construction.
Font changes inside OnGUI also update cached line heights in the same event. Changes
outside OnGUI defer measurement until the next StartOfOnGUI.

With uniform UI fonts disabled, scaling Medium leaves Tiny/Small sizes and fallbacks
unchanged. Uniform mode intentionally mirrors the scale and fallback policy to all
three sizes, while retaining their original size ratios. Waylith registers primary and
merged fallback sources at each actual UI pixel size. Unity dynamic fonts share source
objects and rasterize both primary and fallback glyphs at the requested GUIStyle size.

Waylith captures its TMP atlas after the renderer's initial native texture upload.
Newly baked glyphs could therefore remain pending when the render snapshot was copied.
The font compatibility adapter flushes those pending updates through the engine's own
renderer before snapshot capture finishes. Integration checks count any uploads still
pending at frame end, including during font switches, instead of checking only settled
glyphs. The upload adapter is skipped if the capture entry point is absent.

`-customfonts-visual-verify="<Hangul font folder>" -quicktest` opens an isolated visual
comparison window with file/bundled/Default fonts, automatic switching, matching size
and offset controls, measured label boxes, fixed-height buttons and input fields.
`-customfonts-visual-fallback=1` starts with an explicit HangeulNuri fallback. Compare
the same controls in 1.6 and 1.7; native Default fallback rendering can differ even
with Custom Fonts disabled. This diagnostic window never opens without the flag.

`-customfonts-size-verify="<folder containing HangeulNuri and MaruBuri>"` checks 21
independent file/bundle/Default scale combinations, including Medium-only 2x/3x changes,
same-event line metrics, unchanged-size glyph dimensions and per-size fallback rendering.
Run in an isolated profile without `-quicktest` to include the normal mod-loading path.
Restart that profile with `-customfonts-size-restart-verify=1` to check the saved 12/14/63px
configuration before the first UI draw and after loading. Both flags open a comparison
window; they never run in normal play.

## Verification

`task test` checks shared catalog behavior (overlapping sources, family deduplication, Regular/source precedence and moved file selections, explicit nested files, missing/invalid sources and removal), OpenType names, invalid tables, Unicode paths, environment expansion,
explicit files, recursive/non-recursive folders, supplementary Unicode decoding and
empty CJK glyph coverage without starting RimWorld.
It also verifies Unity imports preserve exact primary/fallback bytes, deduplicate sources,
keep the template small, use distinct bundle identities and pick up file replacements.

The stream tests check that catalog reads stay within an embedded font's bounds and read
less than 1 KiB from a padded 4 MiB fixture. To also verify all 15 actual bundled font
payloads, supply the loose-font directory (also included by `task test`):

```bash
dotnet run --project Tests/WaylithFonts.Tests/WaylithFonts.Tests.csproj -- "<mod folder>/Fonts"
```

For a live 1.7 integration check, use a **separate save-data folder**, enable Core, Harmony
and this mod in its `Config/ModsConfig.xml`, then launch:

```text
RimWorld.exe -savedatafolder="<test profile>" -quicktest -customfonts-verify="<folder with two different font files>"
```

This opt-in check changes the test profile's font settings. It checks system and bundled
discovery, custom file rendering, actual world-label geometry (including Hangul with
Malgun Gothic/HangeulNuri), switching between files and bundles, 200 font/size changes,
registration reuse on refresh, lazy bundle selection/reuse and restoration of the default fonts.
It also checks the engine's registration count and the world materials, glyph UVs and actual
atlas pixels at each font selection and again after the final selection has settled. Look for
`[Custom Fonts][verify] PASS` or `FAIL` in the game log. The ordinary profile is unaffected
by the save-data override. Waylith may still write `Player.log` to the usual `Logs` folder.

To test a translated game UI, set `Prefs.xml`'s `langFolderName` to the exact installed
language folder name and add the matching `-customfonts-language` argument. Examples:
`Korean (한국어)`, `Japanese (日本語)`, `ChineseSimplified (简体中文)` and
`ChineseTraditional (繁體中文)`. A short value such as `Korean` can silently fall back to
English. The check asserts the active language and translated UI, then tests mixed CJK
samples in all three UI sizes and actual world meshes with custom files, bundles and
Default. Add `-customfonts-supplementary="<font containing U+20000 and U+20BB7>"` to test
supplementary ideographs. After passing, the language test leaves a custom file selected
for visual inspection of the translated menus, settings previews and world map.
Add `-customfonts-collection="<TTC file>"` to check first-face registration and mixed CJK
glyphs in the live UI renderer and world meshes. Steam F12 captures the original game
resolution, which is useful when a scaled desktop automation capture drops thin strokes.

Unity 1.4–1.6 accept the same `-customfonts-verify` option with `RimWorldWin64.exe`.
Use a folder containing two different Hangul fonts and an isolated profile. The check
excludes system fonts, renders all three UI sizes and world labels, performs 20 file
switches, refreshes the catalog and restores Default. It verifies imported bundle
counts settle to one (shared UI/world source) and zero after Default restoration. It then leaves
the first file selected for visual review. TMP can distribute glyphs across atlas
submeshes, so the world check counts geometry from all submeshes.

`task font_catalog` verifies all 15 original payload hashes, selection names and matching
license notices before `task build`. `task benchmark` measures metadata-only loose listing,
selected file reads and OS name caching on this machine. Add `-customfonts-profile=1` to
separate index reads, file reads, native creation and initial font-application timings.
`task export` and `task release` also remove the obsolete font archive and library DLLs
from their target mod folder when upgrading an existing installation. `task package`
requires a clean committed tree and overlays verified, materialized Git LFS fonts into
`custom-fonts.zip`, so players receive TTF bytes rather than LFS pointer files.
The ZIP contains only versioned assemblies, About/Fonts/Languages, LICENSE and README.
It excludes development sources, tests and local settings. `task package` also writes
`custom-fonts.zip.sha256` and is called by `task release`. Follow the build → export →
release sequence above, then upload the deployed local mod from inside RimWorld.

The file-distribution migration passes all four builds and 188 standalone assertions,
including independent UnityFS/SerializedFile inspection, all 15 original hashes and
matching per-file license notices. Windows RimWorld 1.6.4871 and 1.7.4977 each pass
21 independent primary/fallback size combinations and 96 UI/world changes with four
fallback policies; settled resource counts stay stable and Default releases all owned
imports/registrations. Computer Use confirms Korean UI and world labels with loose
bundled primaries/fallbacks, including zoom changes and world-view re-entry.

On this Windows machine, standalone first reads measured 2.3 ms for the metadata-only
15-font index and 1.1 ms for HangeulNuri Bold (1.64 MB). Fresh isolated game profiles
with bundled HangeulNuri Bold and bundled MaruBuri fallbacks measured index/file reads
of 3.5/0.7 ms in 1.6 and 2.5/0.6 ms in 1.7; startup font application was about 224 ms
in each engine. These are font-operation measurements, not total game startup times.
New 1.4/1.5 runtime runs and Linux/macOS execution remain unverified; those versions
build and platform roots have deterministic path tests. Symbolic-link tests are skipped
when Windows denies link creation.

# License

* MIT License

## Bundled Fonts

Each of the 15 shipped font payloads has its own notice in
[`About/FontLicenses`](About/FontLicenses/README.txt), including the copyright embedded
in that file, publisher/source links, applicable terms and an exact payload SHA-256.
[`fonts.csv`](About/FontLicenses/fonts.csv) maps every payload to its notice.
The mod's MIT license does not replace the fonts' licenses.
The additional review in [`REVIEW.txt`](About/FontLicenses/REVIEW.txt) records the
Goyang redistribution restriction, HangeulNuri usage restrictions, and official
archive hash comparisons for five files in [`provenance.csv`](About/FontLicenses/provenance.csv).

* Sarasa UI J
    ```
    Copyright (c) 2015-2024, Renzhi Li and other credited contributors (see the payload notice).
    SIL Open Font License 1.1
    https://github.com/be5invis/Sarasa-Gothic
    ```

* NAVER: Nanum Fonts / MaruBuri / CLOVA handwritten fonts
    ```
    NAVER / NAVER Cultural Foundation and the credited font creators.
    Original per-file copyright statements are retained in About/FontLicenses/.
    SIL Open Font License 1.1
    https://help.naver.com/service/30016/contents/18088?osType=PC&lang=ko
    ```

* GoYangDeogYang B, EB
    ```
    공공누리 제1유형(출처표시). 배포처는 상업적 재배포 및 유료 양도·판매를 별도로 금지합니다.
    https://www.goyang.go.kr/www/www05/www05_3/www05_3_6/www05_3_6_tab2.jsp
    ```

* hangeulnuri
    ```
    공공누리 제3유형(출처표시-변경금지). 배포처는 상업적 사용을 허용하며 글꼴 자체의 유료 판매·대여 및 유해매체 사용은 금지합니다.
    https://hanfont.hangeul.go.kr/relaxfont/font/hangeulnuri.do
    ```
