# Custom-Fonts

* this fork: [ZCube/rw-customfonts](https://github.com/ZCube/rw-customfonts)
* fork from [nmkj-io/rw-customfonts](https://github.com/nmkj-io/rw-customfonts)
* Steam Workshop: [Custom Fonts - Forked](https://steamcommunity.com/sharedfiles/filedetails/?id=3231727915)

# Build

Requires .NET SDK (10 for Waylith), task, curl and 7zip. Copy `.env.sample` to
`.env` and configure the installed game paths. Unity builds need the corresponding
1.4, 1.5 or 1.6 installation. Waylith reads assemblies from the 1.7 game root.

```bash
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

## Engine implementations

* `Source/CustomFonts/Shared`: system/user roots, file/folder search, OpenType metadata,
  TTC first-face conversion, source catalog, content cache, lazy bundle discovery/selection,
  settings and UI, text metrics, variant retirement, shared Harmony hooks and diagnostics.
* `Source/CustomFonts/Engines/Unity`: Font creation through an imported Unity bundle,
  native validation and resource destruction. Builds target .NET Framework 4.7.2.
* `Source/CustomFonts/Engines/Waylith`: native renderer registration, bundle Font creation,
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

Unity imports custom font bytes into a small embedded Unity Font bundle template,
then uses the engine's normal dynamic font and TMP loaders. A plain `new Font(path)`
is only a TMP data source, so it cannot render the existing IMGUI UI. This distinction
is described by the [TMP developer](https://discussions.unity.com/t/textmesh-pro-os-font/757769/5).
The byte-based import supports Unicode paths and avoids OS installation or platform-specific
registration. AssetsTools.NET 3.0.4 is restored by NuGet and shipped only with the Unity
assemblies; its MIT notice is in `About/AssetsTools.NET-LICENSE.txt`.
To regenerate the embedded template from `rimfonts`:

```bash
dotnet run --project Tools/UnityFontTemplate -- rimfonts Source/CustomFonts/Engines/Unity/FontTemplate.bundle
```

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
each complete file. Bundled choices initially retain names and payload locations only;
selecting one loads its source data once for reuse by UI and world text. The 15 fonts in
the current bundle contain 100,663,608 bytes (about 96 MiB) of source data, which is no
longer retained upfront as separate font arrays. The original `rimfonts` remains unchanged.
Its data is one LZMA block: seeking directly to a font is impossible, but the shared
reader decodes only through the selected payload's end, discards preceding output and
retains only that font plus the decoder dictionary. Fonts later in the block still
require preceding data to be decoded. No original Unity bundle engine loader is used.
A 998-byte embedded catalog, validated against the bundle's SHA-256, avoids decompressing
the bundle merely to list its fonts. Changed bundles fall back to catalog regeneration.
Default startup skips font discovery; file selections scan only their configured sources,
and opening settings discovers the full list. File names and bundle metadata/payloads
are cached under the profile's `Config/CustomFontsCache`, validated by source size and
modification time. Cached payload checksums are verified; missing/corrupt cache files
fall back to the source. Only selected bundle payloads are written to disk.
CJK coverage cache keys are weak references;
native glyph-exclusion ranges stay pinned while the engine may borrow them. Dynamic
glyph generation still requires registered font data to remain available.

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
therefore have their own memory cost. The original `rimfonts` remains unchanged.

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
It also verifies Unity imports preserve exact source bytes, contain only one populated
font, keep the template small, use distinct bundle identities and pick up file replacements.

The stream tests check that catalog reads stay within an embedded font's bounds and read
less than 1 KiB from a padded 4 MiB fixture. To also verify all 15 actual bundled font
payloads, supply the bundle path:

```bash
dotnet run --project Tests/WaylithFonts.Tests/WaylithFonts.Tests.csproj -- "<mod folder>/rimfonts"
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

Shared-source implementation validated on Windows on 2026-10-14/15: 63 assertions
without the bundle, 80 with all actual bundle payloads, and successful builds for
1.4, 1.5, 1.6 and 1.7. The Unity 1.6 live check passed with uninstalled HangeulNuri
and MaruBuri in a Unicode folder containing spaces, including 20 UI/world switches,
refresh and complete imported-bundle cleanup on Default restoration. Computer Use
confirmed all three Korean UI previews and actual world labels.
The Waylith 1.7 check passed with those files, bundles, 200 font/size switches, mixed
CJK glyphs and registration reuse. Visual review found transparent vertex colors
that the previous geometry/atlas-only check missed. The world color patch keeps
vertices white and uses the engine's material color for fading, preserving curved
geometry. Added render-layer and vertex-alpha assertions reproduced the failure
before the patch and passed after it; Computer Use confirmed Hangul labels after
leaving and re-entering the world view. New 1.4/1.5 live runs and Linux/macOS execution
are not verified by these results.

The subsequent shared-loading refactor adds common Windows/macOS/Linux root discovery,
engine-independent names (including Mac Roman), legacy-family/face/file-key migration,
TTC conversion and content caching. The source-path, name and dynamic-OS-font APIs are
no longer called in either engine. Both use metadata-only bundle catalogs and lazy
selection. Shared tests pass 79 assertions without the real bundle and 96 with it.
The Unity 1.6 and Waylith 1.7 live checks also verify the common catalog, migration of `Malgun Gothic`
to `Malgun Gothic`, one source shared across UI sizes and world text,
and lazy bundle selection. Unity passes 20 file switches and release after Default
restoration; Waylith passes 200 font/scale switches and refresh checks. Computer Use
also confirms rendered Hangul world labels in both engines after this refactor.
All four version builds pass. These results do not establish live 1.4/1.5 or Linux/macOS
compatibility; those game runs remain unverified.

Startup performance work on 2026-10-15 preserves the original bundle and uses the same
managed extraction path in Unity and Waylith. `task benchmark` compares full unpacking
with manifest/range extraction and persistent caches on the current machine;
`task font_catalog` regenerates/verifies the embedded catalog and also runs before
`task build`. Add `-customfonts-profile=1` to log catalog, extraction, native creation
and initial font-application times separately.
On this Windows machine, a fresh standalone process measured full bundle listing at
about 3 seconds and full-unpack extraction at about 3 seconds. The embedded manifest
reduced fresh listing to 97 ms and range extraction of HangeulNuri Bold to 45 ms;
subsequent processes measured 0.2 ms and 4 ms from disk caches. System enumeration
took about 4 ms; name reads were about 22–27 ms with a warm filesystem and 15 ms
from the persistent metadata cache (the first earlier read was 1.15 seconds).
Unity 1.6's Mono runtime was substantially slower at full decompression: the measured
initial font application took 91.2 seconds. With a fresh cache, manifest/range loading
applied the same bundled HangeulNuri Bold font in 526 ms (400 ms catalog, 81 ms
extraction and 38 ms native creation). These are mod font-operation timings, not total
game startup times. Tests pass 124 assertions with the real bundle, including exact
payload hashes for all 15 prefix-decoded fonts against one full decompression.
Unity 1.6's repeated bundled start measured 117 ms, and a fresh Default profile
measured 7.4 ms without catalog reads or cache creation. Waylith 1.7's fresh bundled
start measured 208 ms (127 ms catalog, 72 ms extraction). Both live verification
runs passed after the optimization, including three UI sizes, custom/bundled files,
world text, font/scale switches and refresh. Computer Use confirmed rendered Hangul
world labels in both engines and the Default main menu in 1.6.

Validated on Windows with RimWorld 1.7.4977 rev1248 and Harmony 3 on 2026-10-14:
45 metadata/path/Unicode/coverage assertions and 62 assertions with the actual bundle passed.
Symbolic-link assertions are skipped on this Windows account because link creation requires
additional OS privileges; the existing settings are left unchanged.
Live integration checks in Korean and English cover custom Arial/Malgun Gothic, bundled
HangeulNuri, TTC first-face loading, mixed CJK world meshes, supplementary ideographs,
200 font/size switches and Default restoration. An added atlas-pixel check reproduced the
disappearing world labels before the bake-lifetime fix and passed after it. World labels
were also checked on the game screen. Separate Japanese and Chinese game-language runs
were not repeated for this fix; their glyph samples are included in the mixed CJK checks.
The 1.7 **etc** menu was also tested manually through Computer Use in an isolated Korean
profile on Windows. A Unicode folder with spaces adds both its root and nested fonts
(285 system choices become 287); excluding system fonts leaves two file choices, and
disabling recursion leaves one. An explicit nested TTF supplied through `%TEMP%` is still
discoverable with recursion disabled. Overlapping sources and repeated path additions do
not duplicate choices; an absent file shows an input error and is not saved. Restoring
both search options returns all 287 choices. Bundled choices remain available with system
discovery disabled. Custom Malgun Gothic and bundled HangeulNuri Bold render all three
UI previews and Hangul world labels, including re-entry into the world view.
The personal source, search options and bundled selection persist across a game restart.
Default restoration also renders Korean previews and world labels; the original-resolution
Steam capture confirms intact UI text where the scaled desktop capture loses thin strokes.
Linux/macOS game execution has not been tested; their engine discovery paths and Linux XDG additions
were reviewed, with deterministic path tests running on Windows.
Unity 1.4/1.5/1.6 builds passed. Steam's public 1.6.4871 installation was also built
and deployed with Task, then tested manually through Computer Use in a separate Korean
profile. Default, bundled HangeulNuri Bold and system Malgun Gothic rendered in the UI
and world view; world labels survived re-entry and Default restoration. At 1.5x UI font
scaling, all three previews resized and wrapped correctly. Malgun Gothic's title clipping
at vertical offset 0 was resolved with a +5 offset in the uniform UI settings; offsets
depend on the selected font and scale. Runtime tests for 1.4/1.5 were not repeated.

# License

* MIT License

## Bundled Fonts

Each of the 15 shipped font payloads has its own notice in
[`About/FontLicenses`](About/FontLicenses/README.txt), including the copyright embedded
in that file, publisher/source links, applicable terms and an exact payload SHA-256.
[`fonts.csv`](About/FontLicenses/fonts.csv) maps every payload to its notice.
The mod's MIT license does not replace the fonts' licenses.

* Sarasa UI J
    ```
    Copyright (c) 2015-2024, Renzhi Li and other credited contributors (see the payload notice).
    SIL Open Font License 1.1
    https://github.com/be5invis/Sarasa-Gothic
    ```

* Nanum Fonts
    ```
    Copyright (c) 2010, NAVER Corporation (https://www.navercorp.com/).
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
