"""Archive the checked release, active source and its verification evidence."""
from pathlib import Path
import difflib,hashlib,html,json,re,shutil,subprocess,zipfile
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/"artifacts/playback-fixes"
ADB=r"C:\tmp\PetrichorAndroid\sdk\platform-tools\adb.exe";PKG="app.petrichor.music"
def adb(*args):return subprocess.run([ADB,"-s","emulator-5554",*map(str,args)],check=True,capture_output=True,timeout=50).stdout
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
apk=ROOT/"artifacts/android/Petrichor-Android.apk"
installed=OUT/"installed-final.apk"
path=adb("shell","pm","path",PKG).decode().strip().split("package:")[1]
adb("pull",path,installed)
package=adb("shell","dumpsys","package",PKG).decode("utf-8","replace")
(OUT/"release-package.txt").write_text(package,encoding="utf-8")
assert sha(apk)==sha(installed)
assert "versionName=1.0.10" in package and "versionCode=11" in package and "DEBUGGABLE" not in package
device=json.loads((OUT/"device-checks.json").read_text(encoding="utf-8"))
final=json.loads((OUT/"final-device-checks.json").read_text(encoding="utf-8"))
background=json.loads((OUT/"background-samples.json").read_text(encoding="utf-8"))
assert len(device)==12 and len(final)>=3 and background["samples"][-1]["seconds"]>=120
assert "All 128 checks passed." in (OUT/"core-tests.log").read_text(encoding="utf-8-sig")
assert "All 26 playback lifecycle checks passed." in (OUT/"playback-tests.log").read_text(encoding="utf-8-sig")
pid=adb("shell","pidof",PKG).decode().strip().split()[0]
logs=adb("logcat","-d","--pid="+pid,"-v","threadtime").decode("utf-8","replace")
(OUT/"final-logcat.txt").write_text(logs,encoding="utf-8")
assert not re.search(r"FATAL EXCEPTION|Fatal signal|Unhandled exception|ANR in",logs)
power_save=json.loads((OUT/"power-save/result.json").read_text(encoding="utf-8"))
assert power_save["durationSeconds"]>=360 and power_save["samples"]==18
manifest={"systemPowerSaver":power_save,"versionName":"1.0.10","versionCode":11,"sha256":sha(apk),"installedSha256":sha(installed),"bytes":apk.stat().st_size,"release":True,"coreChecks":128,"playbackLifecycleChecks":26,"releaseDeviceChecks":device,"finalArtworkChecks":final,"background":background,
"testScope":"Android 15 disposable emulator; six minutes of system Battery Saver with background/screen-off network audio and diagnostic export; no OEM or battery benchmark"}
(OUT/"manifest.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding="utf-8")
shutil.copyfile(apk,ROOT/"artifacts/android/Petrichor-1.0.10.apk")
# Keep a compact review diff against the source copies saved before the edits.
patch=[]
for before in sorted((OUT/"baseline").rglob("*")):
    if not before.is_file():continue
    relative=before.relative_to(OUT/"baseline")
    after=ROOT/relative
    if after.exists():
        patch.extend(difflib.unified_diff(before.read_text(encoding="utf-8-sig").splitlines(keepends=True),after.read_text(encoding="utf-8-sig").splitlines(keepends=True),fromfile="a/"+relative.as_posix(),tofile="b/"+relative.as_posix()))
(OUT/"changes.patch").write_text("".join(patch),encoding="utf-8")
source=ROOT/"artifacts/android/Petrichor-1.0.10-source.zip"
with zipfile.ZipFile(source,"w",zipfile.ZIP_DEFLATED) as archive:
    for folder in ("src/Petrichor.Android","src/Petrichor.Core","tests/Petrichor.Core.Tests","tests/Petrichor.Playback.Tests"):
        for p in sorted((ROOT/folder).rglob("*")):
            if p.is_file() and not {"bin","obj",".git","__pycache__"}&set(p.parts):archive.write(p,p.relative_to(ROOT).as_posix())
    for name in ("README.md","Petrichor.Android.sln","docs/PLAYBACK-1.0.10.md","scripts/Build-Android.ps1","scripts/Verify-PlaybackFixes.py","scripts/Verify-BackgroundPlayback.py","scripts/Verify-PlaybackFinal.py","scripts/Package-PlaybackFixes.py","scripts/Verify-PowerSavePlayback.py"):
        archive.write(ROOT/name,name)
with zipfile.ZipFile(source) as archive:assert archive.testzip() is None
videos="".join('<p>'+title+'</p><video controls preload="metadata" src="'+name+'"></video>' for name,title in (("final-artwork-motion.mp4","最终包封面转场"),("navigation-motion.mp4","主页面与详情页切换")))
page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Petrichor 1.0.10 检查记录</title><style>body{margin:24px auto;padding:0 20px;max-width:1000px;font:16px/1.7 system-ui;background:#f7f5fa;color:#24222a}video,img{max-width:100%;max-height:680px}code{word-break:break-all}</style><h1>Petrichor 1.0.10</h1><p><a href="../android/Petrichor-1.0.10.apk">安装包</a> · <a href="../android/Petrichor-1.0.10-source.zip">源码与许可</a> · <a href="../../docs/PLAYBACK-1.0.10.md">修改与验证范围</a></p><p>154 项自动检查、12 项 Release 交互检查通过，另完成最终封面转场专项验证。Android 系统省电开启时完成 6 分钟后台熄屏网络播放与日志导出检查；早先两分钟检查的后台绘制帧数保持不变。模拟器验证不代替厂商真机和耗电测试。</p>'''+videos+'<p>系统媒体界面实际截图：</p><img src="system-media-final.png"><p>系统省电长测后的诊断页面：</p><img src="power-save/diagnostics-screen.png"><p><a href="power-save/result.json">6 分钟省电测试结果</a> · <a href="power-save/exported-diagnostics.txt">实际导出的诊断日志</a></p><p>APK SHA256：<code>'+sha(apk)+'</code></p><p><a href="manifest.json">完整验证清单</a></p></html>'
(OUT/"index.html").write_text(page,encoding="utf-8")
print(json.dumps({"apk":str(apk),"sha256":sha(apk),"sourceBytes":source.stat().st_size,"deviceChecks":len(device),"finalChecks":len(final),"backgroundSeconds":background["samples"][-1]["seconds"],"systemPowerSaveSeconds":power_save["durationSeconds"]},ensure_ascii=False,indent=2))