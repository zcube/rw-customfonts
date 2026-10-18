#!/usr/bin/env python3
"""Run the font-only example through real RimWorld registration and renderers.

Requires an installed matching Custom Fonts build, Harmony, and a graphical desktop.
Each game run gets an isolated save-data profile; ordinary mod settings are untouched.
"""
import argparse
import json
import plistlib
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time
import xml.etree.ElementTree as ET

PACK_ID = "zcubekr.customfonts.example.abeezee"
MAIN_ID = "zcubekr.customfonts"
REPO = Path(__file__).resolve().parents[1]


def executable(game, version):
    if sys.platform == "darwin":
        app = "RimWorld.app" if tuple(map(int, version.split('.'))) >= (1, 7) else "RimWorldMac.app"
        plist = game / app / "Contents/Info.plist"
        if not plist.exists():
            return None
        with plist.open("rb") as stream:
            name = plistlib.load(stream)["CFBundleExecutable"]
        candidates = [game / app / "Contents/MacOS" / name]
    elif sys.platform == "win32":
        candidates = [game / "RimWorldWin64.exe", game / "RimWorld.exe"]
    else:
        candidates = [game / "RimWorldLinux", game / "RimWorld"]
    return next((p for p in candidates if p.is_file()), None)


def write_mod_config(profile, game, mode):
    active = ["brrainz.harmony", "ludeon.rimworld", MAIN_ID]
    if mode != "disabled":
        active.insert(2 if mode == "before" else 3, PACK_ID)
    config = ET.Element("ModsConfigData")
    versions = [game / "Version.txt", game / "RimWorldMac.app/Version.txt"]
    version_file = next(path for path in versions if path.is_file())
    ET.SubElement(config, "version").text = version_file.read_text().strip().splitlines()[0]
    mods = ET.SubElement(config, "activeMods")
    for package in active:
        ET.SubElement(mods, "li").text = package
    ET.SubElement(config, "knownExpansions")
    folder = profile / "Config"
    folder.mkdir(parents=True, exist_ok=True)
    ET.ElementTree(config).write(folder / "ModsConfig.xml", encoding="utf-8", xml_declaration=True)


def run_case(binary, game, profile, mode, output, timeout, mac_arch):
    write_mod_config(profile, game, mode)
    result = output / (mode + "-result.txt")
    result.unlink(missing_ok=True)
    log = output / (mode + "-stdout.log")
    engine_log = output / (mode + "-engine.log")
    args = [str(binary), "-quicktest", "-screen-width", "1280", "-screen-height", "720", "-logFile", str(engine_log),
            f"-savedatafolder={profile}", f"-customfonts-pack-verify={mode}", f"-customfonts-pack-result={result}"]
    if mac_arch == "x86_64":
        args = ["/usr/bin/arch", "-x86_64"] + args
    print(f"Running {mode}: {profile}", flush=True)
    with log.open("w") as stream:
        proc = subprocess.Popen(args, cwd=game, stdout=stream, stderr=subprocess.STDOUT)
        try:
            deadline = time.monotonic() + timeout
            while time.monotonic() < deadline:
                if result.exists():
                    message = result.read_text().strip()
                    if "[pack-verify] PASS:" not in message:
                        raise RuntimeError(message)
                    logs = log.read_text(errors="replace") + (engine_log.read_text(errors="replace") if engine_log.exists() else "")
                    if mode in ("active", "restart") and "did not load any content" in logs:
                        raise RuntimeError(f"{mode}: a font-only mod was reported as empty; see {log}")
                    print(message, flush=True)
                    return message
                if proc.poll() is not None:
                    raise RuntimeError(f"{mode}: game exited {proc.returncode} without a test result; see {log}")
                time.sleep(1)
            raise TimeoutError(f"{mode}: no test result after {timeout}s; see {log}")
        finally:
            if proc.poll() is None:
                proc.terminate()
                try:
                    proc.wait(timeout=20)
                except subprocess.TimeoutExpired:
                    proc.kill()
                    proc.wait()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-root", type=Path, required=True)
    parser.add_argument("--version", default="1.7")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--timeout", type=int, default=240)
    parser.add_argument("--mac-arch", choices=["native", "x86_64"], default="native",
                        help="Use x86_64 via installed Rosetta when Unity/Harmony lacks native Apple Silicon support")
    opts = parser.parse_args()
    if opts.mac_arch != "native" and sys.platform != "darwin":
        parser.error("--mac-arch is only supported on macOS")
    game = opts.game_root.resolve()
    binary = executable(game, opts.version)
    if binary is None:
        parser.error(f"No RimWorld {opts.version} executable under {game}")
    output = opts.output.resolve() if opts.output else Path(tempfile.mkdtemp(prefix="customfonts-pack-test-"))
    if output.exists() and any(output.iterdir()):
        parser.error(f"Evidence directory must be empty to avoid reusing an earlier test profile: {output}")
    output.mkdir(parents=True, exist_ok=True)
    # macOS Unity discovers user mods inside RimWorldMac.app/Mods.
    mods = game / "Mods" if sys.platform != "darwin" or tuple(map(int, opts.version.split('.'))) >= (1, 7) else game / "RimWorldMac.app/Mods"
    mods.mkdir(parents=True, exist_ok=True)
    target = mods / "rw-customfonts-abeezee-example"
    if target.exists() or target.is_symlink():
        parser.error(f"Refusing to replace existing example installation: {target}; move it aside for this isolated test")
    source = REPO / "Examples/ABeeZeeFontPack"
    shutil.copytree(source, target)
    results = {}
    try:
        profile = output / "active-profile"
        for mode in ["active", "restart", "before", "disabled"]:
            results[mode] = run_case(binary, game, profile if mode in ("active", "restart") else output / (mode + "-profile"),
                                     mode, output, opts.timeout, opts.mac_arch)
    finally:
        shutil.rmtree(target)
        (output / "results.json").write_text(json.dumps(results, indent=2) + "\n")
        print(f"Evidence: {output}", flush=True)
    print("PASS: real font-pack registration, rendering, restart, wrong-order and disabled cases", flush=True)


if __name__ == "__main__":
    main()
