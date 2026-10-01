[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$outputDirectory = Join-Path $projectRoot 'dist'
$testPath = Join-Path $outputDirectory 'LayoutTests.exe'
$references = @('/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', '/reference:System.Web.Extensions.dll', '/reference:System.Core.dll')
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

& $compiler /nologo /target:exe /main:CodexUsagePill.LayoutTests "/out:$testPath" $references (Join-Path $projectRoot 'src\CodexUsagePill.cs') (Join-Path $projectRoot 'tests\LayoutTests.cs')
if ($LASTEXITCODE -ne 0) { throw "Test build failed with exit code $LASTEXITCODE." }
& $testPath
if ($LASTEXITCODE -ne 0) { throw "Layout tests failed with exit code $LASTEXITCODE." }
