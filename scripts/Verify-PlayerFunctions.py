"""Exercise the real release APK, without account writes or online playback."""
import json,re,subprocess,time,sys
from pathlib import Path
import xml.etree.ElementTree as ET
sys.stdout.reconfigure(encoding="utf-8")
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/"artifacts/md3-functions";OUT.mkdir(exist_ok=True)
ADB=r"C:\tmp\PetrichorAndroid\sdk\platform-tools\adb.exe";PKG="app.petrichor.music";ACT=PKG+"/crc64d2d221ed02488ed4.MainActivity"
checks=json.loads((OUT/"checks.json").read_text(encoding="utf-8")) if "--resume" in sys.argv else []
def adb(*args):return subprocess.run([ADB,"-s","emulator-5554",*map(str,args)],capture_output=True,check=True,timeout=50).stdout
def dump():
    for _ in range(8):
        if b"dumped to" in adb("shell","uiautomator","dump","/sdcard/player-functions.xml"):
            return list(ET.fromstring(adb("shell","cat","/sdcard/player-functions.xml")).iter("node"))
    raise RuntimeError("UI did not settle")
def bounds(n):return tuple(map(int,re.findall(r"\d+",n.get("bounds",""))))
def labels(ns):return {n.get("text","") for n in ns}|{n.get("content-desc","") for n in ns}
def center(n):a,b,c,d=bounds(n);return (a+c)//2,(b+d)//2
def tap(label,last=False):
    ns=[n for n in dump() if label in (n.get("text"),n.get("content-desc"))]
    if last:ns.reverse()
    if not ns:raise AssertionError("Missing control: "+label)
    n=next((n for n in ns if n.get("clickable")=="true"),ns[0]);adb("shell","input","tap",*center(n));time.sleep(.45)
def wait(label):
    for _ in range(6):
        ns=dump()
        if label in labels(ns):return ns
        time.sleep(.4)
    raise AssertionError("Missing page label: "+label)
def capture(name):
    ns=dump();(OUT/(name+".png")).write_bytes(adb("exec-out","screencap","-p"));(OUT/(name+".json")).write_text(json.dumps([n.attrib for n in ns],ensure_ascii=False,indent=2),encoding="utf-8");return ns
def check(name,condition=True):
    if not condition:raise AssertionError(name)
    checks.append(name);print("PASS "+name,flush=True)
def swipe(x,y,xx,yy,ms=500):adb("shell","input","swipe",x,y,xx,yy,ms);time.sleep(.4)
def back():adb("shell","input","keyevent",4);time.sleep(.45)
def setting(scope,key):return adb("shell","settings","get",scope,key).decode().strip()
def restore(scope,key,value):
    adb("shell","settings","delete",scope,key) if value=="null" else adb("shell","settings","put",scope,key,value)
