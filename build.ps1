# Builds SimpleMusicReplacer.dll, installs it into Valheim and creates the zips in dist\:
#   SimpleMusicReplacer-<version>-thunderstore.zip  public upload (no music)
#   SimpleMusicReplacer-friends.zip                 unzip into a friend's BepInEx folder (includes your music)
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
    /target:library /optimize "/out:$bin\SimpleMusicReplacer.dll" @refs "$root\src\SimpleMusicReplacerPlugin.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

# Install for local testing
New-Item -ItemType Directory -Force "$bepinex\plugins\SimpleMusicReplacer" | Out-Null
Copy-Item "$bin\SimpleMusicReplacer.dll" "$bepinex\plugins\SimpleMusicReplacer\" -Force
Write-Host "Installed to $bepinex\plugins\SimpleMusicReplacer"

# Thunderstore / Nexus package
$version = (Get-Content "$root\thunderstore\manifest.json" -Raw | ConvertFrom-Json).version_number
$pkg = "$bin\thunderstore"
New-Item -ItemType Directory -Force "$pkg\plugins" | Out-Null
Copy-Item "$bin\SimpleMusicReplacer.dll" "$pkg\plugins\"
Copy-Item "$root\thunderstore\manifest.json", "$root\thunderstore\icon.png", "$root\README.md" $pkg
Compress-Archive -Path "$pkg\*" -DestinationPath "$dist\SimpleMusicReplacer-$version-thunderstore.zip"

# Friends package: plugins\ + config\ with your music folders
$friends = "$bin\friends"
New-Item -ItemType Directory -Force "$friends\plugins\SimpleMusicReplacer", "$friends\config\SimpleMusicReplacer" | Out-Null
Copy-Item "$bin\SimpleMusicReplacer.dll" "$friends\plugins\SimpleMusicReplacer\"
if (Test-Path "$bepinex\config\SimpleMusicReplacer") {
    Get-ChildItem "$bepinex\config\SimpleMusicReplacer" -Directory |
        ForEach-Object { Copy-Item $_.FullName "$friends\config\SimpleMusicReplacer\" -Recurse }
}
Compress-Archive -Path "$friends\*" -DestinationPath "$dist\SimpleMusicReplacer-friends.zip"

Write-Host "Packages in $dist"
Get-ChildItem $dist | ForEach-Object { Write-Host "  $($_.Name)" }
