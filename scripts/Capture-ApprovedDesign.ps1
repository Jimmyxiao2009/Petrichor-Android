param([string[]]$Screens=@('discover','search-songs','search','library-guest','login','qr-login','settings','downloads','podcasts','podcast','playlist','player','queue','comments','artist','album','empty-search'))
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$capture=Join-Path $root 'artifacts/ui-refinement'
$renderRoot='C:\tmp\PetrichorAndroid\reference-render'
New-Item -ItemType Directory -Force $capture,$renderRoot | Out-Null
foreach($screen in $Screens){
    if($screen -notmatch '^[a-z-]+$'){throw 'Invalid screen name'}
    $source=Join-Path $root ('design/android/'+$screen+'.html')
    if(!(Test-Path -LiteralPath $source)){throw 'Missing approved design'}
    $uri=([Uri]$source).AbsoluteUri+'?palette=rose&dark=false'
    $wrapper=Join-Path $renderRoot 'frame.html'
    '<!doctype html><meta charset="utf-8"><style>body{margin:0}iframe{display:block;width:412px;height:892px;border:0}</style><iframe src="'+$uri+'"></iframe>' | Set-Content -LiteralPath $wrapper -Encoding utf8
    $png=Join-Path $renderRoot ($screen+'.png')
    $arguments=@('--headless=new','--no-first-run','--disable-gpu','--hide-scrollbars','--window-size=520,940','--force-device-scale-factor=1','--virtual-time-budget=5000',('--user-data-dir='+$renderRoot+'\profile'),('--screenshot='+$png),([Uri]$wrapper).AbsoluteUri)
    $process=Start-Process 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe' -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru -RedirectStandardError (Join-Path $renderRoot 'render.err.log') -RedirectStandardOutput (Join-Path $renderRoot 'render.out.log')
    if($process.ExitCode -ne 0){throw 'Reference render failed'}
    python -c 'from PIL import Image; import sys; Image.open(sys.argv[1]).crop((0,0,412,892)).save(sys.argv[2])' $png (Join-Path $capture ('reference-'+$screen+'.png'))
    if($LASTEXITCODE -ne 0){throw 'Reference crop failed'}
    Write-Output ('CAPTURED '+$screen)
}
