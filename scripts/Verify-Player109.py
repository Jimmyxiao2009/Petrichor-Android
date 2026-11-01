"""Release 1.0.9 checks on the disposable emulator; no account mutations."""
from pathlib import Path
import json,re,sys,time
sys.stdout.reconfigure(encoding="utf-8")
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/"artifacts/ui-1.0.9";OUT.mkdir(exist_ok=True)
helper=ROOT/"scripts/Verify-PlayerFunctions.py";ns={"__file__":str(helper)}
exec(helper.read_text(encoding="utf-8").split("\nold=")[0],ns);ns["OUT"]=OUT
adb,dump,tap,wait,labels,bounds,center,capture,swipe,back=[ns[k] for k in ["adb","dump","tap","wait","labels","bounds","center","capture","swipe","back"]]
checks=json.loads((OUT/"checks.json").read_text(encoding="utf-8")) if "--resume-catalog" in sys.argv else []
def check(name,ok=True):
    if not ok:raise AssertionError(name)
    checks.append(name);print("PASS "+name,flush=True)
def media():
    text=adb("shell","dumpsys","media_session").decode("utf-8","replace")
    match=re.search(r"state=PlaybackState \{state=(?:[A-Z_]+\()?(\d+)\)?, position=(\d+)",text)
    return tuple(map(int,match.groups())) if match else (-1,0)
def find_control(label):return next(n for n in dump() if label in (n.get("text"),n.get("content-desc")))
try:
    if "--resume-catalog" not in sys.argv:
        adb("shell","am","force-stop","app.petrichor.music");adb("shell","am","start","-n","app.petrichor.music/crc64d2d221ed02488ed4.MainActivity");wait("主页");tap("我的");wait("最近播放");tap("最近播放");wait("FLAC 解码测试");tap("FLAC 解码测试")
        for _ in range(20):
            if media()[0]==3:break
            time.sleep(.4)
        check("Existing FLAC fixture still plays",media()[0]==3);adb("shell","input","keyevent",127);time.sleep(.6);tap("FLAC 解码测试",last=True);wait("点击封面显示歌词");capture("01-player")
        swipe(540,650,540,1450);nodes=capture("02-queue");check("Downward cover swipe opens current queue",{"正在播放","播放队列","FLAC 解码测试"}<=labels(nodes))
        swipe(540,1500,540,650);wait("点击封面显示歌词");check("Upward queue swipe returns to cover")
        swipe(540,1500,540,650);nodes=capture("03-functions");check("Upward cover swipe opens functions","播放功能" in labels(nodes))
        check("Functions contain direct Mooic actions",{"收藏到歌单","下载歌曲","评论","分享歌曲","正在播放","下一首播放","播放音质","睡眠定时"}<=labels(nodes))
        check("Functions omit current track header","FLAC 解码测试" not in labels(nodes) and "点击封面显示歌词" not in labels(nodes))
        for label in ("收藏到歌单","下载歌曲","评论","分享歌曲","正在播放"):
            n=next(n for n in nodes if n.get("class")=="android.widget.Button" and n.get("content-desc")==label);l,t,r,b=bounds(n);check(label+" target >= 48 dp",r-l>=126 and b-t>=126)
        tap("评论");wait("本地音频暂无在线评论。");check("Local audio retains its online comments guard")
        tap("正在播放");wait("播放队列");capture("05-direct-queue");check("Function queue action opens queue pane directly")
        back();wait("点击封面显示歌词");tap("播放选项");wait("播放功能");tap("播放音质");wait("无损 · FLAC");capture("06-quality");back();wait("播放功能");check("Lossless quality selection retained")
        tap("睡眠定时");wait("让音乐陪你入睡。");back();wait("播放功能");check("Sleep timer retained")
        swipe(540,1600,540,650);nodes=capture("07-functions-bottom");check("Function page scrolls to artist album and lyrics",{"查看歌手","查看专辑","歌词"}<=labels(nodes))
        tap("歌词");wait("返回封面");capture("08-lyrics");tap("返回封面");wait("播放功能");check("Lyrics returns to original function pane")
        back();wait("点击封面显示歌词");seek=next(n for n in dump() if n.get("class")=="android.widget.SeekBar");l,t,r,b=bounds(seek);y=(t+b)//2
        before=media()[1];swipe((l+r)//2,y,(l+r)//2,y+350);check("Slider vertical gesture still cancels without paging",abs(media()[1]-before)<800 and "点击封面显示歌词" in labels(dump()))
        adb("shell","input","tap",round(l+(r-l)*.5),y);time.sleep(.6);check("Slider still seeks local FLAC",abs(media()[1]-45000)<1500)
        for _ in range(3):tap("播放选项");wait("播放功能");back();wait("点击封面显示歌词")
        check("Repeated pane open and Back remain usable");back();wait("最近播放");back();wait("我的");tap("探索");wait("更多音乐");nodes=capture("09-explore")
        controls={s:next(n for n in nodes if n.get("class")=="android.widget.Button" and n.get("content-desc")==s) for s in ("每日推荐","私人 FM","播客与节目","歌单广场")}
        a,b,c,d=[bounds(controls[s]) for s in controls];check("More music uses a 2 by 2 grid",a[1]==b[1] and c[1]==d[1] and c[1]>a[1] and a[0]==c[0] and b[0]==d[0])
        tap("查看排行榜");wait("排行榜");time.sleep(1);nodes=capture("10-charts");check("Charts use native two column cards",any(n.get("class")=="android.widget.Button" and n.get("content-desc","").endswith("榜") for n in nodes))
        cards=[n for n in nodes if n.get("class")=="android.widget.Button" and "榜" in n.get("content-desc","")];first=cards[0];chartname=first.get("content-desc");tap(chartname);wait("播放全部");nodes=capture("11-playlist-expanded")
        swipe(540,1600,540,600);swipe(540,1600,540,600);nodes=capture("12-playlist-collapsed");check("Playlist title and cover collapse into header",any(n.get("content-desc","").startswith("歌单：") for n in nodes))
        song=next(n for n in nodes if n.get("clickable")=="true" and "，" in n.get("content-desc","") and bounds(n)[1]>250 and bounds(n)[3]<1900);adb("shell","input","tap",*center(song));time.sleep(2);adb("shell","input","keyevent",127);time.sleep(.5)
        nodes=dump();mini=next(n for n in nodes if n.get("content-desc")=="播放或暂停");adb("shell","input","tap",300,center(mini)[1]);wait("点击封面显示歌词");tap("播放选项");wait("播放功能");tap("评论");wait("发送");capture("04-comments");back();nodes=wait("播放功能");check("Comments Back returns directly to functions","点击封面显示歌词" not in labels(nodes));back();wait("点击封面显示歌词");back();wait("歌单："+chartname)
    back();wait("排行榜");back();wait("更多音乐");tap("每日推荐");wait("每日推荐");nodes=capture("13-daily-guest");check("Daily artwork header exists for guest","播放全部" in labels(nodes))
    back();wait("更多音乐");capture("14-root-return");check("Root remains usable after detail roundtrip")
except Exception:
    (OUT/"failure-latest.png").write_bytes(adb("exec-out","screencap","-p"));raise
finally:
    (OUT/"checks.json").write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding="utf-8")
