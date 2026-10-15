# Dynamic Unity font import

RimWorld 1.4–1.6 IMGUI needs a dynamic `UnityEngine.Font`. A file-path Font alone
only supplies TMP source data. The importer therefore constructs one **uncompressed,
temporary in-memory** UnityFS resource from selected TTF bytes. It never reads or
extracts a distributed font archive and has no asset-processing dependency.

`FontTemplate.bin` is a fixed 9,938-byte template derived from the previous empty
dynamic-font resource, using its Unity 2019.4.40f1 serialized type definitions.
It contains no font data, baked character/kerning tables or extra Font objects.
The engine-specific type tree, builtin shader external reference and unchanged
Font/Texture2D/Material fields stay opaque. This is a writer for this template,
not a parser or editor for arbitrary Unity bundles.

The little-endian template format is:

1. Eight ASCII bytes `CFUFNT01`.
2. SerializedFile metadata prefix and suffix, each as `int32 length + bytes`.
3. Font field fragments: `int32 count`, then `int32 tag + length-prefixed bytes`.
4. Length-prefixed Texture2D and Material field bytes.
5. `int32 count` and `int32` offsets of Material texture path IDs.
6. AssetBundle field fragments, in the same representation as step 3.

Tag zero retains an unchanged field. Font tags 1–6 substitute name, material pointer,
texture pointer, TTF byte array, family-name array and ordered fallback-pointer array.
Bundle tags 7–8 substitute preload pointers and asset-name/container entries.
Strings, arrays and pointers follow the retained type tree's four-byte alignment.

The generated SerializedFile uses version 21, four retained types (Font, Texture2D,
Material, AssetBundle), sorted object IDs, eight-byte object-data alignment and
16-byte data-section alignment. **The AssetBundle root must have path ID 1** for
Unity's native loader; font IDs start at 2. Each selected font has its own texture
and material, and only the primary has ordered references to defined fallbacks.
The outer UnityFS version 7 wrapper has one uncompressed block, an aligned header
and a unique CAB identity per import. No decompressor or disk staging is involved.

`Tests/WaylithFonts.Tests/UnityFontInspection.cs` reads type definitions and object
data independently, rejecting compressed blocks, invalid spans and broken font
dependencies. Native `-customfonts-verify`, size/fallback and memory runners must
also pass after changing this template or writer: a valid serialized structure
alone does not establish that Unity accepts or renders it. Version 1.7 does not
compile or use this template; it registers font bytes with Waylith directly.
