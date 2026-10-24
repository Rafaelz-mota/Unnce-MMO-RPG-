<#
 Compila o Core + os testes de EditMode e roda com o mini-NUnit (tools/harness/NUnitShim.cs).
 Uso:  pwsh -NoProfile -File tools/harness/run-tests.ps1 [-Filter NomeParcial] [-OutDir caminho]
 Variável opcional MMORPG_DATA_DIR: pasta com os JSON (padrão: client/Assets/Resources/GameData).
#>
param(
    [string]$Filter = '',
    [string]$OutDir = ''
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
if (-not $OutDir) { $OutDir = Join-Path ([System.IO.Path]::GetTempPath()) 'mmorpg-harness' }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$build = Join-Path $PSScriptRoot 'build.ps1'
$core = Join-Path $OutDir 'Mmorpg.Core.dll'
$shim = Join-Path $OutDir 'NUnitShim.dll'
$tests = Join-Path $OutDir 'Mmorpg.Core.Tests.dll'

& pwsh -NoProfile -File $build -OutDll $core -Source (Join-Path $root 'client/Assets/Scripts/Core'); if ($LASTEXITCODE) { exit 1 }
& pwsh -NoProfile -File $build -OutDll $shim -Source (Join-Path $PSScriptRoot 'NUnitShim.cs'); if ($LASTEXITCODE) { exit 1 }
& pwsh -NoProfile -File $build -OutDll $tests -Source (Join-Path $root 'client/Assets/Tests/EditMode') -Reference "$core,$shim"; if ($LASTEXITCODE) { exit 1 }

if (-not $env:MMORPG_DATA_DIR) { $env:MMORPG_DATA_DIR = Join-Path $root 'client/Assets/Resources/GameData' }
[System.Reflection.Assembly]::LoadFrom($core) | Out-Null
$shimAsm = [System.Reflection.Assembly]::LoadFrom($shim)
$testAsm = [System.Reflection.Assembly]::LoadFrom($tests)
$runner = $shimAsm.GetType('MiniNUnit.Runner')
$failed = $runner.GetMethod('Run').Invoke($null, @($testAsm, $Filter))
exit ([int]$failed)
