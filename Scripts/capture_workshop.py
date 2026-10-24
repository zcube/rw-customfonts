"""Reproducible English Workshop screenshots (Windows, RimWorld 1.7).

1. python Scripts/capture_workshop.py prepare --game <RimWorld directory>
2. In Codex's computer-use node_repl runtime (with @oai/sky available):
   const {sky} = await import('@oai/sky');
   const capture = await import('file:///D:/Workspace/rw-customfonts/Scripts/capture_workshop.mjs');
   await capture.run({sky, root: 'D:/Workspace/rw-customfonts'});
3. python Scripts/capture_workshop.py finish
4. powershell -NoProfile -File Scripts/cleanup_workshop_capture.ps1

Only an isolated profile is used. The capture module targets one RimWorld window,
checks the recorded geometry, exports real screenshots, and stops on a mismatch.
The Python finish step crops the settings window without resizing, hides the
personal font-directory row where present, and creates a contact sheet and ZIP.
Do not upload the raw frames in .task/. Review Docs/Screenshots/en/ instead.
Close other RimWorld sessions before prepare, and run prepare only once per capture.
The UI step requires the computer-use runtime; this is not a desktop-wide recorder.
Dependencies: Python 3 with Pillow; installed Windows Segoe UI, Arial Bold Italic;
latest mod already installed; Windows display scaling 150% for the calibrated UI.
"""

from pathlib import Path
import argparse
import hashlib
import json
import re
import shutil
import subprocess
import zipfile
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from uuid import uuid4

ROOT = Path(__file__).resolve().parent.parent
WORK = ROOT / '.task' / 'workshop-screenshots'
OUT = ROOT / 'Docs' / 'Screenshots' / 'en'
PACK_ID = 'zcubekr.customfonts.example.abeezee'
MARKER = '.customfonts-capture-fixture'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save_xml(path, root):
    ET.indent(root)
    ET.ElementTree(root).write(path, encoding='utf-8', xml_declaration=True)


def prepare(game):
    game = game.resolve()
    exe = game / 'RimWorld.exe'
    installed = game / 'Mods' / 'rw-customfonts' / '1.7' / 'Assemblies' / 'CustomFonts.dll'
    if not exe.is_file() or not installed.is_file():
        raise SystemExit('Expected a Windows RimWorld 1.7 installation and rw-customfonts mod.')
    if sha(installed) != sha(ROOT / '1.7/Assemblies/CustomFonts.dll'):
        raise SystemExit('Installed DLL differs from the repository build. Update the local mod first.')
    pack = game / 'Mods' / 'CustomFontsScreenshotExample'
    if pack.exists() and not (pack / MARKER).is_file():
        raise SystemExit(f'Refusing to overwrite an existing non-fixture mod: {pack}')
    shutil.copytree(ROOT / 'Examples/ABeeZeeFontPack', pack, dirs_exist_ok=True)
    (pack / MARKER).write_text('rw-customfonts-workshop-capture', encoding='utf-8')
    config = WORK / 'profile' / 'Config'
    config.mkdir(parents=True, exist_ok=True)
    mods = ET.Element('ModsConfigData')
    ET.SubElement(mods, 'version').text = '1.7'
    active = ET.SubElement(mods, 'activeMods')
    for name in ['brrainz.harmony', 'ludeon.rimworld', 'zcubekr.customfonts', PACK_ID]:
        ET.SubElement(active, 'li').text = name
    save_xml(config / 'ModsConfig.xml', mods)
    prefs = ET.Element('PrefsData')
    for key, value in {'langFolderName': 'English', 'screenWidth': '1920', 'screenHeight': '1200',
                       'fullscreen': 'False', 'runInBackground': 'True', 'volumeMaster': '0',
                       'scaleRelativeToSystem': 'True', 'uiScale': '0.75'}.items():
        ET.SubElement(prefs, key).text = value
    save_xml(config / 'Prefs.xml', prefs)
    block = ET.Element('SettingsBlock')
    settings = ET.SubElement(block, 'ModSettings', {'Class': 'CustomFonts.FontSettings'})
    for key, value in {'UniformUIfonts': 'False', 'CurrentUITinyFontName': 'Segoe UI (Regular)',
                       'CurrentUISmallFontName': 'Default', 'CurrentUIFontName': '(Bundled) Sarasa UI J (Regular)',
                       'CurrentWorldFontName': f'(Bundled) [{PACK_ID}] ABeeZee (Italic)',
                       'WaylithIncludeSystemFonts': 'False', 'ShowFontNamePreviews': 'True',
                       'FontNamePreviewSize': '20'}.items():
        ET.SubElement(settings, key).text = value
    sources = ET.SubElement(settings, 'WaylithFontSourcePaths')
    for name in ['segoeui.ttf', 'arialbi.ttf']:
        if not (Path('C:/Windows/Fonts') / name).is_file():
            raise SystemExit(f'The screenshot scenario requires {name}.')
        ET.SubElement(sources, 'li').text = '%WINDIR%/Fonts/' + name
    favorites = ET.SubElement(settings, 'FavoriteFontNames')
    for name in ['(Bundled) Sarasa UI J (Regular)', '(Bundled) Sarasa UI K (Regular)',
                 f'(Bundled) [{PACK_ID}] ABeeZee (Regular)', f'(Bundled) [{PACK_ID}] ABeeZee (Italic)']:
        ET.SubElement(favorites, 'li').text = name
    for target in ['Tiny', 'Small', 'Medium', 'World']:
        ET.SubElement(ET.SubElement(settings, 'FallbackFonts' + target), 'li').text = '(Bundled) Sarasa UI K (Regular)'
    save_xml(config / 'Mod_rw-customfonts_CustomFonts.xml', block)
    (WORK / 'raw').mkdir(exist_ok=True)
    with (WORK / 'game.log').open('w', encoding='utf-8') as log:
        proc = subprocess.Popen([str(exe), '-screen-width', '1920', '-screen-height', '1200',
                                 '-screen-fullscreen', '0', f'-savedatafolder={WORK / "profile"}'],
                                cwd=game, stdout=log, stderr=subprocess.STDOUT)
    (WORK / 'session.json').write_text(json.dumps({'pid': proc.pid, 'game': str(game),
        'profile': str(WORK / 'profile'), 'temporary_pack': str(pack), 'dll_sha256': sha(installed),
        'capture_id': uuid4().hex, 'started_utc': datetime.now(timezone.utc).isoformat()}, indent=2))
    print(f'Screenshot session started: PID {proc.pid}. Wait for the English main menu, then run the capture module.')


