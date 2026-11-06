$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
dotnet run --project "$root\tests\TLCPlus.Verify" -c Release "-p:BaseOutputPath=$root\work\verify-build\" -- "$root"
if ($LASTEXITCODE -ne 0) { throw 'Format compatibility failed' }
$exe = "$root\release\TLCPlus-CSharp-v1.0.3\TLCPlus.exe"
if (-not (Test-Path -LiteralPath $exe)) { throw 'Run build.ps1 first' }
$process = Start-Process -FilePath $exe -ArgumentList '--smoke-test',('"' + $root + '"') -PassThru -Wait -WindowStyle Hidden
if (Test-Path -LiteralPath "$root\analysis\desktop-smoke-results.json") {
    Get-Content -LiteralPath "$root\analysis\desktop-smoke-results.json"
}
if ($process.ExitCode -ne 0) { throw 'Desktop verification failed; see analysis/desktop-smoke-results.json' }
