"""Verify the existing MAUI application after transplanting Mooic UI. No account mutations."""
import json,re,subprocess,time,sys
from pathlib import Path
import xml.etree.ElementTree as ET
sys.stdout.reconfigure(encoding="utf-8")
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/"artifacts/maui-mooic-port";OUT.mkdir(exist_ok=True)
ADB=r"C:\tmp\PetrichorAndroid\sdk\platform-tools\adb.exe";PKG="app.petrichor.music";ACT=PKG+"/crc64d2d221ed02488ed4.MainActivity"
checks=[]
def adb(*args):return subprocess.run([ADB,"-s","emulator-5554",*map(str,args)],capture_output=True,check=True,timeout=50).stdout

def dump():
    for _ in range(3):
        if b"dumped to" in adb("shell","uiautomator","dump","/sdcard/maui-port.xml"):
            return list(ET.fromstring(adb("shell","cat","/sdcard/maui-port.xml")).iter("node"))
    raise RuntimeError("UI did not settle")
def bounds(n):return tuple(map(int,re.findall(r"\d+",n.get("bounds",""))))
def labels(nodes):return {n.get("text","") for n in nodes}|{n.get("content-desc","") for n in nodes}
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
        time.sleep(.5)
    raise AssertionError("Missing page label: "+label)
def capture(name):
    ns=dump();time.sleep(.2);(OUT/(name+".png")).write_bytes(adb("exec-out","screencap","-p"));(OUT/(name+".json")).write_text(json.dumps([n.attrib for n in ns],ensure_ascii=False,indent=2),encoding="utf-8");return ns

def check(name,condition=True):
    if not condition:raise AssertionError(name)
    checks.append(name);print("PASS "+name,flush=True)

def back():adb("shell","input","keyevent",4);time.sleep(.5)
try:
    adb("shell","svc","wifi","enable");adb("shell","svc","data","enable");adb("shell","am","force-stop",PKG);adb("shell","am","start","-n",ACT)
    ns=wait("推荐歌曲");capture("01-home-release");check("Existing MAUI package displays songs and playlists",{"主页","推荐歌曲","推荐歌单"}<=labels(ns));check("No oversized welcome section","听见，新的喜欢。" not in labels(ns))
    more=[n for n in ns if n.get("content-desc")=="更多操作" and 400<bounds(n)[1]<1300]
    check("Song actions remain connected",bool(more));x,y=center(more[0]);adb("shell","input","tap",x,y);wait("加入播放队列");capture("08-song-menu-release");tap("加入播放队列");ns=wait("播放队列");capture("09-mini-release")
    # Use the mini player text, not the song row above it.
    title=next(n for n in ns if n.get("class")=="android.widget.TextView" and bounds(n)[1]>1800 and n.get("text"))
    adb("shell","input","tap",*center(title));wait("点击封面显示歌词");ns=capture("02-player-release");check("Large cover and existing seek control",any(n.get("class")=="android.widget.SeekBar" for n in ns));check("No redundant now-playing caption","正在播放" not in labels(ns))
    tap("点击封面显示歌词");ns=capture("03-lyrics-release");check("Lyrics have a separate full reading state", "返回封面" in labels(ns) and not any(n.get("class")=="android.widget.SeekBar" for n in ns))
    before=[n.get("text") for n in ns if n.get("class")=="android.widget.TextView" and bounds(n)[1]>400];adb("shell","input","swipe",540,1900,540,750,500);ns=capture("10-lyrics-scroll-release");after=[n.get("text") for n in ns if n.get("class")=="android.widget.TextView" and bounds(n)[1]>400];check("Lyrics scroll independently",before!=after)
    tap("返回封面");wait("点击封面显示歌词")
    states=set()
    for _ in range(3):
        n=next(n for n in dump() if n.get("content-desc") in ("列表循环","单曲循环","不循环"));states.add(n.get("content-desc"));adb("shell","input","tap",*center(n));time.sleep(.35)
    check("All repeat states and icon feedback remain connected",len(states)==3)
    tap("播放选项");wait("评论");capture("11-player-menu-release");tap("评论");wait("发送");ns=capture("12-comments-release");check("Comments route uses Petrichor data and compact composer", "评论" in labels(ns) and "发送" in labels(ns))
    back();wait("点击封面显示歌词");tap("返回");wait("主页");tap("探索");wait("排行榜");ns=capture("05-explore-release");check("Explore keeps charts and added Petrichor features",{"每日推荐","私人 FM","播客与节目","歌单广场"}<=labels(ns))
    tap("打开播客与节目");wait("播客");ns=capture("13-podcasts-release");check("Podcast screen stays reachable", "我的订阅" in labels(ns));back()
    tap("搜索");wait("节目");ns=capture("14-search-release");check("Search keeps all six Petrichor categories",{"单曲","歌单","歌手","专辑","播客","节目"}<=labels(ns));check("Search opened as a secondary page with Back","返回" in labels(ns));back();wait("探索");tap("我的");wait("点击登录");capture("04-library-release")
    tap("登录网易云音乐");wait("登录");ns=capture("15-login-release");check("Existing login remains reachable",any("验证码" in t for t in labels(ns)));back();wait("点击登录");tap("本地与下载");wait("选择下载文件夹");capture("16-downloads-release");check("Existing downloads and folder picker remain reachable")
    tap("选择下载文件夹");ns=capture("17-folder-picker-release");check("Android document tree picker opens",any(n.get("package")=="com.google.android.documentsui" for n in ns));
    for _ in range(8):
        if not any(n.get("package")=="com.google.android.documentsui" for n in dump()):break
        back()
    back();wait("我的")
    # Public Home content must survive a process restart without a network.
    tap("主页");wait("推荐歌单");adb("shell","svc","wifi","disable");adb("shell","svc","data","disable");adb("shell","am","force-stop",PKG);adb("shell","am","start","-n",ACT);ns=wait("推荐歌单");
    for _ in range(6):
        ns=dump()
        if any(n.get("content-desc")=="更多操作" for n in ns):break
        time.sleep(.5)
    capture("18-offline-home-release");check("Cached songs render after offline process restart",any(n.get("content-desc")=="更多操作" for n in ns))
finally:
    adb("shell","svc","wifi","enable");adb("shell","svc","data","enable");(OUT/"checks.json").write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding="utf-8")
