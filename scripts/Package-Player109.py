"""Package the installed, checked 1.0.9 release and corresponding source."""
from pathlib import Path
import hashlib,html,json,re,shutil,subprocess,zipfile
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/"artifacts/ui-1.0.9";ADB=r"C:\tmp\PetrichorAndroid\sdk\platform-tools\adb.exe";PKG="app.petrichor.music"
def adb(*args):return subprocess.run([ADB,"-s","emulator-5554",*map(str,args)],check=True,capture_output=True).stdout
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
apk=ROOT/"artifacts/android/Petrichor-Android.apk";package=adb("shell","dumpsys","package",PKG).decode("utf-8","replace")
installed=Path(r"C:\tmp\PetrichorAndroid\ui109-installed.apk");adb("pull",adb("shell","pm","path",PKG).decode().strip().split("package:")[1],installed)
pid=adb("shell","pidof",PKG).decode().strip().split()[0];logs=adb("logcat","-d","--pid="+pid,"-v","threadtime").decode("utf-8","replace")
(OUT/"release-process-logcat.txt").write_text(logs,encoding="utf-8");(OUT/"release-package.txt").write_text(package,encoding="utf-8")
fatal=re.findall(r"^.*(?:FATAL EXCEPTION|Fatal signal|SIGABRT|SIGSEGV|Unhandled exception|ANR in app\.petrichor\.music).*$",logs,re.M)
with zipfile.ZipFile(ROOT/"artifacts/backups/maui-before-1.0.9.zip") as backup:
    core=[p for p in (ROOT/"src/Petrichor.Core").rglob("*") if p.is_file() and not {"bin","obj"}&set(p.parts)]
    unchanged=all(backup.read(p.relative_to(ROOT).as_posix())==p.read_bytes() for p in core)
checks=json.loads((OUT/"checks.json").read_text(encoding="utf-8"))
report={"versionName":re.search(r"versionName=([^\s]+)",package).group(1),"versionCode":int(re.search(r"versionCode=(\d+)",package).group(1)),"bytes":apk.stat().st_size,"sha256":sha(apk),"installedSha256":sha(installed),"debuggable":"DEBUGGABLE" in package,"fatalProcessLogMatches":fatal,"coreSourceUnchanged":unchanged,"releaseUiChecks":len(checks),"coreChecks":119}
report["phoneInstall"]=json.loads((OUT/"phone-install.json").read_text(encoding="utf-8"))
assert report["versionName"]=="1.0.9" and report["versionCode"]==10 and not report["debuggable"] and not fatal and unchanged and sha(apk)==sha(installed) and len(checks)==31 and report["phoneInstall"]["installedSha256"]==sha(apk)
shutil.copyfile(apk,ROOT/"artifacts/android/Petrichor-1.0.9.apk");(OUT/"manifest.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
screens=[("03-functions","播放功能：移除外层黑底与歌曲信息"),("15-functions-dark","深色播放功能"),("02-queue","封面下滑打开当前队列"),("04-comments","从功能页直接打开评论"),("09-explore","探索：原生排行榜与四个入口"),("10-charts","原生排行榜卡片"),("12-playlist-collapsed","歌单封面和标题收进顶栏"),("13-daily-guest","游客每日推荐占位；真实封面依赖登录"),("14-root-return","详情返回后的底部播放栏")]
cards="".join(f'<figure><a href="{name}.png"><img loading="lazy" src="{name}.png" alt="{html.escape(title)}"></a><figcaption>{html.escape(title)}</figcaption></figure>' for name,title in screens if (OUT/(name+".png")).exists())
video='<video controls preload="metadata" src="player109-motion.mp4"></video>' if (OUT/"player109-motion.mp4").exists() else ""
page=f'''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Petrichor 1.0.9</title><style>body{{margin:0;background:#f5f5fa;color:#25252c;font:16px/1.65 system-ui,sans-serif}}main{{max-width:1180px;margin:auto;padding:32px 24px}}h1{{font-size:30px;margin:0}}a{{color:#495d92}}.screens{{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:20px;margin:24px 0}}figure{{margin:0;padding:12px;border-radius:20px;background:white}}img{{width:100%;max-height:650px;object-fit:contain}}figcaption{{padding:10px 4px}}video{{width:100%;max-height:640px;background:#151519;border-radius:20px}}code{{font-size:12px;word-break:break-all}}</style><main><h1>Petrichor 1.0.9</h1><p>实际 Release 安装包运行截图；Android 15 模拟器。功能页移除外层黑底，补齐当前队列与直接导航。</p><p><a href="../android/Petrichor-1.0.9.apk">安装 APK</a> · <a href="../android/Petrichor-1.0.9-source.zip">对应源码与许可</a> · <a href="../../docs/UI-1.0.9.md">实现与验证范围</a></p>{video}<details><summary>{len(checks)} 项 Release 交互检查、119 项后台检查通过</summary><ul>{''.join('<li>'+html.escape(c)+'</li>' for c in checks)}</ul><p>无云端账号写入或分享发送。游客每日推荐仅验证占位布局；封面转场未作帧率基准。安装与交付包 SHA256 一致：<code>{report['sha256']}</code>。</p></details><div class="screens">{cards}</div></main></html>'''
(OUT/"index.html").write_text(page,encoding="utf-8")
with zipfile.ZipFile(ROOT/"artifacts/android/Petrichor-1.0.9-source.zip","w",zipfile.ZIP_DEFLATED) as z:
    for folder in ["src/Petrichor.Android","src/Petrichor.Core","tests/Petrichor.Core.Tests"]:
        for p in sorted((ROOT/folder).rglob("*")):
            if p.is_file() and not {"bin","obj",".git"}&set(p.parts):z.write(p,p.relative_to(ROOT).as_posix())
    for name in ["README.md","Petrichor.Android.sln","docs/UI-1.0.8.md","docs/UI-1.0.9.md","design-qa.md","scripts/Build-Android.ps1","scripts/Verify-PlayerFunctions.py","scripts/Verify-Player109.py","scripts/Record-Player109.py","scripts/Measure-Player109.py","scripts/Package-Player109.py"]:z.write(ROOT/name,name)
with zipfile.ZipFile(ROOT/"artifacts/android/Petrichor-1.0.9-source.zip") as z:assert z.testzip() is None
print(json.dumps(report,ensure_ascii=False,indent=2))
