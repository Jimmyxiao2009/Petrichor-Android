"""Exercise rapid system back on the disposable emulator, starting on Discover."""
import json
import argparse
import re
import subprocess
import time
import xml.etree.ElementTree as ET
from pathlib import Path

adb_path=r'C:\tmp\PetrichorAndroid\sdk\platform-tools\adb.exe'
parser=argparse.ArgumentParser();parser.add_argument('--out',type=Path,default=Path(__file__).resolve().parents[1]/'artifacts/polish');args=parser.parse_args()
out=args.out.resolve();out.mkdir(parents=True,exist_ok=True)
def adb(*args):return subprocess.run([adb_path,'-s','emulator-5554',*args],capture_output=True,check=True,timeout=60).stdout.decode('utf-8','replace')
def dump():
    for _ in range(8):
        status=adb('shell','uiautomator','dump','/sdcard/petrichor-nav.xml')
        if 'dumped to' in status:return ET.fromstring(adb('shell','cat','/sdcard/petrichor-nav.xml'))
        time.sleep(.4)
    raise RuntimeError('UI hierarchy not ready')
def find(root,label):return next((n for n in root.iter('node') if label in (n.get('text'),n.get('content-desc'))),None)
def tap(node):
    assert node is not None,'Missing expected control'
    a,b,c,d=map(int,re.findall(r'\d+',node.get('bounds')))
    adb('shell','input','tap',str((a+c)//2),str((b+d)//2))

checks={}
try:
    for index in range(3):
        tap(find(dump(),'查看全部  ›'))
        root=dump()
        tile=next((n for n in root.iter('node') if n.get('class')=='android.view.ViewGroup' and n.get('content-desc') and not n.get('content-desc').startswith('让音乐')),None)
        assert tile is not None,'No public playlist tile'
        tap(tile);root=dump();assert find(root,'歌单') is not None,'Playlist did not open'
        adb('shell','input','keyevent','4','4')
        root=dump()
        safe=find(root,'歌单广场') is not None or find(root,'听见，新的喜欢。') is not None
        checks['rapid_back_round_'+str(index+1)]=safe
        assert safe,'Rapid back produced a blank or unexpected screen'
        if find(root,'歌单广场') is not None:
            tap(find(root,'返回'));root=dump()
        assert find(root,'听见，新的喜欢。') is not None,'Discover did not restore'
finally:
    (out/'navigation-checks.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
print(json.dumps(checks))