def finish():
    from PIL import Image, ImageDraw, ImageFont
    recipe = json.loads((WORK / 'captures.json').read_text(encoding='utf-8'))
    session = json.loads((WORK / 'session.json').read_text())
    expected = ['01-ui-tiny', '02-ui-small', '03-ui-medium', '04-world',
                '05-etc-preview-and-sources', '06-etc-font-packs']
    if [shot['name'] for shot in recipe] != expected or any(
            shot.get('capture_id') != session['capture_id'] for shot in recipe):
        raise ValueError('Incomplete capture or frames from another session. Capture all six views again.')
    installed = Path(session['game']) / 'Mods/rw-customfonts/1.7/Assemblies/CustomFonts.dll'
    if sha(installed) != session['dll_sha256'] or sha(ROOT / '1.7/Assemblies/CustomFonts.dll') != session['dll_sha256']:
        raise ValueError('The mod build changed during capture. Prepare a new screenshot session.')
    OUT.mkdir(parents=True, exist_ok=True)
    font_path = Path('C:/Windows/Fonts/arial.ttf')
    font = ImageFont.truetype(str(font_path), 15)
    records = []
    for shot in recipe:
        raw = Image.open(WORK / 'raw' / (shot['name'] + '.png')).convert('RGB')
        x, y, w, h = shot['crop']
        if x < 0 or y < 0 or x + w > raw.width or y + h > raw.height:
            raise ValueError('Capture crop falls outside its source frame: ' + shot['name'])
        image = raw.crop((x, y, x + w, y + h))
        for point in [(6, 43), (w - 8, 38), (7, h - 12), (w - 8, h - 12)]:
            pixel = image.getpixel(point)
            if max(abs(pixel[i] - (21, 24, 29)[i]) for i in range(3)) > 12:
                raise ValueError('Settings window moved or was covered; refusing to publish: ' + shot['name'])
        for redaction in shot.get('redactions', []):
            box = redaction['box']
            draw = ImageDraw.Draw(image)
            draw.rectangle(box, fill=(21, 25, 29))
            draw.text((box[0] + 2, box[1] + 1), redaction['label'], font=font, fill=(210, 210, 210))
        path = OUT / (shot['name'] + '.png')
        image.save(path, optimize=True)
        records.append({'file': path.name, 'title': shot['title'], 'width': w, 'height': h,
                        'sha256': sha(path), 'redactions': shot.get('redactions', [])})
    cols, cell_w, cell_h = 2, 500, 430
    sheet = Image.new('RGB', (cols * cell_w, ((len(records) + 1) // 2) * cell_h), '#edf0f4')
    draw = ImageDraw.Draw(sheet)
    for i, record in enumerate(records):
        thumb = Image.open(OUT / record['file']).convert('RGB')
        thumb.thumbnail((cell_w - 20, cell_h - 48), Image.Resampling.LANCZOS)
        left, top = (i % cols) * cell_w, (i // cols) * cell_h
        draw.text((left + 12, top + 10), record['title'], font=font, fill='#17202c')
        sheet.paste(thumb, (left + (cell_w - thumb.width) // 2, top + 38))
    sheet.save(OUT / 'contact-sheet.jpg', quality=92)
    log = (WORK / 'game.log').read_text(encoding='utf-8', errors='replace')
    version = re.search(r'RimWorld\s+(1\.7\.[\w. ]+)', log)
    manifest = {'language': 'English', 'screen': [1920, 1200], 'ui_scale': 0.75,
                'capture_id': session['capture_id'], 'started_utc': session['started_utc'],
                'game_version': version.group(1).strip() if version else '1.7',
                'mod_dll_sha256': session['dll_sha256'], 'resized': False,
                'scripts_sha256': {name: sha(ROOT / 'Scripts' / name) for name in
                                   ['capture_workshop.py', 'capture_workshop.mjs', 'cleanup_workshop_capture.ps1']},
                'method': 'Real RimWorld window captures via computer-use; settings-window crop only.', 'images': records}
    (OUT / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    archive = ROOT / 'Docs' / 'Screenshots' / 'workshop-screenshots-en.zip'
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as zipout:
        for record in records:
            zipout.write(OUT / record['file'], record['file'])
        for name in ['contact-sheet.jpg', 'manifest.json']:
            zipout.write(OUT / name, name)
    print(f'Created {len(records)} English screenshots, contact sheet and {archive.name}.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('stage', choices=['prepare', 'finish'])
    parser.add_argument('--game', type=Path)
    args = parser.parse_args()
    if args.stage == 'prepare':
        if args.game is None:
            parser.error('prepare requires --game')
        prepare(args.game)
    else:
        finish()
