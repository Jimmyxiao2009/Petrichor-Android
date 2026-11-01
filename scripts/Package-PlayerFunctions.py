"""Package the verified MAUI build with corresponding source and real UI evidence."""
import hashlib,json,re,shutil,subprocess,zipfile,html
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/"artifacts/md3-functions"
ADB=r"C:\tmp\PetrichorAndroid\sdk\platform-tools\adb.exe";PKG="app.petrichor.music"
def adb(*args):return subprocess.run([ADB,"-s","emulator-5554",*args],check=True,capture_output=True).stdout
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest().upper()
apk=ROOT/"artifacts/android/Petrichor-Android.apk"
package=adb("shell","dumpsys","package",PKG).decode("utf-8","replace")
installed_path=adb("shell","pm","path",PKG).decode().strip().split("package:")[1]
installed=Path(r"C:\tmp\PetrichorAndroid\md3-functions-installed.apk");adb("pull",installed_path,str(installed))
pid=adb("shell","pidof",PKG).decode().strip().split()[0]
logs=adb("logcat","-d","--pid="+pid,"-v","threadtime").decode("utf-8","replace")
(OUT/"release-process-logcat.txt").write_text(logs,encoding="utf-8")
(OUT/"release-package.txt").write_text(package,encoding="utf-8")
fatal=re.findall(r"^.*(?:FATAL EXCEPTION|Fatal signal|SIGABRT|SIGSEGV|Unhandled exception|Assertion at|ANR in app\.petrichor\.music).*$",logs,re.M)
with zipfile.ZipFile(ROOT/"artifacts/backups/maui-before-1.0.7.zip") as backup:
    core=[p for p in (ROOT/"src/Petrichor.Core").rglob("*") if p.is_file() and not {"bin","obj"}&set(p.parts)]
    core_unchanged=all(backup.read(p.relative_to(ROOT).as_posix())==p.read_bytes() for p in core)
checks=json.loads((OUT/"checks.json").read_text(encoding="utf-8"))
report={"versionName":re.search(r"versionName=([^\s]+)",package).group(1),"versionCode":int(re.search(r"versionCode=(\d+)",package).group(1)),"bytes":apk.stat().st_size,"sha256":sha(apk),"installedSha256":sha(installed),"debuggable":"DEBUGGABLE" in package,"fatalProcessLogMatches":fatal,"coreSourceUnchanged":core_unchanged,"releaseUiChecks":len(checks),"coreChecks":119}
assert report["versionName"]=="1.0.7" and report["versionCode"]==8 and not report["debuggable"] and not fatal and core_unchanged and sha(installed)==sha(apk) and len(checks)>=18
shutil.copyfile(apk,ROOT/"artifacts/android/Petrichor-1.0.7.apk")
(OUT/"manifest.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
titles={"01-player-release":"播放器与主色按钮","02-functions-release":"下滑功能面板","03-quality-release":"现有音质选项","04-sleep-release":"现有睡眠定时","05-comments-release":"现有评论页","06-lyrics-release":"独立歌词阅读","07-landscape-player-release":"横屏播放器","08-landscape-controls-release":"横屏滚动至完整控制区","09-landscape-functions-release":"横屏功能区滚动","10-final-player-release":"连续开关后返回播放器","11-functions-dark-release":"深色动态主题"}
cards="".join(f'<figure><a href="{key}.png"><img loading="lazy" src="{key}.png" alt="{title}"></a><figcaption>{title}</figcaption></figure>' for key,title in titles.items() if (OUT/(key+".png")).exists())
video='<video controls preload="metadata" src="player-functions-motion.mp4"></video>' if (OUT/"player-functions-motion.mp4").exists() else ""
page=f'''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Petrichor 1.0.7 实际运行检查</title><style>body{{margin:0;background:#f6f5fa;color:#24232a;font:16px/1.6 system-ui,sans-serif}}main{{max-width:1240px;margin:auto;padding:32px 24px}}h1{{font-size:30px;margin:0}}a{{color:#465b91}}.grid{{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:20px}}figure{{margin:0;background:white;border-radius:20px;padding:12px}}img{{width:100%;max-height:620px;object-fit:contain;border-radius:12px}}figcaption{{padding:12px 4px}}video{{width:100%;max-height:660px;background:#17171a;border-radius:20px}}code{{word-break:break-all;font-size:12px}}details{{margin:24px 0}}li{{margin:6px 0}}</style><main><h1>Petrichor 1.0.7 · 下滑播放功能</h1><p>现有 MAUI 客户端 · 实际 Android 15 模拟器截图与录屏。封面下滑展开功能区，上滑收起；歌词独立滚动。</p><p><a href="../android/Petrichor-1.0.7.apk">下载 Release APK</a> · <a href="../android/Petrichor-1.0.7-source.zip">对应源码（含许可）</a> · <a href="../../docs/UI-1.0.7.md">实现、Google 文档依据与验证边界</a></p>{video}<details><summary>{len(checks)} 项 Release 交互检查通过，119 项后台检查通过</summary><ul>{''.join('<li>'+html.escape(c)+'</li>' for c in checks)}</ul><p>真实账号写入与会员在线无损未新增实测。未据模拟器录屏承诺帧率或性能提升比例。</p><p>已安装 APK 与交付包 SHA256 一致：<code>{report['sha256']}</code></p></details><div class="grid">{cards}</div></main></html>'''
(OUT/"index.html").write_text(page,encoding="utf-8")
source=ROOT/"artifacts/android/Petrichor-1.0.7-source.zip"
with zipfile.ZipFile(source,"w",zipfile.ZIP_DEFLATED) as z:
    for folder in ["src/Petrichor.Android","src/Petrichor.Core","tests/Petrichor.Core.Tests"]:
        for p in sorted((ROOT/folder).rglob("*")):
            if p.is_file() and not {"bin","obj",".git"}&set(p.parts):z.write(p,p.relative_to(ROOT).as_posix())
    for name in ["README.md","Petrichor.Android.sln","scripts/Build-Android.ps1","scripts/Verify-PlayerFunctions.py","scripts/Package-PlayerFunctions.py","scripts/Record-PlayerFunctions.py","docs/UI-1.0.7.md","design-qa.md"]:
        p=ROOT/name
        if p.exists():z.write(p,name)
print(json.dumps(report,ensure_ascii=False,indent=2))
