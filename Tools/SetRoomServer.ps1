param(
    [Parameter(Mandatory = $true)][string]$Url,
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot)
)
$ErrorActionPreference = 'Stop'
$taskEndpoint = $null
if (-not [Uri]::TryCreate($Url.Trim(), [UriKind]::Absolute, [ref]$taskEndpoint) -or
    $taskEndpoint.Scheme -ne 'https' -or $taskEndpoint.IsLoopback -or
    $taskEndpoint.UserInfo -or $taskEndpoint.Query -or $taskEndpoint.Fragment) {
    throw 'Specify the public HTTPS room server URL, without credentials, query or fragment.'
}
$taskRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
if (-not (Test-Path -LiteralPath (Join-Path $taskRoot 'ProjectSettings\ProjectVersion.txt'))) {
    throw 'The target is not a Unity project.'
}
$taskUrl = $taskEndpoint.AbsoluteUri.TrimEnd('/')
$taskHealth = Invoke-RestMethod -Uri ($taskUrl + '/health') -TimeoutSec 90
if ($taskHealth.ok -ne $true -or $taskHealth.version -ne 'moji-fighter-x-rooms-v3') {
    throw 'The URL is not a compatible MOJI Fighter X room server. Configuration was not changed.'
}
$taskDirectory = Join-Path $taskRoot 'Assets\Resources\Online'
New-Item -ItemType Directory -Path $taskDirectory -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $taskDirectory 'RoomServer.txt'), $taskUrl + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
Write-Output "Bundled room endpoint: $taskUrl"
Write-Output 'Rebuild the game to distribute the endpoint. Connection settings will be hidden automatically.'
