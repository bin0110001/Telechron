<#
.SYNOPSIS
    R-ENG1 file-length lint: warns (does not fail the build) on source files
    exceeding the ~800 line cap. Advisory only — Phase 7 wires this into the
    Findings pipeline so oversized files become auto-repair candidates instead
    of a blocking CI failure.
#>
param(
    [int]$MaxLines = 800,
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot ".."))
)

$excludeDirs = @('bin', 'obj', 'node_modules', '.git', 'dist', 'build')
$extensions = @('*.cs', '*.ts', '*.tsx', '*.js', '*.jsx')

$violations = @()

foreach ($ext in $extensions) {
    Get-ChildItem -Path $RepoRoot -Recurse -File -Filter $ext -ErrorAction SilentlyContinue |
        Where-Object {
            $relative = $_.FullName.Substring($RepoRoot.Length)
            -not ($excludeDirs | Where-Object { $relative -match [regex]::Escape([IO.Path]::DirectorySeparatorChar + $_ + [IO.Path]::DirectorySeparatorChar) })
        } |
        ForEach-Object {
            $lineCount = (Get-Content -Path $_.FullName -ErrorAction SilentlyContinue | Measure-Object -Line).Lines
            if ($lineCount -gt $MaxLines) {
                $violations += [pscustomobject]@{
                    File  = $_.FullName.Substring($RepoRoot.Length + 1)
                    Lines = $lineCount
                }
            }
        }
}

if ($violations.Count -eq 0) {
    Write-Host "File-length lint: all files within $MaxLines-line cap." -ForegroundColor Green
    exit 0
}

Write-Warning "File-length lint: $($violations.Count) file(s) exceed the ~$MaxLines line cap (R-ENG1). Advisory only."
$violations | Sort-Object -Property Lines -Descending | ForEach-Object {
    Write-Warning ("  {0} ({1} lines)" -f $_.File, $_.Lines)
}

# Advisory: always exit 0. Phase 7 turns this into a Finding instead of a CI signal.
exit 0
