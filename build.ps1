[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePath = Join-Path $projectRoot 'src\CodexUsagePill.cs'
$iconPath = Join-Path $projectRoot 'assets\CodexUsagePill.ico'
$outputDirectory = Join-Path $projectRoot 'dist'
$outputPath = Join-Path $outputDirectory 'CodexUsagePill.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The .NET Framework C# compiler was not found. Install .NET Framework 4.x.'
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$references = @(
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Web.Extensions.dll',
    '/reference:System.Core.dll'
)

& $compiler /nologo /optimize+ /target:winexe "/out:$outputPath" "/win32icon:$iconPath" $references $sourcePath
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

$hash = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $outputDirectory 'SHA256SUMS.txt') -Value "$hash  CodexUsagePill.exe" -Encoding Ascii

Write-Host "Built $outputPath"
Write-Host "SHA256 $hash"
