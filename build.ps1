# Builds SeaShanty.dll, installs it into Valheim and creates the zips in dist\:
#   SeaShanty-<version>-thunderstore.zip  public upload (no music)
#   SeaShanty-friends.zip                 unzip into a friend's BepInEx folder (includes your music)
# Usage: powershell -ExecutionPolicy Bypass -File build.ps1 [-ValheimDir <path>]
param([string]$ValheimDir = 'D:\SteamLibrary\steamapps\common\Valheim')

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$bin = "$root\bin"
$dist = "$root\dist"
$bepinex = "$ValheimDir\BepInEx"
$m = "$ValheimDir\valheim_Data\Managed"
$refs = @(
    "$m\mscorlib.dll", "$m\netstandard.dll", "$m\System.dll", "$m\System.Core.dll",
    "$bepinex\core\BepInEx.dll", "$bepinex\core\0Harmony.dll", "$m\assembly_valheim.dll", "$m\assembly_utils.dll",
    "$m\UnityEngine.dll", "$m\UnityEngine.CoreModule.dll", "$m\UnityEngine.AudioModule.dll",
    "$m\UnityEngine.InputLegacyModule.dll", "$m\UnityEngine.UnityWebRequestModule.dll",
    "$m\UnityEngine.UnityWebRequestAudioModule.dll"
) | ForEach-Object { "/r:$_" }

Remove-Item $bin, $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $bin, $dist | Out-Null

& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /nostdlib /noconfig `
    /target:library /optimize "/out:$bin\SeaShanty.dll" @refs "$root\src\SeaShantyPlugin.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

# Install for local testing
New-Item -ItemType Directory -Force "$bepinex\plugins\SeaShanty" | Out-Null
Copy-Item "$bin\SeaShanty.dll" "$bepinex\plugins\SeaShanty\" -Force
Write-Host "Installed to $bepinex\plugins\SeaShanty"

# Thunderstore / Nexus package
$version = (Get-Content "$root\thunderstore\manifest.json" -Raw | ConvertFrom-Json).version_number
$pkg = "$bin\thunderstore"
New-Item -ItemType Directory -Force "$pkg\plugins" | Out-Null
Copy-Item "$bin\SeaShanty.dll" "$pkg\plugins\"
Copy-Item "$root\thunderstore\manifest.json", "$root\thunderstore\icon.png", "$root\README.md" $pkg
Compress-Archive -Path "$pkg\*" -DestinationPath "$dist\SeaShanty-$version-thunderstore.zip"

# Friends package: plugins\ + config\ with your music folders
$friends = "$bin\friends"
New-Item -ItemType Directory -Force "$friends\plugins\SeaShanty", "$friends\config\SeaShanty" | Out-Null
Copy-Item "$bin\SeaShanty.dll" "$friends\plugins\SeaShanty\"
if (Test-Path "$bepinex\config\SeaShanty") {
    Get-ChildItem "$bepinex\config\SeaShanty" -Directory |
        ForEach-Object { Copy-Item $_.FullName "$friends\config\SeaShanty\" -Recurse }
}
Compress-Archive -Path "$friends\*" -DestinationPath "$dist\SeaShanty-friends.zip"

Write-Host "Packages in $dist"
Get-ChildItem $dist | ForEach-Object { Write-Host "  $($_.Name)" }
