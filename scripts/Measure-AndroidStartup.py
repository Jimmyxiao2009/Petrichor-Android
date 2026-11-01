"""Alternate process-cold launches of the isolated 1.0.2 and current app.
The am-start first-frame metric excludes remote-content readiness.
"""
import json
import re
import statistics
import subprocess
import time
from pathlib import Path

out=Path(__file__).resolve().parents[1]/"artifacts/polish"
adb_path=r"C:\tmp\PetrichorAndroid\sdk\platform-tools\adb.exe"
packages={"before":"app.petrichor.baseline","after":"app.petrichor.music"}
def adb(*args):
    return subprocess.run([adb_path,"-s","emulator-5554",*args],capture_output=True,check=True,timeout=60).stdout.decode("utf-8","replace")

results={name:[] for name in packages}
for trial in range(3):
    for name in ["before","after"]:
        for package in packages.values():adb("shell","am","force-stop",package)
        package=packages[name]
        launch=adb("shell","am","start","-W","-n",package+"/crc64d2d221ed02488ed4.MainActivity")
        (out/f"{name}-cold-{trial+1}.txt").write_text(launch,encoding="utf-8")
        match=re.search(r"TotalTime:\s*(\d+)",launch)
        if not match:raise RuntimeError("No first-frame result: "+launch)
        time.sleep(1)
        if not adb("shell","pidof",package).strip():raise RuntimeError("Process exited after launch")
        results[name].append(int(match.group(1)))
gpu=next((line.strip() for line in adb("shell","dumpsys","SurfaceFlinger").splitlines() if "GLES:" in line),"unknown")
report={"metric":"Android am start -W TotalTime, process cold; remote content excluded","device":"Android 15 x86_64","gpu":gpu,"trials_ms":results,"median_ms":{name:statistics.median(values) for name,values in results.items()}}
(out/"startup-comparison.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
print(json.dumps(report))
