"""Capture public/guest pages on the disposable DEBUG emulator, sequentially.
No credential entry, SMS or remote library/comment mutation is performed.
"""
import json, re, subprocess, sys, time
import xml.etree.ElementTree as ET
from pathlib import Path

sys.stdout.reconfigure(encoding='utf-8')
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/compact-review'
ADB=r'C:\tmp\PetrichorAndroid\sdk\platform-tools\adb.exe'
COMPONENT='app.petrichor.music/crc64d2d221ed02488ed4.MainActivity'
def adb(*args):
    return subprocess.run([ADB,'-s','emulator-5554',*map(str,args)],capture_output=True,check=True,timeout=35).stdout
def dump():
    for _ in range(3):
        result=adb('shell','uiautomator','dump','/sdcard/compact-review.xml')
        if b'dumped to' in result:return ET.fromstring(adb('shell','cat','/sdcard/compact-review.xml'))
    raise RuntimeError('No settled UI hierarchy')
def labels(root):return {n.get('text') for n in root.iter('node')}|{n.get('content-desc') for n in root.iter('node')}
def capture(name,root):
    (OUT/(name+'-ui.json')).write_text(json.dumps([n.attrib for n in root.iter('node')],ensure_ascii=False,indent=2),encoding='utf-8')
    (OUT/(name+'.png')).write_bytes(adb('exec-out','screencap','-p'))
def tap(root,label):
    nodes=[n for n in root.iter('node') if label in (n.get('text'),n.get('content-desc'))]
    if not nodes:raise RuntimeError('Missing '+label)
    node=next((n for n in nodes if n.get('clickable')=='true'),nodes[0])
    a,b,c,d=map(int,re.findall(r'\d+',node.get('bounds')));adb('shell','input','tap',(a+c)//2,(b+d)//2)
pages={
    'settings':'按你的方式听。','login':'手机验证码','playlist':'播放全部',
    'artist':'热门歌曲','albums':'歌手专辑','album':'播放全部',
    'podcast':'全部节目','queue':'接下来播放','lyrics':'歌词',
    'daily':'今日推荐','charts':'排行榜','catalog':'歌单广场',
    'search':'Coldplay','programs':'Coldplay','fm':'私人 FM',
    'player':'晴天','comments':'写下这一刻的感受…','downloads':'选择下载文件夹'
}
selected=sys.argv[1:] or list(pages)
report={}
try:
    for page in selected:
        adb('shell','am','start','-n',COMPONENT,'--es','qa-page',page)
        deadline=time.monotonic()+40
        while True:
            root=dump();names=labels(root)
            if pages[page] in names and not any(n.get('class')=='android.widget.ProgressBar' for n in root.iter('node')):break
            if time.monotonic()>deadline:raise RuntimeError('Page did not finish loading: '+page)
            time.sleep(.3)
        capture(page,root)
        before=[(n.get('text'),n.get('bounds')) for n in root.iter('node') if n.get('text')]
        adb('shell','input','swipe',540,1750,540,650,350)
        after=dump()
        moved=before!=[(n.get('text'),n.get('bounds')) for n in after.iter('node') if n.get('text')]
        capture(page+'-scrolled',after)
        report[page]={'loaded':True,'content_moved_after_swipe':moved,'screenshot':page+'.png','scrolled':page+'-scrolled.png'}
        print(page,'loaded; swipe moved:',moved,flush=True)
        adb('shell','input','keyevent','4');time.sleep(.35)
finally:
    existing=OUT/'page-review.json'
    old=json.loads(existing.read_text(encoding='utf-8')) if existing.exists() else {}
    old.update(report);existing.write_text(json.dumps(old,ensure_ascii=False,indent=2),encoding='utf-8')
