param(
    [string]$Serial,
    [string]$AdbPath='C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe'
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$apk=Join-Path $root 'artifacts/android/Petrichor-Android.apk'
if(!(Test-Path -LiteralPath $apk)){throw '请先运行 scripts/Build-Android.ps1'}
if(!(Test-Path -LiteralPath $AdbPath)){throw '请通过 -AdbPath 指定 Android SDK 的 adb.exe'}
$deviceArgs=@()
if($Serial){$deviceArgs=@('-s',$Serial)}
& $AdbPath @deviceArgs get-state
if($LASTEXITCODE -ne 0){throw '请连接 Android 设备并授权 USB 调试，或用 -Serial 指定模拟器'}
& $AdbPath @deviceArgs install -r $apk
if($LASTEXITCODE -ne 0){throw 'APK 安装失败，请查看 adb 返回的错误'}
& $AdbPath @deviceArgs shell monkey -p app.petrichor.music -c android.intent.category.LAUNCHER 1
