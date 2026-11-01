$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
$manifest = Get-Content -LiteralPath 'design/android/canvas.json' -Raw | ConvertFrom-Json
$screens = Get-Content -LiteralPath 'design/android/screens.json' -Raw | ConvertFrom-Json
$targets = [ordered]@{}
foreach ($screen in $screens) {
    $record = $manifest.($screen.slug)
    if (!$record) { throw ('Missing canvas record: '+$screen.slug) }
    $contextFiles = @('.superdesign/design-system.md', $screen.file)
    $fingerprints = [ordered]@{}
    foreach ($file in $contextFiles) { $fingerprints[$file] = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() }
    $drafts = [ordered]@{}
    $drafts[$record.draftId] = [ordered]@{ title=$screen.title; role='new Android MD3 design'; status='awaiting-user-approval'; previewUrl=$record.previewUrl }
    if ($record.currentVersion) { $drafts[$record.draftId]['currentVersion']=$record.currentVersion }
    $targets[('android/'+$screen.slug)] = [ordered]@{
        targetKind='new-target'
        projectId='f68bfd1a-53a5-4dca-b67c-6ea662228783'
        activeDraftId=$record.draftId
        designSystemPath='.superdesign/design-system.md'
        contextFiles=$contextFiles
        fingerprints=$fingerprints
        drafts=$drafts
        updatedAt=[DateTimeOffset]::UtcNow.ToString('o')
    }
}
$state = [ordered]@{ schemaVersion=1; targets=$targets }
$path=Join-Path $PWD '.superdesign/resume.json'
[IO.File]::WriteAllText($path+'.tmp',($state|ConvertTo-Json -Depth 15),[Text.UTF8Encoding]::new($false))
Move-Item -LiteralPath ($path+'.tmp') -Destination $path -Force
Write-Output ('Saved '+$targets.Count+' design targets')
