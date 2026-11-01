"""Release-only public navigation, cache and gestures; no debug entry points."""
import json,re,subprocess,time,sys,xml.etree.ElementTree as ET
from pathlib import Path
sys.stdout.reconfigure(encoding='utf-8')
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'artifacts/cache-review';OUT.mkdir(parents=True,exist_ok=True)
ADB=r'C:\tmp\PetrichorAndroid\sdk\platform-tools\adb.exe';PKG='app.petrichor.music';COMPONENT=PKG+'/crc64d2d221ed02488ed4.MainActivity'
results=[]
def adb(*args):return subprocess.run([ADB,'-s','emulator-5554',*map(str,args)],capture_output=True,check=True,timeout=45).stdout
def dump():
    for _ in range(3):
        if b'dumped to' in adb('shell','uiautomator','dump','/sdcard/cache-release.xml'):return ET.fromstring(adb('shell','cat','/sdcard/cache-release.xml'))
    raise RuntimeError('UI did not settle')
def labels(root):return {n.get('text','') for n in root.iter('node')}|{n.get('content-desc','') for n in root.iter('node')}
def tap(root,label,last=False):
    nodes=[n for n in root.iter('node') if label in (n.get('text'),n.get('content-desc'))];assert nodes,'Missing '+label
    if label=='更多操作':nodes=[n for n in nodes if 400<int(re.findall(r'\d+',n.get('bounds'))[1])<2000]
    node=nodes[-1] if last else next((n for n in nodes if n.get('clickable')=='true'),nodes[0]);a,b,c,d=map(int,re.findall(r'\d+',node.get('bounds')));adb('shell','input','tap',(a+c)//2,(b+d)//2)
def check(ok,label):
    results.append({'check':label,'passed':bool(ok)});print(('PASS ' if ok else 'FAIL ')+label,flush=True)
    (OUT/'release-checks.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8');assert ok,label
def capture(name,root=None):
    (OUT/(name+'.png')).write_bytes(adb('exec-out','screencap','-p'))
    if root is not None:(OUT/(name+'.json')).write_text(json.dumps([n.attrib for n in root.iter('node')],ensure_ascii=False,indent=2),encoding='utf-8')
def restart():
    adb('shell','am','force-stop',PKG);output=adb('shell','am','start','-W','-n',COMPONENT);return output.decode('utf-8','replace')
def wait_for(label,seconds=40):
    deadline=time.monotonic()+seconds
    while True:
        root=dump()
        if label in labels(root):return root
        if time.monotonic()>deadline:raise AssertionError('Timed out waiting for '+label)
def back():adb('shell','input','keyevent','4');time.sleep(.3);return dump()
try:
    launch=restart();(OUT/'release-launch.txt').write_text(launch,encoding='utf-8');root=wait_for('听见，新的喜欢。');capture('release-discover',root)
    tap(root,'排行榜');root=wait_for('排行榜');chart=next(n.get('content-desc') for n in root.iter('node') if n.get('class')=='android.view.ViewGroup' and '榜' in n.get('content-desc',''))
    check(bool(chart),'Release displays prefetched charts');capture('release-charts',root);tap(root,chart);root=wait_for('播放全部');capture('release-playlist',root)
    songs=[n.get('text') for n in root.iter('node') if n.get('class')=='android.widget.TextView' and n.get('text') and n.get('text') not in {'歌单','播放全部'}]
    check(any(n.get('content-desc')=='更多操作' for n in root.iter('node')),'Release playlist renders song rows')
    tap(root,'更多操作');root=wait_for('加入播放队列');tap(root,'加入播放队列');root=dump()
    # The mini player's title is the text directly above its artist caption.
    title_nodes=[n for n in root.iter('node') if n.get('class')=='android.widget.TextView' and n.get('text') and int(re.findall(r'\d+',n.get('bounds'))[1])>2150]
    assert title_nodes,'No queue mini player';song=title_nodes[0].get('text');tap(root,song,last=True);root=wait_for('上滑查看功能');capture('release-player',root)
    adb('shell','input','swipe','850','1350','850','650','300');root=wait_for('返回播放');check(True,'Release upward swipe from metadata opens functions');capture('release-functions',root)
    adb('shell','input','swipe','850','700','850','1500','300');root=wait_for('上滑查看功能');check(True,'Release downward swipe from function content returns')
    tap(root,'评论');root=wait_for('写下这一刻的感受…');time.sleep(1);root=dump();capture('release-comments',root)
    comment_count=sum(n.get('class')=='android.widget.TextView' and len(n.get('text',''))>20 for n in root.iter('node'))
    check(comment_count>0,'Release loads public comments through the cached EAPI read path');back()
    adb('shell','svc','wifi','disable');adb('shell','svc','data','disable')
    root=dump();tap(root,'评论');root=wait_for('写下这一刻的感受…');capture('release-offline-comments',root)
    check(any(n.get('class')=='android.widget.TextView' and len(n.get('text',''))>20 for n in root.iter('node')),'New comment page displays cached content offline')
    offline_launch=restart();capture('release-offline-first-frame');root=wait_for('听见，新的喜欢。')
    check(any('首' in s or '万' in s for s in labels(root)),'Release cold process restart displays discovery cache offline');capture('release-offline-discover',root)
    tap(root,'排行榜');root=wait_for('排行榜');check(chart in labels(root),'Release charts remain available offline');tap(root,chart);root=wait_for('播放全部')
    check(any(n.get('content-desc')=='更多操作' for n in root.iter('node')),'Playlist detail and song rows survive an offline restart');capture('release-offline-playlist',root)
    (OUT/'release-offline-launch.txt').write_text(offline_launch,encoding='utf-8')
finally:
    adb('shell','svc','wifi','enable');adb('shell','svc','data','enable')
print(json.dumps({'passed':len(results),'total':len(results)}))
