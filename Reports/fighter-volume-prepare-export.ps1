$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$taskSnapshot = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'fighter-volume-source.json') | ConvertFrom-Json
$taskCommit = git -C $taskRoot rev-parse HEAD
if ($taskCommit -ne $taskSnapshot.baseCommit) { throw 'Source commit changed during build.' }
foreach ($taskFile in $taskSnapshot.files.PSObject.Properties) {
    $taskDigest = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $taskRoot $taskFile.Name)).Hash.ToLowerInvariant()
    if ($taskDigest -ne $taskFile.Value) { throw "Source changed during build: $($taskFile.Name)" }
}
$taskExport = (Resolve-Path -LiteralPath (Join-Path $taskRoot 'Builds/WebGL')).Path
$taskBuild = (Resolve-Path -LiteralPath (Join-Path $taskExport 'Build')).Path
$taskIndex = Join-Path $taskExport 'index.html'
$taskHtml = [IO.File]::ReadAllText($taskIndex)
$taskReferences = @([regex]::Matches($taskHtml,'Build/[A-Za-z0-9_.-]+\.(?:loader\.js|data\.gz|framework\.js\.gz|wasm\.gz)') | ForEach-Object { $_.Value.Substring(6) } | Sort-Object -Unique)
if ($taskReferences.Count -ne 4 -or $taskHtml.Contains('{{{')) { throw 'Invalid Unity export.' }
foreach ($taskOld in Get-ChildItem -LiteralPath $taskBuild -File) {
    if ($taskOld.Name -match '^[a-f0-9]{32}\.' -and $taskOld.Name -notin $taskReferences) {
        $taskTarget = [IO.Path]::GetFullPath($taskOld.FullName)
        if (-not $taskTarget.StartsWith($taskBuild + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Prune target outside build.' }
        Remove-Item -LiteralPath $taskTarget
    }
}
$taskFramework = @($taskReferences | Where-Object { $_.EndsWith('.framework.js.gz') })
if ($taskFramework.Count -ne 1) { throw 'Expected one framework file.' }
$taskFrameworkPath = Join-Path $taskBuild $taskFramework[0]
$taskNewName = (Get-FileHash -LiteralPath $taskFrameworkPath -Algorithm SHA256).Hash.ToLowerInvariant().Substring(0,32) + '.framework.js.gz'
$taskDestination = [IO.Path]::GetFullPath((Join-Path $taskBuild $taskNewName))
if (-not $taskDestination.StartsWith($taskBuild + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Move target outside build.' }
if ($taskNewName -ne $taskFramework[0]) {
    Move-Item -LiteralPath $taskFrameworkPath -Destination $taskDestination
    $taskHtml = $taskHtml.Replace($taskFramework[0],$taskNewName)
    [IO.File]::WriteAllText($taskIndex,$taskHtml,[Text.UTF8Encoding]::new($false))
}
foreach ($taskAsset in Get-ChildItem -LiteralPath $taskBuild -File) {
    if ($taskAsset.Length -ge 100000000) { throw "Vercel per-file size exceeded: $($taskAsset.Name)" }
}
$taskReview = Join-Path $taskExport 'reviews/fighter-volume'
New-Item -ItemType Directory -Path $taskReview -Force | Out-Null
$taskCapture = Join-Path $PSScriptRoot 'UI/FighterVolume'
Copy-Item -LiteralPath (Join-Path $taskCapture 'comparison.html') -Destination (Join-Path $taskReview 'index.html')
foreach ($taskReviewFile in @('before.mp4','after.mp4','comparison.mp4','full_battle.png')) {
    Copy-Item -LiteralPath (Join-Path $taskCapture $taskReviewFile) -Destination (Join-Path $taskReview $taskReviewFile)
}
$taskDelivery = @()
foreach ($taskPath in @((Get-ChildItem -LiteralPath $taskBuild -File),(Get-Item -LiteralPath $taskIndex),(Get-Item -LiteralPath (Join-Path $taskExport 'build-info.json')),(Get-ChildItem -LiteralPath $taskReview -File)) | ForEach-Object { $_ }) {
    $taskDelivery += @{file=$taskPath.FullName.Substring($taskExport.Length+1).Replace('\','/');bytes=$taskPath.Length;sha256=(Get-FileHash -LiteralPath $taskPath.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
}
@{baseCommit=$taskCommit;sourceFilesMatched=@($taskSnapshot.files.PSObject.Properties).Count;files=$taskDelivery} | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'fighter-volume-local-delivery.json')
Write-Output "Export ready; source hashes matched, framework cache key refreshed, comparison gallery included."
