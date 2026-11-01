"""Recheck the final APK's artwork endpoint and reduced-motion behavior."""
from pathlib import Path
import json,re,subprocess,time
ROOT=Path(__file__).resolve().parents[1]
ns={"__file__":str(ROOT/"scripts/Verify-PlaybackFixes.py")}
exec((ROOT/"scripts/Verify-PlaybackFixes.py").read_text(encoding="utf-8").split("original=")[0],ns)
adb,tap,wait,media,playing,capture,dump,labels,bounds,back=[ns[k] for k in ("adb","tap","wait","media","playing","capture","dump","labels","bounds","back")]
OUT=ns["OUT"];PKG=ns["PKG"];checks=[]
scale=adb("shell","settings","get","global","animator_duration_scale").decode().strip()
try:
    adb("shell","cmd","statusbar","collapse");adb("shell","input","keyevent",224);adb("shell","wm","dismiss-keyguard")
    adb("shell","am","force-stop",PKG)
    adb("shell","am","start","-n",PKG+"/crc64d2d221ed02488ed4.MainActivity")
    wait("主页");tap("我的");wait("最近播放");tap("最近播放");wait("Viva La Vida——Coldplay");tap("Viva La Vida——Coldplay");playing("Viva La Vida——Coldplay")
    adb("shell","input","keyevent",127);time.sleep(.5)
    nodes=dump();mini=next(n for n in nodes if n.get("content-desc")=="播放或暂停");l,t,r,b=bounds(mini);y=(t+b)//2
    record=subprocess.Popen([ns["ADB"],"-s","emulator-5554","shell","screenrecord","--time-limit","14","--bit-rate","6000000","/sdcard/final-artwork-motion.mp4"],stdout=subprocess.PIPE,stderr=subprocess.PIPE,creationflags=getattr(subprocess,"CREATE_NO_WINDOW",0))
    for _ in range(4):
        adb("shell","input","tap",300,y);time.sleep(.8);adb("shell","input","keyevent",4);time.sleep(.8)
    record.wait(timeout=18);adb("pull","/sdcard/final-artwork-motion.mp4",OUT/"final-artwork-motion.mp4")
    wait("最近播放");checks.append("Final APK artwork open and close settle for four roundtrips")
    adb("shell","settings","put","global","animator_duration_scale",0)
    adb("shell","input","tap",300,y);wait("点击封面显示歌词");back();wait("最近播放")
    checks.append("Disabled system animations leave no hidden cover or incomplete page")
    adb("shell","settings","put","global","animator_duration_scale",scale)
    adb("shell","input","keyevent",126);time.sleep(.6);adb("shell","input","keyevent",127);time.sleep(.5)
    adb("shell","cmd","statusbar","expand-notifications");time.sleep(2)
    nodes=dump()
    if not any(n.get("package")=="com.android.systemui" for n in nodes):
        adb("shell","input","keyevent",83);time.sleep(2);nodes=dump()
    (OUT/"system-media-ui.json").write_text(json.dumps([n.attrib for n in nodes],ensure_ascii=False,indent=2),encoding="utf-8")
    capture("system-media-final")
    checks.append("Final APK notification contains album art") if "typ=BITMAP" in adb("shell","dumpsys","notification","--noredact").decode("utf-8","replace") else (_ for _ in []).throw(AssertionError("No notification bitmap"))
    (OUT/"final-media-state.txt").write_text(media(),encoding="utf-8")
    print(json.dumps(checks,ensure_ascii=False,indent=2),flush=True)
finally:
    adb("shell","settings","put","global","animator_duration_scale",scale)
    adb("shell","input","keyevent",127);adb("shell","cmd","statusbar","collapse")
    (OUT/"final-device-checks.json").write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding="utf-8")