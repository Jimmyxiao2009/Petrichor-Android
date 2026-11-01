"""Inspect dark functions and record actual artwork/pane motion in Release."""
from pathlib import Path
import json,subprocess,time
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/"artifacts/ui-1.0.9"
helper=ROOT/"scripts/Verify-PlayerFunctions.py";ns={"__file__":str(helper)};exec(helper.read_text(encoding="utf-8").split("\nold=")[0],ns);ns["OUT"]=OUT
adb,tap,wait,bounds,center,capture,back=[ns[k] for k in ["adb","tap","wait","bounds","center","capture","back"]]
checks=json.loads((OUT/"checks.json").read_text(encoding="utf-8"))
def check(name,ok=True):
    if not ok:raise AssertionError(name)
    if name not in checks:checks.append(name)
    print("PASS "+name,flush=True)
def open_player():
    mini=next(n for n in ns["dump"]() if n.get("content-desc")=="播放或暂停");adb("shell","input","tap",300,center(mini)[1]);wait("点击封面显示歌词")
try:
    adb("shell","am","start","-n","app.petrichor.music/crc64d2d221ed02488ed4.MainActivity");wait("主页");tap("探索");wait("更多音乐")
    tap("设置");wait("主题模式");tap("主题模式");wait("选择应用外观");tap("深色");back();wait("更多音乐");open_player();tap("播放选项");nodes=capture("15-functions-dark");check("Dark functions omit song header and remain reachable","播放功能" in ns["labels"](nodes) and "点击封面显示歌词" not in ns["labels"](nodes));back();back();wait("更多音乐")
    # Disable motion temporarily, verify settle, then restore the original scale.
    scale=ns["setting"]("global","animator_duration_scale")
    try:
        adb("shell","settings","put","global","animator_duration_scale",0);open_player();tap("播放选项");wait("播放功能");tap("正在播放");wait("播放队列");back();wait("点击封面显示歌词");back();wait("更多音乐");check("Disabled system animations settle all three panes")
    finally:ns["restore"]("global","animator_duration_scale",scale)
    # Restore the emulator's original explicit light appearance.
    tap("设置");wait("主题模式");tap("主题模式");wait("选择应用外观");tap("浅色");back();wait("更多音乐")
    mini=next(n for n in ns["dump"]() if n.get("content-desc")=="播放或暂停");mini_y=center(mini)[1]
    record=subprocess.Popen([ns["ADB"],"-s","emulator-5554","shell","screenrecord","--time-limit","24","--bit-rate","5000000","/sdcard/player109-motion.mp4"],stdout=subprocess.PIPE,stderr=subprocess.PIPE,creationflags=getattr(subprocess,"CREATE_NO_WINDOW",0))
    time.sleep(.6)
    for _ in range(3):
        adb("shell","input","tap",300,mini_y);wait("点击封面显示歌词");adb("shell","input","keyevent",4);wait("更多音乐")
    record.wait(timeout=30);adb("pull","/sdcard/player109-motion.mp4",OUT/"player109-motion.mp4");wait("更多音乐");check("Repeated artwork open and close return to usable root")
    probe=json.loads(subprocess.check_output(["ffprobe","-v","error","-show_entries","format=duration","-of","json",str(OUT/"player109-motion.mp4")]))
    (OUT/"motion-metadata.json").write_text(json.dumps(probe,indent=2),encoding="utf-8");capture("16-final-root")
finally:(OUT/"checks.json").write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding="utf-8")
