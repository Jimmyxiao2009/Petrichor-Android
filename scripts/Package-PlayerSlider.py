"""Deliver the checked Release binary, corresponding source and native UI evidence."""
from pathlib import Path
import hashlib,html,json,re,shutil,subprocess,zipfile
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/"artifacts/slider-review";ADB=r"C:\tmp\PetrichorAndroid\sdk\platform-tools\adb.exe";PKG="app.petrichor.music"
def adb(*args):return subprocess.run([ADB,"-s","emulator-5554",*args],check=True,capture_output=True).stdout
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
apk=ROOT/"artifacts/android/Petrichor-Android.apk";package=adb("shell","dumpsys","package",PKG).decode("utf-8","replace")
installed=Path(r"C:\tmp\PetrichorAndroid\slider-installed.apk");adb("pull",adb("shell","pm","path",PKG).decode().strip().split("package:")[1],str(installed))
pid=adb("shell","pidof",PKG).decode().strip().split()[0];logs=adb("logcat","-d","--pid="+pid,"-v","threadtime").decode("utf-8","replace")
(OUT/"release-process-logcat.txt").write_text(logs,encoding="utf-8");(OUT/"release-package.txt").write_text(package,encoding="utf-8")
fatal=re.findall(r"^.*(?:FATAL EXCEPTION|Fatal signal|SIGABRT|SIGSEGV|Unhandled exception|ANR in app\.petrichor\.music).*$",logs,re.M)
with zipfile.ZipFile(ROOT/"artifacts/backups/maui-before-1.0.8.zip") as backup:
    core=[p for p in (ROOT/"src/Petrichor.Core").rglob("*") if p.is_file() and not {"bin","obj"}&set(p.parts)]
    unchanged=all(backup.read(p.relative_to(ROOT).as_posix())==p.read_bytes() for p in core)
checks=json.loads((OUT/"checks.json").read_text(encoding="utf-8"));visual=json.loads((OUT/"visual-measurements.json").read_text(encoding="utf-8"))
report={"versionName":re.search(r"versionName=([^\s]+)",package).group(1),"versionCode":int(re.search(r"versionCode=(\d+)",package).group(1)),"bytes":apk.stat().st_size,"sha256":sha(apk),"installedSha256":sha(installed),"debuggable":"DEBUGGABLE" in package,"fatalProcessLogMatches":fatal,"coreSourceUnchanged":unchanged,"releaseUiChecks":len(checks),"coreChecks":119,"visualMeasurements":visual}
assert report["versionName"]=="1.0.8" and report["versionCode"]==9 and not report["debuggable"] and not fatal and unchanged and sha(apk)==sha(installed) and len(checks)==18 and visual["wavePhaseChanges"]
shutil.copyfile(apk,ROOT/"artifacts/android/Petrichor-1.0.8.apk");(OUT/"manifest.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
details=[("05-paused-half-release-detail","暂停时的轨道和 6 dp 留白"),("03-drag-preview-release-detail","按住变细与时间预览"),("06-playing-wave-release-detail","播放时的动态波浪")]
screens=[("06-playing-wave-release","播放器实际运行"),("03-drag-preview-release","拖动预览"),("08-functions-after-seek-release","原有下滑菜单")]
def cards(items):return "".join(f'<figure><a href="{name}.png"><img loading="lazy" src="{name}.png" alt="{title}"></a><figcaption>{title}</figcaption></figure>' for name,title in items)
video='<video controls preload="metadata" src="player-slider-motion.mp4"></video>' if (OUT/"player-slider-motion.mp4").exists() else ""
page=f'''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Petrichor 1.0.8 进度条</title><style>body{{margin:0;background:#f5f5fa;color:#25252c;font:16px/1.65 system-ui,sans-serif}}main{{max-width:1180px;margin:auto;padding:32px 24px}}h1{{font-size:30px;margin:0}}a{{color:#495d92}}.details,.screens{{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:20px;margin:24px 0}}figure{{margin:0;padding:12px;border-radius:20px;background:white}}img{{width:100%;border-radius:10px}}.screens img{{max-height:650px;object-fit:contain}}figcaption{{padding:10px 4px}}video{{width:100%;max-height:640px;background:#151519;border-radius:20px}}code{{font-size:12px;word-break:break-all}}li{{margin:6px 0}}</style><main><h1>Petrichor 1.0.8 · 音乐进度条</h1><p>真实 Release APK · Android 15 模拟器 · 90 秒本地 FLAC 测试音频。下方细节直接裁自原生截图。</p><p><a href="../android/Petrichor-1.0.8.apk">安装 APK</a> · <a href="../android/Petrichor-1.0.8-source.zip">对应源码与许可</a> · <a href="../../docs/UI-1.0.8.md">实现、Google 文档与验证范围</a></p><div class="details">{cards(details)}</div>{video}<details><summary>18 项 Release 交互检查、119 项后台检查通过</summary><ul>{''.join('<li>'+html.escape(c)+'</li>' for c in checks)}</ul><p>截图像素包含取整及抗锯齿边缘，可见两侧留白各约 {visual['visibleGapDp'][0]:.2f} dp；设计值为 6 dp。暂停轨道跨度 {visual['pausedRailSpanPixels']} px，播放波浪跨度 {visual['playingWaveSpanPixels']} px，相邻帧波形发生变化。</p><p>已安装包与交付包 SHA256 一致：<code>{report['sha256']}</code>。未新增会员在线无损或真实账号写入验证；未作完整 TalkBack 或性能基准验收。</p></details><div class="screens">{cards(screens)}</div></main></html>'''
(OUT/"index.html").write_text(page,encoding="utf-8")
with zipfile.ZipFile(ROOT/"artifacts/android/Petrichor-1.0.8-source.zip","w",zipfile.ZIP_DEFLATED) as z:
    for folder in ["src/Petrichor.Android","src/Petrichor.Core","tests/Petrichor.Core.Tests"]:
        for p in sorted((ROOT/folder).rglob("*")):
            if p.is_file() and not {"bin","obj",".git"}&set(p.parts):z.write(p,p.relative_to(ROOT).as_posix())
    for name in ["README.md","Petrichor.Android.sln","docs/UI-1.0.8.md","design-qa.md","scripts/Build-Android.ps1","scripts/Verify-PlayerFunctions.py","scripts/Verify-PlayerSlider.py","scripts/Measure-PlayerSlider.py","scripts/Record-PlayerSlider.py","scripts/Package-PlayerSlider.py"]:z.write(ROOT/name,name)
print(json.dumps(report,ensure_ascii=False,indent=2))
