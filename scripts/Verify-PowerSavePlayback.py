"""Six minutes of background playback with Android's system Battery Saver on."""
from pathlib import Path
import json,re,subprocess,time
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/"artifacts/playback-fixes/power-save";OUT.mkdir(exist_ok=True)
ns={"__file__":str(ROOT/"scripts/Verify-PlaybackFixes.py")}
exec((ROOT/"scripts/Verify-PlaybackFixes.py").read_text(encoding="utf-8").split("original=")[0],ns)
ns["OUT"]=OUT
adb,tap,wait,media,state,playing,dump,labels,bounds,back,capture=[ns[k] for k in ("adb","tap","wait","media","state","playing","dump","labels","bounds","back","capture")]
PKG=ns["PKG"];PRIVATE="/data/user/0/"+PKG
base_adb=adb
def adb(*args):
    for attempt in range(5):
        try:return base_adb(*args)
        except (subprocess.CalledProcessError,subprocess.TimeoutExpired) as error:
            if attempt==4:raise
            message=getattr(error,"stderr",b"") or b""
            print("ADB read retry "+str(attempt+1)+": "+message.decode("utf-8","replace")[:180],flush=True)
            time.sleep(1)
ns["adb"]=adb
original=adb("shell","cat",PRIVATE+"/files/library/library.json")
(OUT/"library-before.json").write_bytes(original)
library=json.loads(original)
track=next(t for t in library["Recent"] if t["Id"]==2101465016)
library["Recent"]=[track]
fixture=OUT/"library-single-stream.json";fixture.write_text(json.dumps(library,ensure_ascii=False),encoding="utf-8")
old_mode=adb("shell","settings","get","global","low_power").decode().strip()
(OUT/"battery-before.txt").write_bytes(adb("shell","dumpsys","battery"))
samples=[];checks=[]
def cpu_ticks(pid):
    fields=adb("shell","cat","/proc/"+pid+"/stat").decode().rsplit(")",1)[1].split()
    return int(fields[11])+int(fields[12])
def frames():
    raw=adb("shell","dumpsys","gfxinfo",PKG).decode("utf-8","replace")
    m=re.search(r"Total frames rendered:\s*(\d+)",raw)
    return int(m.group(1)) if m else None
