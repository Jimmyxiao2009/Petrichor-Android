"""Verify installed Release identity and capture process-specific crash evidence."""
import hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'artifacts/cache-review';ADB=r'C:\tmp\PetrichorAndroid\sdk\platform-tools\adb.exe';PKG='app.petrichor.music'
def adb(*args):return subprocess.check_output([ADB,'-s','emulator-5554',*args])
apk=ROOT/'artifacts/android/Petrichor-Android.apk';expected=hashlib.sha256(apk.read_bytes()).hexdigest().upper()
installed_path=adb('shell','pm','path',PKG).decode().strip().split('package:')[1]
installed=Path(r'C:\tmp\PetrichorAndroid\cache-installed.apk');adb('pull',installed_path,str(installed));actual=hashlib.sha256(installed.read_bytes()).hexdigest().upper()
package=adb('shell','dumpsys','package',PKG).decode('utf-8','replace');(OUT/'release-package.txt').write_text(package,encoding='utf-8')
pid=adb('shell','pidof',PKG).decode().strip().split()[0]
logs=adb('logcat','-d','--pid='+pid,'-v','threadtime').decode('utf-8','replace');(OUT/'release-process-logcat.txt').write_text(logs,encoding='utf-8')
crash=adb('logcat','-d','-b','crash','-v','threadtime').decode('utf-8','replace');(OUT/'release-crash-logcat.txt').write_text(crash,encoding='utf-8')
fatal=re.findall(r'^.*(?:FATAL EXCEPTION|Fatal signal|SIGABRT|SIGSEGV|Unhandled exception|Assertion at|ANR in app\.petrichor\.music).*$',logs,re.M)
report={'versionName':re.search(r'versionName=([^\s]+)',package).group(1),'versionCode':int(re.search(r'versionCode=(\d+)',package).group(1)),'bytes':apk.stat().st_size,'sha256':expected,'installedSha256':actual,'installedMatches':expected==actual,'debuggable':'DEBUGGABLE' in package,'fatalProcessLogMatches':fatal,'wifiRestored':adb('shell','settings','get','global','wifi_on').decode().strip()=='1','mobileDataRestored':adb('shell','settings','get','global','mobile_data').decode().strip()=='1'}
(OUT/'release-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
assert report['versionName']=='1.0.5' and report['versionCode']==6 and report['installedMatches'] and not report['debuggable'] and not fatal
print(json.dumps(report,ensure_ascii=False))
