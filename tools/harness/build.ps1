<#
 Compila arquivos C# com o Roslyn embutido no PowerShell 7 (pwsh).
 Uso (de qualquer pasta):
   pwsh -NoProfile -File tools/harness/build.ps1 -OutDll out/Mmorpg.Core.dll -Source client/Assets/Scripts/Core
 -Source aceita arquivos .cs ou pastas (busca recursiva). -Reference adiciona DLLs extras.
 Sai com código 1 se houver erro de compilação. Linguagem: C# 9 (o que o Unity 2021.2+ aceita).
 Este script existe porque o ambiente de desenvolvimento do projeto não tinha dotnet nem Unity.
 Em uma máquina normal, prefira o Unity Test Runner ou 'dotnet test'.
#>
param(
    [Parameter(Mandatory = $true)][string]$OutDll,
    [Parameter(Mandatory = $true)][string[]]$Source,
    [string[]]$Reference = @(),
    [string[]]$Define = @(),
    [switch]$Exe,
    [switch]$ShowWarnings
)
$ErrorActionPreference = 'Stop'
# Com 'pwsh -File', listas chegam como uma string separada por vírgulas.
$Source = @($Source | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$Reference = @($Reference | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$Define = @($Define | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
Add-Type -Path (Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll'), (Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')

$files = New-Object System.Collections.Generic.List[string]
foreach ($s in $Source) {
    if (Test-Path -LiteralPath $s -PathType Container) {
        Get-ChildItem -LiteralPath $s -Recurse -Filter *.cs | ForEach-Object { $files.Add($_.FullName) }
    } else { $files.Add((Resolve-Path -LiteralPath $s).Path) }
}
if ($files.Count -eq 0) { Write-Error "Nenhum .cs encontrado"; exit 2 }

$parse = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::new([Microsoft.CodeAnalysis.CSharp.LanguageVersion]::CSharp9, [Microsoft.CodeAnalysis.DocumentationMode]::None, [Microsoft.CodeAnalysis.SourceCodeKind]::Regular, [string[]]$Define)
$trees = New-Object 'System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]'
foreach ($f in $files) {
    $text = [System.IO.File]::ReadAllText($f)
    $trees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($text, $parse, $f, [System.Text.Encoding]::UTF8))
}

$refs = New-Object 'System.Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]'
$names = Get-ChildItem -LiteralPath $PSHOME -Filter *.dll | Where-Object {
    $_.Name -like 'System.*' -or $_.Name -in @('netstandard.dll', 'mscorlib.dll', 'Newtonsoft.Json.dll')
}
foreach ($n in $names) {
    try { $refs.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($n.FullName)) } catch { }
}
foreach ($r in $Reference) { $refs.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile((Resolve-Path -LiteralPath $r).Path)) }

$kind = if ($Exe) { [Microsoft.CodeAnalysis.OutputKind]::ConsoleApplication } else { [Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary }
$opts = [Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new($kind).WithOptimizationLevel([Microsoft.CodeAnalysis.OptimizationLevel]::Release)
$name = [System.IO.Path]::GetFileNameWithoutExtension($OutDll)
$comp = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create($name, $trees, $refs, $opts)

$dir = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($OutDll))
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
$fs = [System.IO.File]::Create([System.IO.Path]::GetFullPath($OutDll))
try { $result = $comp.Emit($fs) } finally { $fs.Dispose() }

$errs = 0; $warns = 0
foreach ($d in $result.Diagnostics) {
    if ($d.Severity -eq [Microsoft.CodeAnalysis.DiagnosticSeverity]::Error) {
        $errs++
        $ls = $d.Location.GetLineSpan()
        Write-Host ("ERRO  {0}({1},{2}): {3} {4}" -f $ls.Path, ($ls.StartLinePosition.Line + 1), ($ls.StartLinePosition.Character + 1), $d.Id, $d.GetMessage())
    } elseif ($d.Severity -eq [Microsoft.CodeAnalysis.DiagnosticSeverity]::Warning) {
        $warns++
        if ($ShowWarnings) {
            $ls = $d.Location.GetLineSpan()
            Write-Host ("AVISO {0}({1},{2}): {3} {4}" -f $ls.Path, ($ls.StartLinePosition.Line + 1), ($ls.StartLinePosition.Character + 1), $d.Id, $d.GetMessage())
        }
    }
}
Write-Host ("{0}: {1} arquivo(s), {2} erro(s), {3} aviso(s)" -f $name, $files.Count, $errs, $warns)
if (-not $result.Success) { exit 1 }