old={(s,k):setting(s,k) for s,k in [("system","accelerometer_rotation"),("system","user_rotation"),("global","animator_duration_scale")]}
try:
    if "--resume" not in sys.argv:
        adb("shell","settings","put","system","accelerometer_rotation",0);adb("shell","settings","put","system","user_rotation",0);time.sleep(1.2)
        adb("shell","am","force-stop",PKG);adb("shell","am","start","-n",ACT)
        ns=wait("推荐歌曲")
        for _ in range(8):
            more=next((n for n in ns if n.get("content-desc")=="更多操作" and 400<bounds(n)[1]<1300),None)
            if more is not None:break
            time.sleep(.5);ns=dump()
        if more is None:raise AssertionError("Public cached songs did not load")
        adb("shell","input","tap",*center(more));time.sleep(.5);swipe(540,2200,540,1000);wait("加入播放队列");tap("加入播放队列");ns=wait("播放队列")
        mini=next(n for n in ns if n.get("class")=="android.widget.TextView" and bounds(n)[1]>1800 and n.get("text"));adb("shell","input","tap",*center(mini));wait("点击封面显示歌词");capture("01-player-release")
        swipe(540,800,540,1600);ns=capture("02-functions-release")
        check("Downward cover drag opens all eight existing actions",{"播放功能","收藏到歌单","下载歌曲","评论","播放队列","播放音质","睡眠定时","歌词","更多歌曲操作"}<=labels(ns))
        buttons=[n for n in ns if n.get("class")=="android.widget.Button" and n.get("content-desc") in {"收藏到歌单","下载歌曲","评论","播放队列","播放音质","睡眠定时","歌词","更多歌曲操作","返回播放"}]
        check("Every function button has at least a 48 dp touch target",len(buttons)==10 and all(bounds(n)[2]-bounds(n)[0]>=126 and bounds(n)[3]-bounds(n)[1]>=126 for n in buttons))
        back();check("System Back closes functions before leaving player","点击封面显示歌词" in labels(wait("点击封面显示歌词")))
        swipe(540,800,540,830,600);check("Short drag cancels without opening lyrics or functions","点击封面显示歌词" in labels(wait("点击封面显示歌词")))
        seek=next(n for n in dump() if n.get("class")=="android.widget.SeekBar");x,y=center(seek)
        swipe(x,y,x,y+500);check("Vertical drag starting on seek bar does not switch pages","点击封面显示歌词" in labels(wait("点击封面显示歌词")))
        swipe(240,y,850,y);check("Horizontal seeking does not switch pages","点击封面显示歌词" in labels(wait("点击封面显示歌词")))
        tap("播放选项");wait("播放功能");swipe(540,1600,540,800);check("Reverse upward drag closes functions","点击封面显示歌词" in labels(wait("点击封面显示歌词")))
        tap("播放选项");wait("播放功能");tap("播放音质");wait("无损 · FLAC");ns=capture("03-quality-release");check("Existing quality choices include lossless",{"无损 · FLAC","Hi-Res · FLAC"}<=labels(ns));back();wait("播放功能")
        tap("睡眠定时");wait("让音乐陪你入睡。");ns=capture("04-sleep-release");check("Existing sleep timer dialog remains reachable",any("分钟" in s for s in labels(ns)));back();wait("播放功能")
        tap("播放队列");wait("接下来播放");check("Queue uses existing navigation");back();wait("点击封面显示歌词")
        tap("播放选项");wait("播放功能");tap("评论");wait("发送");capture("05-comments-release");check("Comments use existing Petrichor screen");back();wait("点击封面显示歌词")
        tap("点击封面显示歌词");wait("返回封面");swipe(540,1500,540,700);swipe(540,700,540,1500);ns=capture("06-lyrics-release");check("Lyrics own scrolling and never open functions","返回封面" in labels(ns) and "播放功能" not in labels(ns));tap("返回封面");wait("点击封面显示歌词")
        adb("shell","settings","put","global","animator_duration_scale",0);tap("播放选项");wait("播放功能");tap("返回播放");wait("点击封面显示歌词");check("Disabled system animations still settle to usable state");restore("global","animator_duration_scale",old[("global","animator_duration_scale")])
        adb("shell","settings","put","system","user_rotation",1);time.sleep(1)
        ns=capture("07-landscape-player-release")
        for _ in range(4):
            swipe(1100,750,1100,350);ns=dump()
            control=next((n for n in ns if n.get("content-desc")=="播放或暂停"),None)
            if control is not None and bounds(control)[3]-bounds(control)[1]>=189:break
        ns=capture("08-landscape-controls-release");check("Short-height player scrolls to fully visible playback controls",control is not None and bounds(control)[3]-bounds(control)[1]>=189)
    # The menu is reachable after scrolling, then its child gets first chance to scroll.
    # Return to portrait to open the same panel, then rotate while it is open.
    adb("shell","settings","put","system","user_rotation",0);time.sleep(.6);tap("播放选项");wait("播放功能")
    adb("shell","settings","put","system","user_rotation",1);time.sleep(.6);before=labels(dump())
    for _ in range(6):
        swipe(1200,750,1200,330);ns=dump()
        if "更多歌曲操作" in labels(ns):break
        if "播放功能" not in labels(ns):break
    ns=capture("09-landscape-functions-release");check("Scrollable functions retain page while revealing lower rows","播放功能" in labels(ns) and "更多歌曲操作" in labels(ns) and "更多歌曲操作" not in before)
    # At the lower boundary, the next upward gesture returns to playback.
    swipe(1200,750,1200,330);swipe(1200,750,1200,330);ns=wait("播放选项");check("At scroll boundary reverse gesture returns to player","播放功能" not in labels(ns))
    adb("shell","settings","put","system","user_rotation",0);time.sleep(1)
    for _ in range(3):tap("播放选项");wait("播放功能");back();wait("点击封面显示歌词")
    check("Repeated open and Back preserve input")
    tap("播放选项");wait("播放功能");tap("播放音质");wait("无损 · FLAC");back();wait("播放功能");back();wait("点击封面显示歌词");check("Menu actions still accept touches after repeated reopen")
    capture("10-final-player-release")
except Exception:
    capture("failure-latest")
    raise
finally:
    for (scope,key),value in old.items():restore(scope,key,value)
    (OUT/"checks.json").write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding="utf-8")
