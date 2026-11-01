param([ValidateSet('Debug','Release')][string]$Configuration='Release',[switch]$Test)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$native=Join-Path $workspace 'native/Petrichor'
$ownedDrive=$null
$previousJava=$env:JAVA_HOME
$previousOptions=$env:JAVA_TOOL_OPTIONS
try {
    $env:JAVA_HOME='C:\Program Files\Android\openjdk\jdk-21.0.8'
    # OpenJDK Unix-domain sockets fail on this host. A nonexistent socket directory selects TCP.
    $env:JAVA_TOOL_OPTIONS='-Djdk.net.unixdomain.tmpdir=C:/tmp/PetrichorAndroid/no-unix-socket'
    $buildRoot=$native
    if($native -match '[^\x00-\x7f]') {
        foreach($letter in @('N','O','V','W','X')) {
            $candidate=$letter+':'
            if(!(Test-Path -LiteralPath ($candidate+'\'))) {
                & subst.exe $candidate $native
                if($LASTEXITCODE -ne 0){throw '无法创建原生项目英文路径映射'}
                $ownedDrive=$candidate;$buildRoot=$candidate+'\';break
            }
        }
        if(!$ownedDrive){throw '原生构建可用盘符均被占用'}
    }
    $gradle=Get-ChildItem -LiteralPath (Join-Path $env:USERPROFILE '.gradle/wrapper/dists/gradle-8.14.3-bin') -Filter gradle.bat -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
    if(!$gradle){$gradle=Join-Path $buildRoot 'gradlew.bat'}
    $tasks=@(':app:assemble'+$Configuration)
    if($Test){$tasks+=@(':ncmapi:testDebugUnitTest',':app:testDebugUnitTest')}
    & $gradle -p $buildRoot @tasks --no-daemon --console=plain
    if($LASTEXITCODE -ne 0){throw '原生 Android 构建失败'}
    $apk=Join-Path $native ('app/build/outputs/apk/'+$Configuration.ToLower()+'/app-'+$Configuration.ToLower()+'.apk')
    $destination=Join-Path $workspace ('artifacts/android/Petrichor-Native-2.0.0'+$(if($Configuration -eq 'Debug'){'-debug'}else{''})+'.apk')
    Copy-Item -LiteralPath $apk -Destination $destination -Force
    Get-FileHash -LiteralPath $destination -Algorithm SHA256
} finally {
    if($ownedDrive){& subst.exe $ownedDrive /D}
    $env:JAVA_HOME=$previousJava
    $env:JAVA_TOOL_OPTIONS=$previousOptions
}
