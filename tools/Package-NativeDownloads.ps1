param(
    [Parameter(Mandatory = $true)]
    [string]$WebVersion,

    [string]$WindowsSource = "artifacts\windows-release-2026-08-12"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "PrayAdFree\PrayAdFree.csproj"
[xml]$projectXml = Get-Content -LiteralPath $project
$appVersion = ([string]$projectXml.Project.PropertyGroup.ApplicationDisplayVersion).Trim()
if ([string]::IsNullOrWhiteSpace($appVersion)) { throw "ApplicationDisplayVersion is missing." }

$downloads = Join-Path $root "Pray.web\src\public\downloads"
$androidName = "PrayAdFree-Android-$appVersion-web$WebVersion.apk"
$windowsName = "PrayAdFree-Windows-x64-$appVersion-web$WebVersion.zip"
$androidSource = Join-Path $root "PrayAdFree\bin\Release\net10.0-android\publish\com.rynex.prayer-Signed.apk"
$windowsSource = Join-Path $root $WindowsSource
$androidTarget = Join-Path $downloads "android\$androidName"
$windowsTarget = Join-Path $downloads "windows\$windowsName"

if (-not (Test-Path -LiteralPath $androidSource)) { throw "Android Release APK is missing: $androidSource" }
if (-not (Test-Path -LiteralPath $windowsSource)) { throw "Windows Release directory is missing: $windowsSource" }

Copy-Item -LiteralPath $androidSource -Destination $androidTarget -Force
Compress-Archive -Path (Join-Path $windowsSource "*") -DestinationPath $windowsTarget -CompressionLevel Optimal -Force

$manifest = @{
    files = @(
        @{ platform = "android"; kind = "apk"; url = "/downloads/android/$androidName"; label = "Download Android APK"; version = "$appVersion (web $WebVersion)" },
        @{ platform = "windows"; kind = "zip"; url = "/downloads/windows/$windowsName"; label = "Download Windows x64 ZIP"; version = "$appVersion (web $WebVersion)" }
    )
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $downloads "manifest.json") -Encoding utf8

Write-Output $androidTarget
Write-Output $windowsTarget
