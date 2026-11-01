"""Record actual wave playback, scrub preview and original cover paging."""
from pathlib import Path
import json,subprocess,time
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/"artifacts/slider-review"
helper=ROOT/"scripts/Verify-PlayerFunctions.py";ns={"__file__":str(helper)};exec(helper.read_text(encoding="utf-8").split("\nold=")[0],ns);ns["OUT"]=OUT
adb,tap,wait,bounds,center=[ns[k] for k in ["adb","tap","wait","bounds","center"]]
# Slider checks finish on Recent with its local FLAC prepared and paused.
tap("FLAC 解码测试",last=True);wait("点击封面显示歌词")
seek=next(n for n in ns["dump"]() if n.get("class")=="android.widget.SeekBar");l,t,r,b=bounds(seek);d=(b-t)/48;y=(t+b)//2
left=l+round(12*d);right=r-round(12*d)
adb("shell","input","tap",round((left+right)/2),y);time.sleep(.5)
record=subprocess.Popen([ns["ADB"],"-s","emulator-5554","shell","screenrecord","--time-limit","14","--bit-rate","5000000","/sdcard/player-slider-motion.mp4"],stdout=subprocess.PIPE,stderr=subprocess.PIPE,creationflags=getattr(subprocess,"CREATE_NO_WINDOW",0))
time.sleep(.6);adb("shell","input","keyevent",126);time.sleep(1.8);adb("shell","input","keyevent",127);time.sleep(.5)
adb("shell","input","motionevent","DOWN",round(left+(right-left)*.5),y)
for f in [.54,.58,.62,.66,.7]:adb("shell","input","motionevent","MOVE",round(left+(right-left)*f),y);time.sleep(.1)
time.sleep(.7);adb("shell","input","motionevent","UP",round(left+(right-left)*.7),y);time.sleep(.7)
adb("shell","input","keyevent",126);time.sleep(1.8);adb("shell","input","keyevent",127);time.sleep(.5)
adb("shell","input","swipe",540,800,540,1500,600);time.sleep(.6);adb("shell","input","keyevent",4)
record.wait(timeout=22);adb("pull","/sdcard/player-slider-motion.mp4",OUT/"player-slider-motion.mp4")
probe=json.loads(subprocess.check_output(["ffprobe","-v","error","-show_entries","format=duration","-of","json",str(OUT/"player-slider-motion.mp4")]))
(OUT/"motion-metadata.json").write_text(json.dumps(probe,indent=2),encoding="utf-8");print(json.dumps(probe))
