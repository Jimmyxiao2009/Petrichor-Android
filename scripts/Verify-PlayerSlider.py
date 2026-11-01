"""Real Release slider/gesture checks using an existing local FLAC fixture."""
from pathlib import Path
import json,re,sys,time,subprocess
sys.stdout.reconfigure(encoding="utf-8")
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/"artifacts/slider-review";OUT.mkdir(exist_ok=True)
# Shared ADB inspection helpers, before the standalone functions test's entry point.
helper=ROOT/"scripts/Verify-PlayerFunctions.py";ns={"__file__":str(helper)}
exec(helper.read_text(encoding="utf-8").split("\nold=")[0],ns)
ns["OUT"]=OUT
adb,dump,tap,wait,labels,bounds,center,capture,swipe,back=[ns[k] for k in ["adb","dump","tap","wait","labels","bounds","center","capture","swipe","back"]]
checks=[]
def check(name,ok=True):
    if not ok:raise AssertionError(name)
    checks.append(name);print("PASS "+name,flush=True)
def media():
    text=adb("shell","dumpsys","media_session").decode("utf-8","replace")
    (OUT/"media-latest.txt").write_text(text,encoding="utf-8")
    match=re.search(r"state=PlaybackState \{state=(?:[A-Z_]+\()?(\d+)\)?, position=(\d+)",text)
    if not match:raise AssertionError("No prepared media session")
    return tuple(map(int,match.groups()))
def raw_capture(name):(OUT/(name+".png")).write_bytes(adb("exec-out","screencap","-p"))
def touch(action,x,y):adb("shell","input","motionevent",action,x,y)
def pause():adb("shell","input","keyevent",127);time.sleep(.5)
try:
    adb("shell","am","force-stop","app.petrichor.music");adb("shell","am","start","-n","app.petrichor.music/crc64d2d221ed02488ed4.MainActivity");wait("主页");tap("我的");wait("最近播放");tap("最近播放");wait("FLAC 解码测试");tap("FLAC 解码测试")
    # Do not pause while MediaPlayer is still preparing; that would cancel the load.
    for _ in range(15):
        try:
            if media()[0]==3:break
        except AssertionError:pass
        time.sleep(.4)
    check("Existing local FLAC prepares and plays",media()[0]==3);pause();tap("FLAC 解码测试",last=True);wait("点击封面显示歌词")
    nodes=capture("01-paused-release");seek=next(n for n in nodes if n.get("class")=="android.widget.SeekBar")
    l,t,r,b=bounds(seek);d=(b-t)/48;left=l+round(12*d);right=r-round(12*d);y=(t+b)//2
    check("Native accessible SeekBar keeps a 48 dp target",b-t>=126 and seek.get("enabled")=="true" and seek.get("focusable")=="true")
    x=lambda f:round(left+(right-left)*f)
    adb("shell","input","tap",x(.35),y);time.sleep(.7);baseline=media()[1];check("Track tap seeks local audio",abs(baseline-31500)<1500);capture("02-paused-midpoint-release")
    touch("DOWN",x(.35),y);touch("MOVE",x(.7),y);time.sleep(.35)
    nodes=capture("03-drag-preview-release");check("Time preview appears while dragging",any(s.startswith("预览进度 ") for s in labels(nodes)))
    check("Dragging previews without seeking audio",abs(media()[1]-baseline)<800)
    touch("UP",x(.7),y);time.sleep(.7);after=media()[1];check("Release commits the requested position",abs(after-63000)<1500)
    nodes=capture("04-drag-released-release");check("Preview is removed after release",not any(s.startswith("预览进度 ") for s in labels(nodes)))
    before=media()[1];swipe(x(.7),y,x(.7),y+350,600);check("Vertical gesture on the slider cancels without seeking",abs(media()[1]-before)<800);check("Slider gesture never opens function panel","播放功能" not in labels(wait("点击封面显示歌词")))
    # The same touch stream keeps ownership after a horizontal direction is chosen.
    touch("DOWN",x(.7),y);touch("MOVE",x(.5),y);touch("MOVE",x(.4),y+150);touch("UP",x(.4),y+150);time.sleep(.7)
    check("Horizontal scrubbing tolerates vertical finger drift",abs(media()[1]-36000)<1500 and "播放功能" not in labels(wait("点击封面显示歌词")))
    adb("shell","input","tap",x(0),y);time.sleep(.6);check("Start endpoint reaches zero",media()[1]<1000)
    adb("shell","input","tap",x(1),y);time.sleep(.6);check("End endpoint remains within duration",88000<=media()[1]<=90000)
    adb("shell","input","tap",x(.5),y);time.sleep(.6);capture("05-paused-half-release")
    adb("shell","input","keyevent",126);time.sleep(.9);check("Playback resumes after scrubbing",media()[0]==3);raw_capture("06-playing-wave-release");time.sleep(.3);raw_capture("07-playing-wave-next-release");pause();wait("点击封面显示歌词")
    swipe(540,800,540,1600);nodes=capture("08-functions-after-seek-release");check("Original cover downward gesture still opens functions","播放功能" in labels(nodes));back();wait("点击封面显示歌词")
    tap("播放选项");wait("播放功能");tap("播放音质");wait("无损 · FLAC");back();wait("播放功能");back();wait("点击封面显示歌词");check("Existing menu actions remain usable after seeking")
    for _ in range(8):
        nodes=dump();focused=next((n for n in nodes if n.get("class")=="android.widget.SeekBar" and n.get("focused")=="true"),None)
        if focused is not None:break
        adb("shell","input","keyevent",61)
    check("Seek control remains reachable by keyboard focus",focused is not None)
    before=media()[1];adb("shell","input","keyevent",22);time.sleep(.7);check("Keyboard right seeks approximately one second",500<=media()[1]-before<=1500)
    tap("返回");wait("最近播放");check("Original top collapse control returns to previous page")
except Exception:
    raw_capture("failure-latest")
    raise
finally:
    try:touch("UP",540,1600);pause()
    except Exception:pass
    (OUT/"checks.json").write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding="utf-8")
