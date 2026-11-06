$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$destination = Join-Path $root 'release\TLCPlus-CSharp-v1.0.3'
$publishArguments = @(
    'publish', "$root\src\TLCPlus.Desktop\TLCPlus.Desktop.csproj",
    '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
    '-o', $destination,
    "-p:BaseOutputPath=$root\work\publish-build\",
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true'
)

dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

$documentation = 'LICENSE', 'README.md', 'CONTRIBUTORS.md', 'CONTRIBUTING.md'
foreach ($file in $documentation) {
    Copy-Item -LiteralPath (Join-Path $root $file) -Destination $destination -Force
}
