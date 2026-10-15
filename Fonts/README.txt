These 15 TTF files retain their original bytes.
Each file has a corresponding <filename>.LICENSE.txt notice in ../About/FontLicenses/.
See that directory's README.txt and fonts.csv for licenses, attribution and SHA-256.
Distribute the notices with these files. The mod MIT license does not cover the fonts.

The runtime reads only selected fonts. The candidate index is embedded from
Source/CustomFonts/Shared/BundledFonts.tsv; changing the shipped set also requires
updating that index and the license records, then running task font_catalog.
