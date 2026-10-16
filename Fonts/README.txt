Bundled fonts: Sarasa UI J Regular and Sarasa UI K Regular, version 1.0.42.
Both TTFs are unchanged official release files licensed under SIL OFL 1.1.
J uses Japanese regional glyph forms; K uses Korean regional glyph forms.
Both cover modern Hangul, kana and basic CJK ideographs, but not every rare ideograph.

Each file has a corresponding <filename>.LICENSE.txt in ../About/FontLicenses/.
Distribute that directory with these files. The mod MIT license does not cover fonts.
fonts.csv and provenance.csv record payload and official archive SHA-256 hashes.

The runtime reads only selected fonts. The candidate index is embedded from
Source/CustomFonts/Shared/BundledFonts.tsv; changing the shipped set also requires
updating that index and the license records, then running task font_catalog.
