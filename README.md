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

* `Source/CustomFonts/Shared`: settings, selection UI, text metrics, shared Harmony hooks and diagnostics.
* `Source/CustomFonts/Engines/Unity`: existing Unity font discovery, dynamic fonts,
  AssetBundles, resource destruction and Steam Deck patch. Builds target .NET Framework 4.7.2.
* `Source/CustomFonts/Engines/Waylith`: OpenType metadata, file/folder discovery, native
  renderer registration, efficient bundle reading and world-label binding. Builds target .NET 10.

Each configuration compiles only its own engine directory. Unity's supported feature set
is unchanged; the extra file and folder options are available only in 1.7.

Engine selection compares `RimWorldVersion`: versions below 1.7 compile Unity with
`net472`; 1.7 and above compile Waylith with `net10.0`. `Release <version>` sets this
property automatically. `build_version` uses that rule; the engine-specific task aliases
reject a version belonging to the other engine. A future version's engine can be selected
by this rule, but its game API compatibility still needs verification against that install.

The **etc** menu has separate implementations in each engine's `SettingsMenu.cs`.
Unity keeps the Steam Deck host-font patch and its existing resource diagnostics.
Waylith offers file/folder sources, recursive discovery, system-font inclusion and
registration reuse diagnostics. Only the chosen engine's menu and settings fields are
included in the DLL.

## Waylith font sources (1.7)

Open **Mod settings → Custom Fonts → etc** to add a `.ttf`, `.otf` or `.ttc` file,
or a folder. Absolute paths, paths relative to the installed mod folder, environment
variables such as `%LOCALAPPDATA%`, `$HOME` or `${HOME}`, and `~` are supported. Folder recursion and system
font discovery can be enabled separately. Click **Refresh font list / apply search
options** after changing these options or replacing a font file.

System fonts and recursive folder scanning are enabled by default. Additional file/folder
sources start empty. The engine supplies Windows' system and per-user font directories,
macOS' `/System/Library/Fonts`, `/Library/Fonts` and `~/Library/Fonts`, and Linux's
`/usr/share/fonts`, `/usr/local/share/fonts`, `~/.fonts`, `~/.local/share/fonts` and
Flatpak host font directories. Linux discovery also supplements configured
`XDG_DATA_HOME/fonts` and each `XDG_DATA_DIRS` entry's `fonts` directory; relative XDG
values are ignored as required by the [XDG Base Directory specification](https://specifications.freedesktop.org/basedir/0.8/).
Nonstandard directories can be added manually. File/folder sources include font-file
symlinks, but do not descend into directory symlinks, preventing recursive link loops.

Custom files appear as `(File)` choices in both the UI and world font lists. The full
path is kept in the saved selection so equally named files remain distinct. Fonts do
not have to be installed in the OS. Missing or invalid sources are skipped and reported
in settings and the game log. TTC collections currently use their first face, repacked
in memory as a standalone font for Waylith's registration API. The original file is
unchanged. Fonts above 128 MiB and name tables above 1 MiB are rejected.

Waylith owns registered fonts and shared atlases until the process exits. This mod reuses
registrations by file content across sizes, UI/world selections and refreshes. Selecting
new font files can increase engine memory for the rest of that session; removing a path
removes its choices but does not free the renderer's existing registration.

Font discovery reads only the OpenType directory and name table, rather than allocating
each complete file. Bundled choices initially retain names and payload locations only;
selecting one loads its source data once for reuse by UI and world text. The 15 fonts in
the current bundle contain 100,663,608 bytes (about 96 MiB) of source data, which is no
longer retained upfront as separate font arrays. Opening the compressed bundle still
requires temporary decompression memory. CJK coverage cache keys are weak references;
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

## Verification

`task test` checks OpenType names, invalid tables, Unicode paths, environment expansion,
explicit files, recursive/non-recursive folders, supplementary Unicode decoding and
empty CJK glyph coverage without starting RimWorld.

The stream tests check that catalog reads stay within an embedded font's bounds and read
less than 1 KiB from a padded 4 MiB fixture. To also verify all 15 actual bundled font
payloads, supply the installed game's AssetsTools.NET reference and the bundle path:

```bash
dotnet run --project Tests/WaylithFonts.Tests/WaylithFonts.Tests.csproj "-p:GameDir=<RimWorld 1.7 folder>" -- "<mod folder>/rimfonts"
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

# Licnese

* MIT License

## Bundled Fonts

* Sarasa UI J
    ```
    Copyright (c) 2015-2022, Renzhi Li (aka. Belleve Invis, belleve@typeof.net).
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
    공공누리 제1유형(출처표시)
    https://www.goyang.go.kr/www/www05/www05_3/www05_3_6/www05_3_6_tab2.jsp
    ```

* hangeulnuri
    ```
    공공누리 3유형(출처표시-변경금지-비영리)
    https://hanfont.hangeul.go.kr/relaxfont/font/hangeulnuri.do
    ```
