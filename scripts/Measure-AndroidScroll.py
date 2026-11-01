"""Repeat the same eight gestures on the currently visible 30-result search.

Run after network requests and builds finish; do not include their contention
in a scrolling comparison. This uses Android's own gfxinfo, not a desktop FPS
estimate. Software-emulator figures are relative evidence, not phone FPS.
"""
import argparse
import json
import re
import subprocess
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("stage", choices=["before", "after"])
parser.add_argument("--package", default="app.petrichor.music")
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
out = root / "artifacts/polish"
out.mkdir(parents=True, exist_ok=True)
adb_path = r"C:\tmp\PetrichorAndroid\sdk\platform-tools\adb.exe"

def adb(*command):
    result = subprocess.run([adb_path, "-s", "emulator-5554", *command], capture_output=True, check=True)
    return result.stdout.decode("utf-8", "replace")

adb("shell", "dumpsys", "gfxinfo", args.package, "reset")
for direction in ["up"] * 4 + ["down"] * 4:
    start, end = (1660, 700) if direction == "up" else (700, 1660)
    adb("shell", "input", "swipe", "540", str(start), "540", str(end), "450")
raw = adb("shell", "dumpsys", "gfxinfo", args.package, "framestats")
(out / f"{args.stage}-search-frames.txt").write_text(raw, encoding="utf-8")
gpu=next((line.strip() for line in adb("shell","dumpsys","SurfaceFlinger").splitlines() if "GLES:" in line),"unknown")
report = {"scene": "Coldplay / single tracks / first 30 results", "gestures": "4 up + 4 down, 450 ms, y1660-y700", "device": "Android 15 API35 x86_64 / 1080x2400", "gpu":gpu, "stage": args.stage, "package": args.package}
for label in ["Total frames rendered", "Janky frames", "50th percentile", "90th percentile", "95th percentile", "99th percentile", "Number Slow UI thread"]:
    match = re.search(r"^" + re.escape(label) + r":\s*(.+)$", raw, re.M)
    if match:
        report[label] = match.group(1).strip()
(out / f"{args.stage}-performance.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=True))
