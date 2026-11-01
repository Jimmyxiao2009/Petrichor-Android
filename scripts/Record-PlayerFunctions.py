"""Record real player paging after the Release interaction checks leave cover open."""
import subprocess,time,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/"artifacts/md3-functions";ADB=r"C:\tmp\PetrichorAndroid\sdk\platform-tools\adb.exe"
def adb(*args):return subprocess.run([ADB,"-s","emulator-5554",*map(str,args)],capture_output=True,check=True,timeout=40).stdout
record=subprocess.Popen([ADB,"-s","emulator-5554","shell","screenrecord","--time-limit","12","--bit-rate","5000000","/sdcard/player-functions-motion.mp4"],stdout=subprocess.PIPE,stderr=subprocess.PIPE,creationflags=getattr(subprocess,"CREATE_NO_WINDOW",0))
time.sleep(.7)
adb("shell","input","swipe",540,800,540,1550,800);time.sleep(1)
adb("shell","input","swipe",540,1600,540,800,800);time.sleep(1)
adb("shell","input","tap",540,750);time.sleep(1.3)
adb("shell","input","keyevent",4);time.sleep(1)
adb("shell","input","swipe",540,800,540,1550,600);time.sleep(.5)
adb("shell","input","keyevent",4)
record.wait(timeout=20);adb("pull","/sdcard/player-functions-motion.mp4",OUT/"player-functions-motion.mp4")
probe=json.loads(subprocess.check_output(["ffprobe","-v","error","-show_entries","format=duration","-of","json",str(OUT/"player-functions-motion.mp4")]))
(OUT/"motion-metadata.json").write_text(json.dumps(probe,indent=2),encoding="utf-8")
print(json.dumps(probe))
