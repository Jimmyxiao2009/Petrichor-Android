param(
    [ValidateSet('Debug','Release')][string]$Configuration='Release',
    [switch]$Rebuild,
    [string]$AndroidSdkDirectory='C:\Program Files (x86)\Android\android-sdk',
    [string]$JavaSdkDirectory='C:\Program Files\Android\openjdk\jdk-21.0.8'
)
$ErrorActionPreference='Stop'
$sourceRoot=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$buildRoot=$sourceRoot
$ownedDrive=$null
if (-not ('Petrichor.BuildDrive' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
namespace Petrichor { public static class BuildDrive {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern uint QueryDosDevice(string name, StringBuilder target, int size);
    public static string Target(string name) { var b=new StringBuilder(32768); return QueryDosDevice(name,b,b.Capacity)>0?b.ToString():""; }
} }
'@
}
try {
    if ($sourceRoot -match '[^\x00-\x7f]') {
        $letters=@('P','Q','R','S','T','U')
        $assets=Join-Path $sourceRoot 'src/Petrichor.Android/obj/project.assets.json'
        if(Test-Path -LiteralPath $assets) {
            $cached=Get-Content -LiteralPath $assets -Raw | ConvertFrom-Json
            $cachedPath=$cached.project.restore.projectPath
            if($cachedPath -match '^([P-U]):\\') {$preferred=$Matches[1];$letters=@($preferred)+@($letters|Where-Object{$_ -ne $preferred})}
        }
        foreach($letter in $letters) {
            $drive=$letter+':'
            if (!(Test-Path -LiteralPath ($drive+'\'))) {
                & subst.exe $drive $sourceRoot
                if ($LASTEXITCODE -ne 0) { throw '无法创建英文路径映射' }
                $ownedDrive=$drive; $buildRoot=$drive+'\'; break
            }
            $mapping = [Petrichor.BuildDrive]::Target($drive)
            if ($mapping -eq ('\??\'+$sourceRoot)) { $buildRoot=$drive+'\'; break }
        }
        if ($buildRoot -eq $sourceRoot) { throw 'P: 到 U: 均已占用，无法创建构建路径映射' }
    }
    Push-Location -LiteralPath $buildRoot
    try {
        $buildArguments=@('build','src/Petrichor.Android/Petrichor.Android.csproj','-c',$Configuration,"-p:AndroidSdkDirectory=$AndroidSdkDirectory","-p:JavaSdkDirectory=$JavaSdkDirectory",'-v:minimal')
        if($Rebuild){$buildArguments+='-t:Rebuild'}
        & dotnet @buildArguments
        if ($LASTEXITCODE -ne 0) { throw 'Android 构建失败' }
        $apk=Join-Path $buildRoot "src/Petrichor.Android/bin/$Configuration/net10.0-android/app.petrichor.music-Signed.apk"
        if (!(Test-Path -LiteralPath $apk)) { throw '构建未生成签名 APK' }
        $destination=Join-Path $sourceRoot 'artifacts/android'
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        Copy-Item -LiteralPath $apk -Destination (Join-Path $destination 'Petrichor-Android.apk.tmp') -Force
        Move-Item -LiteralPath (Join-Path $destination 'Petrichor-Android.apk.tmp') -Destination (Join-Path $destination 'Petrichor-Android.apk') -Force
        Get-FileHash -LiteralPath (Join-Path $destination 'Petrichor-Android.apk') -Algorithm SHA256
    } finally { Pop-Location }
} finally {
    if ($ownedDrive) { & subst.exe $ownedDrive /D }
}
