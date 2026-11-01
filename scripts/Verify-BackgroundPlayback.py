"""Two-minute background/screen-off check using public network audio."""
from pathlib import Path
import json,time
ROOT=Path(__file__).resolve().parents[1]
ns={"__file__":str(ROOT/"scripts/Verify-PlaybackFixes.py")}
exec((ROOT/"scripts/Verify-PlaybackFixes.py").read_text(encoding="utf-8").split("original=")[0],ns)
adb,tap,wait,media,state,playing,capture=[ns[k] for k in ("adb","tap","wait","media","state","playing","capture")]
PKG=ns["PKG"];OUT=ns["OUT"]
name="Viva La Vida——Coldplay"
samples=[]
def cpu_ticks(pid):
    value=adb("shell","cat","/proc/"+pid+"/stat").decode().rsplit(")",1)[1].split()
    return int(value[11])+int(value[12])
def frames():
    text=adb("shell","dumpsys","gfxinfo",PKG).decode("utf-8","replace")
    import re
    m=re.search(r"Total frames rendered:\s*(\d+)",text)
    return int(m.group(1)) if m else None
try:
    adb("shell","cmd","statusbar","collapse")
    adb("shell","input","keyevent",224);adb("shell","wm","dismiss-keyguard")
    adb("shell","am","start","-n",PKG+"/crc64d2d221ed02488ed4.MainActivity")
    wait("主页");tap("我的");wait("最近播放");tap("最近播放");wait(name);tap(name);playing(name)
    pid=adb("shell","pidof",PKG).decode().strip().split()[0]
    adb("shell","input","keyevent",3)
    adb("shell","input","keyevent",223)
    time.sleep(1)
    initial=cpu_ticks(pid);initial_frames=frames();started=time.monotonic()
    for _ in range(6):
        time.sleep(20)
        text=media()
        if name not in text or state(text)[0]!=3:raise AssertionError("Background playback stopped: "+text)
        sample={"seconds":round(time.monotonic()-started,2),"appCpuTicks":cpu_ticks(pid)-initial,"appFrames":frames(),"playing":True}
        samples.append(sample);print("PASS background network playback at "+str(sample["seconds"])+" s",flush=True)
    adb("shell","input","keyevent",224);adb("shell","wm","dismiss-keyguard")
    adb("shell","am","start","-n",PKG+"/crc64d2d221ed02488ed4.MainActivity")
    adb("shell","input","keyevent",127);time.sleep(.5)
    capture("after-two-minute-background")
    logs=adb("logcat","-d","--pid="+pid,"-v","threadtime").decode("utf-8","replace")
    (OUT/"background-logcat.txt").write_text(logs,encoding="utf-8")
finally:
    (OUT/"background-samples.json").write_text(json.dumps({"initialFrames":locals().get("initial_frames"),"samples":samples},ensure_ascii=False,indent=2),encoding="utf-8")
    adb("shell","input","keyevent",224);adb("shell","wm","dismiss-keyguard")
    adb("shell","input","keyevent",127)