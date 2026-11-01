$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
$edge = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
$captureRoot = Join-Path $PWD 'artifacts/design'
New-Item -ItemType Directory -Force -Path $captureRoot | Out-Null
$profilePath = Join-Path $captureRoot 'browser-profile'
$pageUri = ([Uri](Join-Path $PWD 'design/android/index.html')).AbsoluteUri
$screens = Get-Content -LiteralPath 'design/android/screens.json' -Raw | ConvertFrom-Json
for ($start=0; $start -lt $screens.Count; $start+=4) {
    $last=[Math]::Min($start+3,$screens.Count-1)
    $slugs = ($screens[$start..$last].slug -join ',')
    $theme = if ($start % 8 -eq 4) { '&palette=teal&dark=true' } else { '&palette=rose&dark=false' }
    $target = $pageUri+'?screens='+$slugs+$theme
    $png = Join-Path $captureRoot ('screens-'+([int]($start/4)).ToString('00')+'.png')
    $cliArgs = @('--headless=new','--no-first-run','--disable-gpu','--hide-scrollbars','--window-size=1680,1060','--virtual-time-budget=4000',('--user-data-dir="'+$profilePath+'"'),('--screenshot="'+$png+'"'),('"'+$target+'"'))
    $process = Start-Process -FilePath $edge -ArgumentList $cliArgs -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput (Join-Path $captureRoot 'capture.out.log') -RedirectStandardError (Join-Path $captureRoot 'capture.err.log')
    if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $png)) { throw "Capture failed for $slugs" }
    Write-Output ('CAPTURED '+$slugs)
}
