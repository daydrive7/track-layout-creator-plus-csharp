$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
dotnet publish "$root\src\TLCPlus.Desktop\TLCPlus.Desktop.csproj" -c Release -r win-x64 --self-contained true -o "$root\release\TLCPlus-CSharp-v1.0.1" "-p:BaseOutputPath=$root\work\publish-build\" -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Copy-Item -LiteralPath "$root\LICENSE","$root\README.md" -Destination "$root\release\TLCPlus-CSharp-v1.0.1" -Force
