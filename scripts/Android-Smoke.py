"""ADB UI inspection for the disposable Petrichor emulator; never logs account inputs."""
import argparse
import json
from pathlib import Path
import re
import subprocess
import shutil
import sys
import time
import xml.etree.ElementTree as ET

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[1]
ADB = Path(r"C:\tmp\PetrichorAndroid\sdk\platform-tools\adb.exe")
OUT = ROOT / "artifacts" / "android-smoke"
OUT.mkdir(parents=True, exist_ok=True)

def adb(*args):
    result = subprocess.run([str(ADB), "-s", "emulator-5554", *map(str,args)], capture_output=True)
    if result.returncode: raise RuntimeError((result.stderr+result.stdout).decode("utf-8", "replace"))
    return result.stdout.decode("utf-8", "replace")

def dump():
    for attempt in range(8):
        remote = f"/sdcard/petrichor-ui-{time.time_ns()}.xml"
        result = adb("shell", "uiautomator", "dump", remote)
        if "dumped to" in result:
            adb("pull",remote,OUT/"window.xml")
            return ET.parse(OUT/"window.xml").getroot()
        time.sleep(.75)
    raise RuntimeError("UI hierarchy did not become ready")

def nodes(root):
    return [dict(n.attrib) for n in root.iter("node") if n.get("text") or n.get("content-desc")]

def click(label,hold=False,last=False):
    root=dump()
    candidates=[n for n in root.iter("node") if label in (n.get("text"),n.get("content-desc"))]
    if last:candidates.reverse()
    if not candidates: raise RuntimeError("Missing UI label "+label)
    node=next((n for n in candidates if n.get("clickable")=="true"),candidates[0])
    x1,y1,x2,y2=map(int,re.findall(r"\d+",node.get("bounds")))
    x,y=(x1+x2)//2,(y1+y2)//2
    if hold: adb("shell","input","swipe",x,y,x,y,900)
    else: adb("shell","input","tap",x,y)

parser=argparse.ArgumentParser()
parser.add_argument("--out",type=Path,default=OUT)
parser.add_argument("--last",action="store_true")
parser.add_argument("action",choices=["dump","tap","long","input","key","capture"])
parser.add_argument("value",nargs="?")
args=parser.parse_args()
OUT=args.out.resolve()
if not OUT.is_relative_to(ROOT):raise ValueError("Screenshot output must stay in this workspace")
OUT.mkdir(parents=True,exist_ok=True)
if args.action=="dump":
    print(json.dumps([{k:n.get(k,"") for k in ("text","content-desc","class","bounds","clickable")} for n in nodes(dump())],ensure_ascii=True))
elif args.action in ("tap","long"):
    click(args.value,args.action=="long",args.last);print("OK")
elif args.action=="input":
    if not args.value.isascii(): raise RuntimeError("Use ASCII public search terms; do not enter account credentials")
    adb("shell","input","text",args.value.replace(" ","%s"));adb("shell","input","keyevent",66);print("OK")
elif args.action=="key": adb("shell","input","keyevent",args.value);print("OK")
elif args.action=="capture":
    if not re.fullmatch(r"[a-zA-Z0-9_-]+",args.value):raise ValueError("Invalid screenshot name")
    adb("shell","screencap","-p","/sdcard/petrichor-capture.png")
    local=Path(r"C:\tmp\PetrichorAndroid")/("capture-"+str(time.time_ns())+".png")
    adb("pull","/sdcard/petrichor-capture.png",local)
    shutil.copyfile(local,OUT/(args.value+".png"));local.unlink()
    print(str(OUT/(args.value+".png")))
