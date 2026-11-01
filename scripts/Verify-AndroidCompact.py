"""Native interaction checks on a disposable emulator with a DEBUG build.
Only synthetic unsent comments are typed. Always restore display size.
"""
import json,re,subprocess,time,sys
import xml.etree.ElementTree as ET
from pathlib import Path
sys.stdout.reconfigure(encoding='utf-8')
OUT=Path(__file__).resolve().parents[1]/'artifacts/compact-review'
ADB=r'C:\tmp\PetrichorAndroid\sdk\platform-tools\adb.exe'
COMP='app.petrichor.music/crc64d2d221ed02488ed4.MainActivity'
checks={}
def adb(*args):return subprocess.run([ADB,'-s','emulator-5554',*map(str,args)],capture_output=True,check=True,timeout=40).stdout
def dump():
    for _ in range(3):
        if b'dumped to' in adb('shell','uiautomator','dump','/sdcard/verify-compact.xml'):
            return ET.fromstring(adb('shell','cat','/sdcard/verify-compact.xml'))
    raise RuntimeError('No settled hierarchy')
def find(root,text):
    nodes=[n for n in root.iter('node') if text in (n.get('text'),n.get('content-desc'))]
    return next((n for n in nodes if n.get('clickable')=='true'),nodes[0] if nodes else None)
def rect(n):return tuple(map(int,re.findall(r'\d+',n.get('bounds'))))
def tap(text):
    n=find(dump(),text);assert n is not None,text
    a,b,c,d=rect(n);adb('shell','input','tap',(a+c)//2,(b+d)//2)
def wait(text):
    for _ in range(10):
        root=dump()
        if find(root,text) is not None:return root
        time.sleep(.2)
    raise AssertionError('Missing '+text)
def capture(name,root=None):
    root=root if root is not None else dump()
    (OUT/(name+'-ui.json')).write_text(json.dumps([n.attrib for n in root.iter('node')],ensure_ascii=False,indent=2),encoding='utf-8')
    (OUT/(name+'.png')).write_bytes(adb('exec-out','screencap','-p'))
def check(name,value):
    checks[name]=bool(value);print(name, bool(value),flush=True);assert value,name
def route(page):adb('shell','am','start','-n',COMP,'--es','qa-page',page)
try:
    route('player');root=wait('晴天')
    check('no_playing_context_line',not any((n.get('text') or '').startswith('正在播放 ·') for n in root.iter('node')))
    tap('列表循环');root=wait('单曲循环');check('repeat_one_glyph',find(root,'单曲循环').get('text')=='\ue041');capture('repeat-one',root)
    tap('单曲循环');wait('不循环');capture('repeat-off')
    tap('不循环');wait('列表循环');capture('repeat-all')
    tap('随机播放已关闭');wait('随机播放已开启');capture('shuffle-on')
    tap('随机播放已开启');wait('随机播放已关闭')
    tap('点击封面显示歌词');root=wait('歌词');check('inline_lyrics_keeps_progress',find(root,'播放进度') is not None);capture('player-lyrics',root)
    tap('歌词');wait('点击封面显示歌词')
    # Start inside the cover, then drag upward beyond the snapping threshold.
    a,b,c,d=rect(find(dump(),'点击封面显示歌词'))
    adb('shell','input','swipe',(a+c)//2,(b+d)//2,(a+c)//2,max(150,(b+d)//2-700),450)
    root=wait('这首歌');check('cover_swipe_opens_functions',True);capture('player-functions',root)
    tap('返回播放');wait('点击封面显示歌词');check('return_button_works',True)
    tap('上滑查看功能');root=wait('下滑返回播放');a,b,c,d=rect(find(root,'下滑返回播放'))
    adb('shell','input','swipe',(a+c)//2,(b+d)//2,(a+c)//2,(b+d)//2+650,450)
    wait('点击封面显示歌词');check('function_grip_swipe_returns',True)
    tap('评论');root=wait('写下这一刻的感受…');capture('comments',root)
    entry=next(n for n in root.iter('node') if n.get('class')=='android.widget.EditText');before=rect(entry)
    check('composer_starts_one_line',before[3]-before[1]<=105)
    tap('写下这一刻的感受…');root=dump();capture('comments-keyboard',root)
    entry=next(n for n in root.iter('node') if n.get('class')=='android.widget.EditText');check('composer_above_keyboard',rect(entry)[3]<1700)
    adb('shell','input','text',('Native%stest%scomment%sfor%sline%swrap.%s'*5))
    root=dump();entry=next(n for n in root.iter('node') if n.get('class')=='android.widget.EditText');height=rect(entry)[3]-rect(entry)[1]
    check('composer_grows_with_text',height>before[3]-before[1] and height<300);capture('comments-grow',root)
    adb('shell','input','keyevent','4');adb('shell','input','keyevent','4');wait('点击封面显示歌词')
    # A 343 x 610 dp viewport must scroll its otherwise short download page.
    adb('shell','wm','size','900x1600');route('downloads');root=wait('选择下载文件夹');capture('small-downloads',root)
    old=[(n.get('text'),n.get('bounds')) for n in root.iter('node') if n.get('text')]
    adb('shell','input','swipe',450,1180,450,450,450);root=dump()
    check('small_downloads_scrolls',old!=[(n.get('text'),n.get('bounds')) for n in root.iter('node') if n.get('text')]);capture('small-downloads-scrolled',root)
    adb('shell','input','keyevent','4');route('player');root=wait('晴天');capture('small-player',root)
    check('small_player_controls_visible',all(find(root,label) is not None and rect(find(root,label))[3]<1600 for label in ['播放或暂停','上一首','下一首','评论','播放进度']))
finally:
    adb('shell','wm','size','reset')
    (OUT/'native-checks.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding='utf-8')
