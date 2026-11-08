param(
    [string]$AndroidSdkDirectory='C:\Program Files (x86)\Android\android-sdk',
    [string]$JavaSdkDirectory='C:\Program Files\Android\openjdk\jdk-21.0.8'
)
$ErrorActionPreference='Stop'
$script:failures=0
function Check([string]$Name,[bool]$Ok,[string]$Detail=''){
    $suffix=''
    if($Detail -ne ''){$suffix='  ('+$Detail+')'}
    if($Ok){Write-Output ('PASS  '+$Name+$suffix)}
    else{$script:failures++;Write-Output ('FAIL  '+$Name+$suffix)}
}

$dotnet=(Get-Command dotnet -ErrorAction SilentlyContinue).Source
if(-not $dotnet){$dotnet='C:\Program Files\dotnet\dotnet.exe'}
Check '.NET SDK 10' (Test-Path -LiteralPath $dotnet) $dotnet
if(Test-Path -LiteralPath $dotnet){
    $sdkVersion=(& $dotnet --version 2>$null | Select-Object -First 1)
    Check '.NET SDK 版本 >= 10' ($sdkVersion -match '^(\d+)\.' -and [int]$Matches[1] -ge 10) $sdkVersion
    $workloads=(& $dotnet workload list 2>$null) -join "`n"
    Check 'maui-android 工作负载' ($workloads -match 'maui-android') 'dotnet workload install maui-android'
}

$java=Join-Path $JavaSdkDirectory 'bin/java.exe'
Check 'JDK 21 目录' (Test-Path -LiteralPath $java) $JavaSdkDirectory
if(Test-Path -LiteralPath $java){
    $javaVersion=(& $java -version 2>&1 | Select-Object -First 1) -join ''
    # 版本号需为三段式；四段版本（如 21.0.13.0.2）会让 .NET Android 探测失败并报 XA0033
    Check 'JDK 版本 21（三段式版本号）' ($javaVersion -match 'version "21\.\d+\.\d+"') $javaVersion
}

Check 'Android SDK 目录' (Test-Path -LiteralPath $AndroidSdkDirectory) $AndroidSdkDirectory
Check 'platforms/android-36' (Test-Path -LiteralPath (Join-Path $AndroidSdkDirectory 'platforms/android-36'))
Check 'build-tools/36.0.0' (Test-Path -LiteralPath (Join-Path $AndroidSdkDirectory 'build-tools/36.0.0'))
$adb=Join-Path $AndroidSdkDirectory 'platform-tools/adb.exe'
Check 'platform-tools/adb' (Test-Path -LiteralPath $adb)
if(Test-Path -LiteralPath $adb){
    (& $adb version 2>$null | Select-Object -First 1) | ForEach-Object {Write-Output ('      '+$_)}
}

$pwsh=(Get-Command pwsh -ErrorAction SilentlyContinue).Source
Check 'PowerShell 7 (pwsh)' ([bool]$pwsh) $pwsh

# 环境变量会被 MSBuild 映射为同名属性；非 SemVer 的 Version 会让 NuGet 还原静默失败。
# 只检查持久化作用域：当前进程可能仍继承删除前的旧值，重开终端即可。
$envVersion=[Environment]::GetEnvironmentVariable('Version','Machine')
if([string]::IsNullOrWhiteSpace($envVersion)){$envVersion=[Environment]::GetEnvironmentVariable('Version','User')}
Check '未设置 Version 环境变量' ([string]::IsNullOrWhiteSpace($envVersion)) ('当前值: '+$envVersion)

Write-Output ''
if($script:failures -eq 0){
    Write-Output '开发环境检查通过。'
    exit 0
}
Write-Output ($script:failures.ToString()+' 项未通过，见 docs/DEV-ENVIRONMENT.md。')
exit 1
