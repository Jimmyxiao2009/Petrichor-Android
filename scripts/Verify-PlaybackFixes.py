"""Exercise the shipped Release on the disposable emulator; no account writes."""
from pathlib import Path
import json,re,subprocess,time,xml.etree.ElementTree as ET
import sys
sys.stdout.reconfigure(encoding="utf-8")
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/"artifacts/playback-fixes"
ADB=r"C:\tmp\PetrichorAndroid\sdk\platform-tools\adb.exe"
PKG="app.petrichor.music"
REMOTE="/data/user/0/"+PKG+"/files"
checks=[]
controls={}
def adb(*args):
    return subprocess.run([ADB,"-s","emulator-5554",*map(str,args)],capture_output=True,check=True,timeout=50).stdout
def dump():
    for _ in range(5):
        out=adb("shell","uiautomator","dump","/sdcard/playback-fixes.xml")
        if b"dumped to" in out:
            return list(ET.fromstring(adb("shell","cat","/sdcard/playback-fixes.xml")).iter("node"))
    raise RuntimeError("UI hierarchy unavailable")
def bounds(n):return tuple(map(int,re.findall(r"\d+",n.get("bounds",""))))
def labels(nodes):return {n.get("text","") for n in nodes}|{n.get("content-desc","") for n in nodes}
def tap(label,last=False):
    if label in controls:
        x,y=controls[label];adb("shell","input","tap",x,y);time.sleep(.35);return
    nodes=[n for n in dump() if label in (n.get("text"),n.get("content-desc"))]
    if last:nodes.reverse()
    if not nodes:raise AssertionError("Missing control "+label)
    n=next((n for n in nodes if n.get("clickable")=="true"),nodes[0]);l,t,r,b=bounds(n)
    adb("shell","input","tap",(l+r)//2,(t+b)//2);time.sleep(.35)
def wait(label):
    for _ in range(12):
        nodes=dump()
        if label in labels(nodes):return nodes
        time.sleep(.3)
    raise AssertionError("Missing "+label)
def back():adb("shell","input","keyevent",4);time.sleep(.35)
def media():return adb("shell","dumpsys","media_session").decode("utf-8","replace")
def state(text=None):
    m=re.search(r"state=PlaybackState \{state=(?:[A-Z_]+\()?(\d+)\)?, position=(\d+)",text or media())
    return tuple(map(int,m.groups())) if m else (-1,0)
def playing(name):
    for _ in range(40):
        text=media()
        if name in text and state(text)[0]==3:return text
        time.sleep(.2)
    raise AssertionError("Did not play "+name+"\n"+media())
def check(name,ok=True):
    if not ok:raise AssertionError(name)
    checks.append(name);print("PASS "+name,flush=True)
def capture(name):
    (OUT/(name+".png")).write_bytes(adb("exec-out","screencap","-p"))
def open_player():
    nodes=dump();mini=next(n for n in nodes if n.get("content-desc")=="播放或暂停")
    l,t,r,b=bounds(mini);adb("shell","input","tap",300,(t+b)//2);wait("点击封面显示歌词")
def seek(fraction):
    n=controls.get("seek")
    if n is None:n=next(n for n in dump() if n.get("class")=="android.widget.SeekBar")
    l,t,r,b=bounds(n);adb("shell","input","tap",round(l+(r-l)*fraction),(t+b)//2);time.sleep(.5)

original=adb("shell","cat",REMOTE+"/library/library.json")
(OUT/"emulator-library-before.json").write_bytes(original)
remote=next(t for t in json.loads(original)["Recent"] if t["Id"]==2101465016)
fixtures=[]
for index,color in enumerate(("#237a56","#2854c4","#af375c")):
    id=-1101-index;name="回归音频 "+chr(65+index)
    file=REMOTE+"/library/qa-playback-"+str(index)+".flac"
    adb("shell","cp",REMOTE+"/qa.flac",file)
    track={"Id":id,"Name":name,"LocalFilePath":file,"DurationMs":90000,"Artists":[{"Name":"播放回归测试"}],"Album":{"Name":"离线回归","CoverUrl":""}}
    fixtures.append(track)
    if index<2:
        image=Image.new("RGB",(640,640),color);d=ImageDraw.Draw(image);d.ellipse((110,110,530,530),outline="white",width=20)
        cover=OUT/("fixture-"+str(index)+".jpg");image.save(cover)
        adb("push",cover,REMOTE+"/library/"+str(id)+".jpg")
data={"Downloads":[],"Local":fixtures,"Recent":[remote,*fixtures]}
fixture=OUT/"emulator-library-fixture.json";fixture.write_text(json.dumps(data,ensure_ascii=False),encoding="utf-8")
try:
    adb("shell","cmd","statusbar","collapse")
    adb("shell","input","keyevent",224);adb("shell","wm","dismiss-keyguard")
    adb("shell","am","force-stop",PKG)
    adb("push",fixture,REMOTE+"/library/library.json")
    adb("shell","am","start","-n",PKG+"/crc64d2d221ed02488ed4.MainActivity")
    wait("主页");tap("我的");wait("本地与下载");tap("本地与下载");wait("本地音乐");tap("本地音乐");wait("回归音频 A")
    tap("回归音频 B");playing("回归音频 B");adb("shell","input","keyevent",127);time.sleep(.5);open_player()
    for n in dump():
        label=n.get("content-desc","")
        if label in ("上一首","下一首"):
            l,t,r,b=bounds(n);controls[label]=((l+r)//2,(t+b)//2)
        if n.get("class")=="android.widget.SeekBar":controls["seek"]=n
    seek(.5);tap("上一首");playing("回归音频 A")
    check("One previous press after seeking selects A instead of restarting B")
    tap("下一首");playing("回归音频 B");check("One next press selects B")
    tap("下一首");playing("回归音频 C")
    time.sleep(.8);note=adb("shell","dumpsys","notification","--noredact").decode("utf-8","replace")
    (OUT/"notification-fallback.txt").write_text(note,encoding="utf-8")
    check("A track without album art publishes a default system bitmap",bool(re.search(r"android\.largeIcon=Icon.*typ=BITMAP",note)))
    tap("上一首");playing("回归音频 B")
    note=adb("shell","dumpsys","notification","--noredact").decode("utf-8","replace")
    (OUT/"notification-B.txt").write_text(note,encoding="utf-8")
    check("The media notification includes a decoded album bitmap",bool(re.search(r"android\.largeIcon=Icon.*typ=BITMAP",note)))
    adb("shell","input","swipe",540,5,540,1800,500);time.sleep(1.2);capture("system-artwork-B");adb("shell","cmd","statusbar","collapse");time.sleep(.6)
    seek(.3);adb("shell","input","keyevent",223);time.sleep(20)
    check("Screen-off mid-song playback stays on B in the playing state","回归音频 B" in media() and state()[0]==3)
    adb("shell","input","keyevent",224);adb("shell","wm","dismiss-keyguard");time.sleep(.8)
    seek(.97);adb("shell","input","keyevent",223);time.sleep(6)
    playing("回归音频 C");check("Normal song end still advances while the screen is off")
    adb("shell","input","keyevent",224);adb("shell","wm","dismiss-keyguard");time.sleep(.5)
    adb("shell","input","keyevent",127)
    back();wait("本地音乐")
    # Record actual open/close transitions rather than inferring smoothness from static screenshots.
    nodes=dump();mini=next(n for n in nodes if n.get("content-desc")=="播放或暂停");l,t,r,b=bounds(mini);y=(t+b)//2
    record=subprocess.Popen([ADB,"-s","emulator-5554","shell","screenrecord","--time-limit","18","--bit-rate","6000000","/sdcard/playback-fixes-motion.mp4"],stdout=subprocess.PIPE,stderr=subprocess.PIPE,creationflags=getattr(subprocess,"CREATE_NO_WINDOW",0))
    time.sleep(.5)
    for _ in range(4):
        adb("shell","input","tap",300,y);time.sleep(.65);adb("shell","input","keyevent",4);time.sleep(.65)
    record.wait(timeout=22);adb("pull","/sdcard/playback-fixes-motion.mp4",OUT/"artwork-motion.mp4")
    wait("本地音乐");check("Four artwork transition roundtrips settle on the original page")
    capture("local-after-roundtrips")
    back();wait("本地与下载")
    nav_record=subprocess.Popen([ADB,"-s","emulator-5554","shell","screenrecord","--time-limit","25","--bit-rate","6000000","/sdcard/playback-fixes-navigation.mp4"],stdout=subprocess.PIPE,stderr=subprocess.PIPE,creationflags=getattr(subprocess,"CREATE_NO_WINDOW",0))
    for label in ("探索","主页","我的","探索","我的"):
        tap(label);time.sleep(.35)
    tap("设置");wait("主题模式");capture("settings-motion-end");back();wait("本地与下载")
    nav_record.wait(timeout=30);adb("pull","/sdcard/playback-fixes-navigation.mp4",OUT/"navigation-motion.mp4")
    check("Cached root tabs and settings forward/back transitions settle correctly")
    # A public playable podcast exercises remote artwork and the real network lock.
    tap("最近播放");wait(remote["Name"]);tap(remote["Name"])
    try:
        playing(remote["Name"])
        time.sleep(2)
        network=adb("shell","dumpsys","wifi").decode("utf-8","replace")
        (OUT/"streaming-wifi.txt").write_text(network,encoding="utf-8")
        check("Network playback acquires Petrichor's Wi-Fi lock","Petrichor:stream" in network)
        adb("shell","input","keyevent",223);time.sleep(20)
        check("Public network audio continues on the same track with screen off",remote["Name"] in media() and state()[0]==3)
        adb("shell","input","keyevent",224);adb("shell","wm","dismiss-keyguard")
        note=adb("shell","dumpsys","notification","--noredact").decode("utf-8","replace")
        (OUT/"notification-remote.txt").write_text(note,encoding="utf-8")
        check("Remote album art is also supplied as a system bitmap",bool(re.search(r"android\.largeIcon=Icon.*typ=BITMAP",note)))
        adb("shell","input","swipe",540,5,540,1800,500);time.sleep(1.2);capture("system-artwork-remote");adb("shell","cmd","statusbar","collapse");time.sleep(.6)
    finally:
        adb("shell","input","keyevent",224);adb("shell","wm","dismiss-keyguard")
    pid=adb("shell","pidof",PKG).decode().strip().split()[0]
    logs=adb("logcat","-d","--pid="+pid,"-v","threadtime").decode("utf-8","replace")
    (OUT/"logcat.txt").write_text(logs,encoding="utf-8")
    check("Release playback has no fatal app crash",not re.search(r"FATAL EXCEPTION|Fatal signal|Unhandled exception|ANR in",logs))
finally:
    (OUT/"device-checks.json").write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding="utf-8")
    capture("final-state")
    adb("shell","am","force-stop",PKG)
    adb("push",OUT/"emulator-library-before.json",REMOTE+"/library/library.json")