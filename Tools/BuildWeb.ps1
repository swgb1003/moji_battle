param(
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.0.61f1/Editor/Unity.exe',
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputPath = '',
    [string]$LogPath = ''
)
$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
if (-not $OutputPath) { $OutputPath = Join-Path $ProjectPath 'Builds/WebGL' }
if (-not $LogPath) { $LogPath = Join-Path $ProjectPath 'Logs/web-build.log' }
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
$LogPath = [IO.Path]::GetFullPath($LogPath)
New-Item -ItemType Directory -Force (Split-Path -Parent $LogPath) | Out-Null
$previousOutput = $env:MOJI_WEB_OUTPUT
try {
    $env:MOJI_WEB_OUTPUT = $OutputPath
    $arguments = '-batchmode -projectPath "{0}" -buildTarget WebGL -executeMethod MojiBattle.EditorTools.WebBuild.Build -logFile "{1}" -quit' -f $ProjectPath, $LogPath
    $process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Write-Output "Unity Web build started (PID $($process.Id)). Log: $LogPath"
    $process.WaitForExit()
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $OutputPath 'build-info.json'))) {
        throw "Web build failed. See $LogPath (exit $($process.ExitCode))."
    }
    Write-Output "Web build ready: $OutputPath"
} finally {
    $env:MOJI_WEB_OUTPUT = $previousOutput
}
