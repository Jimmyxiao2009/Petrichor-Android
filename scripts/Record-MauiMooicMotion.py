"""Record actual MAUI artwork transitions without playing online music."""
import json,re,time,subprocess,xml.etree.ElementTree as ET
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/"artifacts/maui-mooic-port";ADB=r"C:\tmp\PetrichorAndroid\sdk\platform-tools\adb.exe";ACT="app.petrichor.music/crc64d2d221ed02488ed4.MainActivity"
def adb(*args):return subprocess.run([ADB,"-s","emulator-5554",*map(str,args)],capture_output=True,check=True,timeout=40).stdout
def dump():
    adb("shell","uiautomator","dump","/sdcard/maui-motion.xml");return list(ET.fromstring(adb("shell","cat","/sdcard/maui-motion.xml")).iter("node"))
def xy(n):a,b,c,d=map(int,re.findall(r"\d+",n.get("bounds")));return (a+c)//2,(b+d)//2
def tap(label):
    for _ in range(8):
        ns=[n for n in dump() if label in (n.get("text"),n.get("content-desc"))]
        if ns:
            n=next((n for n in ns if n.get("clickable")=="true"),ns[0]);adb("shell","input","tap",*xy(n));time.sleep(.7);return
        time.sleep(.4)
    raise RuntimeError("Missing label: "+label)
adb("shell","am","force-stop","app.petrichor.music");adb("shell","am","start","-n",ACT);time.sleep(2)
ns=dump();more=next(n for n in ns if n.get("content-desc")=="更多操作");adb("shell","input","tap",*xy(more));time.sleep(.5);tap("加入播放队列")
ns=dump();name=next(n for n in ns if n.get("class")=="android.widget.TextView" and int(re.findall(r"\d+",n.get("bounds"))[1])>1850 and n.get("text"));mini=xy(name)
record=subprocess.Popen([ADB,"-s","emulator-5554","shell","screenrecord","--time-limit","10","--bit-rate","5000000","/sdcard/petrichor-maui-motion.mp4"],stdout=subprocess.PIPE,stderr=subprocess.PIPE,creationflags=getattr(subprocess,"CREATE_NO_WINDOW",0))
time.sleep(.7);adb("shell","input","tap",*mini);time.sleep(1.3)
adb("shell","input","tap",540,750);time.sleep(1.5)
adb("shell","input","tap",985,250);time.sleep(1.2)
adb("shell","input","tap",55,200);time.sleep(1.3)
record.wait(timeout=20);adb("pull","/sdcard/petrichor-maui-motion.mp4",OUT/"petrichor-maui-motion.mp4")
print(OUT/"petrichor-maui-motion.mp4")