try:
    adb("shell","cmd","statusbar","collapse");adb("shell","input","keyevent",224);adb("shell","wm","dismiss-keyguard")
    adb("shell","am","force-stop",PKG);adb("push",fixture,PRIVATE+"/files/library/library.json")
    adb("shell","dumpsys","battery","unplug")
    adb("shell","dumpsys","battery","set","level",30)
    adb("shell","cmd","power","set-mode",1)
    if adb("shell","settings","get","global","low_power").decode().strip()!="1":raise AssertionError("System Battery Saver was not enabled")
    for _ in range(30):
        power=adb("shell","dumpsys","power")
        if b"Battery Saver is currently: ON" in power:break
        time.sleep(.2)
    else:raise AssertionError("Android power service did not enter Battery Saver")
    (OUT/"power-save-on.txt").write_bytes(power)
    adb("shell","am","start","-n",PKG+"/crc64d2d221ed02488ed4.MainActivity")
    wait("主页");tap("我的");wait("最近播放");tap("最近播放");wait(track["Name"]);tap(track["Name"]);playing(track["Name"])
    pid=adb("shell","pidof",PKG).decode().strip().split()[0]
    adb("shell","input","keyevent",3);adb("shell","input","keyevent",223);time.sleep(1)
    started=time.monotonic();initial_ticks=cpu_ticks(pid);initial_frames=frames()
    print("START Android Battery Saver enabled; app backgrounded and screen off; target 360 seconds",flush=True)
    for index in range(18):
        time.sleep(max(0,started+(index+1)*20-time.monotonic()))
        # A normal repeat may briefly prepare its new stream at the four-minute mark.
        text=media()
        if state(text)[0] in (3,6) and track["Name"] in text:
            if state(text)[0]==6:playing(track["Name"])
        else:raise AssertionError("Playback interrupted during system power saver: "+text)
        low_power=adb("shell","settings","get","global","low_power").decode().strip()
        if low_power!="1":raise AssertionError("System Battery Saver was disabled during the test")
        sample={"seconds":round(time.monotonic()-started,2),"playing":True,"systemPowerSaver":True,"cpuTicks":cpu_ticks(pid)-initial_ticks,"appFrames":frames()}
        samples.append(sample)
        (OUT/"samples.json").write_text(json.dumps({"initialFrames":initial_frames,"samples":samples},ensure_ascii=False,indent=2),encoding="utf-8")
        print("PASS Battery Saver + screen off: "+str(sample["seconds"])+" seconds",flush=True)
    adb("shell","input","keyevent",224);adb("shell","wm","dismiss-keyguard")
    adb("shell","am","start","-n",PKG+"/crc64d2d221ed02488ed4.MainActivity")
    adb("shell","input","keyevent",127);time.sleep(.5)
    back();wait("最近播放");tap("设置");wait("主题模式")
    for _ in range(3):
        if any("播放诊断日志" in (n.get("text","")+n.get("content-desc","")) for n in dump()):break
        adb("shell","input","swipe",540,1800,540,800,400);time.sleep(.3)
    tap("播放诊断日志");wait("导出日志")
    nodes=dump()
    if not any("系统省电：已开启" in n.get("text","") for n in nodes):raise AssertionError("App did not report system power saver")
    capture("diagnostics-screen")
    tap("导出日志");time.sleep(1);back()
    cached=adb("shell","ls",PRIVATE+"/cache").decode().splitlines()
    exported=sorted(n.strip() for n in cached if n.startswith("Petrichor-playback-") and n.endswith(".txt"))[-1]
    adb("pull",PRIVATE+"/cache/"+exported,OUT/"exported-diagnostics.txt")
    report=(OUT/"exported-diagnostics.txt").read_text(encoding="utf-8")
    session_report=report.rsplit("app.start",1)[-1]
    heartbeats=[line for line in session_report.splitlines() if "heartbeat " in line and "powerSave=True" in line and "ui=False" in line]
    if len(heartbeats)<10:raise AssertionError("Missing background Battery Saver heartbeats")
    if "https://" in report or "http://" in report:raise AssertionError("Unexpected source URL in diagnostics")
    if "native.error " in report or "failure " in report:raise AssertionError("Playback reported a native/final error")
    completions=[line for line in session_report.splitlines() if "completion " in line and "premature=False" in line]
    if not completions:raise AssertionError("Expected a normal end and repeat during the six-minute run")
    checks=["System Battery Saver stayed enabled for the entire six-minute test","Background/screen-off network playback stayed active in all 18 samples","Normal end of the stream repeated while screen off","Diagnostic page correctly reports the active system power saver","Export creates a readable diagnostic file via Android's share chooser","Export includes at least ten background power-save heartbeats without source URLs"]
    (OUT/"logcat.txt").write_bytes(adb("logcat","-d","--pid="+pid,"-v","threadtime"))
    (OUT/"result.json").write_text(json.dumps({"durationSeconds":samples[-1]["seconds"],"samples":len(samples),"checks":checks,"powerSaveHeartbeats":len(heartbeats),"normalCompletions":len(completions),"initialFrames":initial_frames,"finalFrames":samples[-1]["appFrames"]},ensure_ascii=False,indent=2),encoding="utf-8")
    print("PASS all six-minute Battery Saver and diagnostic export checks",flush=True)
finally:
    adb("shell","input","keyevent",224);adb("shell","wm","dismiss-keyguard")
    adb("shell","input","keyevent",127);adb("shell","cmd","statusbar","collapse")
    adb("shell","am","force-stop",PKG);adb("push",OUT/"library-before.json",PRIVATE+"/files/library/library.json")
    adb("shell","dumpsys","battery","reset")
    adb("shell","cmd","power","set-mode",1 if old_mode=="1" else 0)