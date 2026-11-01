"""Record the installed Release player using an already imported local FLAC fixture."""
import json,re,subprocess,time,sys,xml.etree.ElementTree as ET
from pathlib import Path
sys.stdout.reconfigure(encoding='utf-8')
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'artifacts/cache-review';ADB=r'C:\tmp\PetrichorAndroid\sdk\platform-tools\adb.exe'
def adb(*args):return subprocess.run([ADB,'-s','emulator-5554',*map(str,args)],capture_output=True,check=True,timeout=35).stdout
def capture(name):(OUT/(name+'.png')).write_bytes(adb('exec-out','screencap','-p'))
adb('shell','input','swipe',180,1912,680,1912,500)
adb('shell','uiautomator','dump','/sdcard/cache-seek.xml');root=ET.fromstring(adb('shell','cat','/sdcard/cache-seek.xml'))
labels={n.get('text','') for n in root.iter('node')}|{n.get('content-desc','') for n in root.iter('node')}
times=[s for s in labels if re.fullmatch(r'\d+:\d{2}',s)]
assert '上滑查看功能' in labels and len(times)>=2 and any(s not in {'0:00','1:30'} for s in times), 'Horizontal drag did not seek on the playback pane'
capture('release-local-paused-seek')
(OUT/'release-seek-check.json').write_text(json.dumps({'passed':True,'check':'Paused horizontal seeking changes time without opening functions','times':times},ensure_ascii=False,indent=2),encoding='utf-8')
adb('shell','dumpsys','gfxinfo','app.petrichor.music','reset')
record=subprocess.Popen([ADB,'-s','emulator-5554','shell','screenrecord','--time-limit','10','--bit-rate','5000000','/sdcard/petrichor-cache-motion.mp4'],stdout=subprocess.PIPE,stderr=subprocess.PIPE)
try:
    time.sleep(.5);adb('shell','input','tap',540,2120);time.sleep(1);capture('release-wave-playing')
    adb('shell','input','swipe',850,1350,850,650,300);time.sleep(.4);capture('release-local-functions')
    adb('shell','input','swipe',850,700,850,1500,300);time.sleep(.5);capture('release-wave-return')
    adb('shell','input','tap',540,2120);time.sleep(.8);capture('release-wave-paused')
finally:
    stdout,stderr=record.communicate(timeout=20)
    if record.returncode:raise RuntimeError(stderr.decode('utf-8','replace'))
adb('pull','/sdcard/petrichor-cache-motion.mp4',OUT/'release-motion.mp4')
(OUT/'release-motion-frames.txt').write_bytes(adb('shell','dumpsys','gfxinfo','app.petrichor.music'))
print('Release seek check passed; native playback/gesture video saved.')
