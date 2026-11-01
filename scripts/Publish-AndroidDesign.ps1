$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
$projectId = 'f68bfd1a-53a5-4dca-b67c-6ea662228783'
$manifestPath = Join-Path $PWD 'design/android/canvas.json'
$manifest = @{}
if (Test-Path -LiteralPath $manifestPath) {
    $saved = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    foreach ($p in $saved.PSObject.Properties) { $manifest[$p.Name] = $p.Value }
}
$screens = Get-Content -LiteralPath 'design/android/screens.json' -Raw | ConvertFrom-Json
foreach ($screen in $screens) {
    $hash = (Get-FileHash -LiteralPath $screen.file -Algorithm SHA256).Hash
    if ($manifest.ContainsKey($screen.slug) -and $manifest[$screen.slug].sha256 -eq $hash) { continue }
    $draftId = if ($screen.slug -eq 'discover') { 'd529456e-4184-4e33-a3c1-d0b9852f52b1' } elseif ($manifest.ContainsKey($screen.slug)) { $manifest[$screen.slug].draftId } else { $null }
    $cliArgs = @('--yes','@superdesign/cli@latest','import-design-draft','--html-file',$screen.file,'--json')
    if ($draftId) { $cliArgs += @('--into',$draftId) }
    else { $cliArgs += @('--project-id',$projectId,'--title',('Petrichor · '+$screen.title),'--device','mobile') }
    $output = & npx @cliArgs
    if ($LASTEXITCODE -ne 0) { throw "Import failed for $($screen.slug): $output" }
    $result = ($output -join "`n") | ConvertFrom-Json
    if ($result.warnings) { $result.warnings | ConvertTo-Json -Depth 10 | Write-Output; throw "Review import warnings for $($screen.slug)" }
    $record = [ordered]@{ draftId=$result.draftId; title=$screen.title; currentVersion=$result.currentVersion; previewUrl=$result.previewUrl; projectUrl=$result.projectUrl; sha256=$hash }
    $manifest[$screen.slug]=$record
    $json = $manifest | ConvertTo-Json -Depth 10
    [IO.File]::WriteAllText($manifestPath+'.tmp',$json,[Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath ($manifestPath+'.tmp') -Destination $manifestPath -Force
    Write-Output ("IMPORTED " + $screen.slug + ' ' + $result.draftId)
}
Write-Output ("DONE: " + $manifest.Count + ' screens on canvas')
