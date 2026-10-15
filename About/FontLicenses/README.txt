Bundled font licensing records
=============================

Each *.ttf.LICENSE.txt file describes one of the 15 exact font payloads currently
inside rimfonts. fonts.csv maps each payload to its notice, publisher, version,
size and SHA-256. The payload hashes refer to extracted TTF bytes, not the Unity
container. No font binary was modified or replaced in this licensing review.

When packaging these as loose TTF files, retain the corresponding notice files
alongside the fonts and ship this whole directory. Keep the payload bytes intact.
The mod's MIT license does not replace the fonts' own licenses.

OFL notices contain the full license text and relevant copyright notices.
Goyang and HangeulNuri notices record the official publisher-specific terms and
links to the corresponding KOGL terms. HangeulNuri uses Type 3 (attribution,
no derivatives), not a non-commercial license. Goyang's publisher adds a
commercial-redistribution restriction to its Type 1 notice.

Sources were checked on 2026-10-15. Copyright/version strings from shipped font
metadata are retained separately from current website terms. No claim is made
that the existing binary payloads match a newly downloaded upstream release.
