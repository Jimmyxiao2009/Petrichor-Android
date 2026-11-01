"""Public emulator QA: persistent/offline cache and native player gesture arbitration."""
import json,re,subprocess,time,sys
from pathlib import Path
import xml.etree.ElementTree as ET
sys.stdout.reconfigure(encoding='utf-8')
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/cache-review';OUT.mkdir(parents=True,exist_ok=True)
ADB=r'C:\tmp\PetrichorAndroid\sdk\platform-tools\adb.exe'
PKG='app.petrichor.music';COMPONENT=PKG+'/crc64d2d221ed02488ed4.MainActivity'
checks=[]
def adb(*args):return subprocess.run([ADB,'-s','emulator-5554',*map(str,args)],capture_output=True,check=True,timeout=45).stdout
def dump():
    for _ in range(3):
        if b'dumped to' in adb('shell','uiautomator','dump','/sdcard/cache-review.xml'):
            return ET.fromstring(adb('shell','cat','/sdcard/cache-review.xml'))
    raise RuntimeError('UI did not settle')
def labels(root):return {n.get('text','') for n in root.iter('node')}|{n.get('content-desc','') for n in root.iter('node')}
def capture(name,root=None):
    (OUT/(name+'.png')).write_bytes(adb('exec-out','screencap','-p'))
    if root is not None:(OUT/(name+'.json')).write_text(json.dumps([n.attrib for n in root.iter('node')],ensure_ascii=False,indent=2),encoding='utf-8')
def check(ok,label):
    checks.append({'check':label,'passed':bool(ok)});print(('PASS ' if ok else 'FAIL ')+label,flush=True)
    (OUT/'cache-checks.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding='utf-8')
    if not ok:raise AssertionError(label)
def tap(root,label):
    found=[n for n in root.iter('node') if label in (n.get('text'),n.get('content-desc'))]
    if not found:raise AssertionError('Missing '+label)
    node=next((n for n in found if n.get('clickable')=='true'),found[0]);a,b,c,d=map(int,re.findall(r'\d+',node.get('bounds')));adb('shell','input','tap',(a+c)//2,(b+d)//2)
def route(page):adb('shell','am','start','-n',COMPONENT,'--es','qa-page',page);time.sleep(.8);return dump()
def swipe(x,y,endx,endy,duration=300):adb('shell','input','swipe',x,y,endx,endy,duration);time.sleep(.4);return dump()
try:
    root=route('player');check('上滑查看功能' in labels(root),'Player opens with bounded controls');capture('debug-player',root)
    root=swipe(850,1350,850,700);check('返回播放' in labels(root),'Swipe up outside cover opens functions');capture('debug-functions',root)
    root=swipe(850,700,850,1500);check('上滑查看功能' in labels(root),'Swipe down in function content returns to playback')
    root=swipe(480,1400,480,1340,550);check('上滑查看功能' in labels(root),'Small slow drag snaps back without an accidental page change')
    tap(root,'点击封面显示歌词');time.sleep(.5);root=dump();capture('debug-inline-lyrics',root)
    root=swipe(500,1300,500,800,450);check('返回播放' not in labels(root),'Lyrics retain their own scroll instead of opening functions')
    root=route('charts');check(any('榜' in x and x!='排行榜' for x in labels(root)),'Charts load before offline test');capture('debug-charts',root)
    adb('shell','am','force-stop',PKG);adb('shell','svc','wifi','disable');adb('shell','svc','data','disable')
    start=time.monotonic();adb('shell','am','start','-W','-n',COMPONENT);capture('debug-offline-first-frame');root=dump();elapsed=round(time.monotonic()-start,2)
    check(any('首' in x or '万' in x for x in labels(root)),'Offline process restart displays persisted discovery data');capture('debug-offline-discover',root)
    root=route('charts');check(any('榜' in x and x!='排行榜' for x in labels(root)),'A new charts page renders disk cache while offline');capture('debug-offline-charts',root)
    (OUT/'offline-timing.json').write_text(json.dumps({'restart_to_completed_uiautomator_dump_seconds':elapsed,'note':'Includes adb launch and UIAutomator idle/dump overhead; not a time-to-first-content benchmark.'},indent=2),encoding='utf-8')
finally:
    adb('shell','svc','wifi','enable');adb('shell','svc','data','enable')
print(json.dumps({'passed':len(checks),'total':len(checks)}))
