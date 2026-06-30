param(
    [string]$PackageName = "com.rynex.prayadfree",
    [string]$ApkPath = "",
    [int]$WaitSeconds = 8,
    [switch]$Install,
    [switch]$IncludeNetworkToggle,
    [switch]$ClearWebberActive,
    [switch]$OpenLogsFolder
)

$ErrorActionPreference = "Stop"

function Repo-Root {
    return (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Require-AdbDevice {
    $devices = adb devices | Select-String -Pattern "`tdevice$"
    if (-not $devices) {
        throw "No ADB device is connected. Connect the phone and enable USB debugging."
    }
}

function Resolve-ApkPath {
    if ($ApkPath) {
        return (Resolve-Path $ApkPath).Path
    }

    $root = Repo-Root
    $candidate = Join-Path $root "PrayAdFree\bin\Debug\net10.0-android\com.rynex.prayadfree-Signed.apk"
    if (-not (Test-Path $candidate)) {
        throw "APK not found at $candidate. Build Android Debug first."
    }

    return (Resolve-Path $candidate).Path
}

function Start-AppAndCollectLogs {
    param(
        [string]$Name,
        [string]$OutDir
    )

    adb logcat -c | Out-Null
    adb shell am force-stop $PackageName | Out-Null
    adb shell monkey -p $PackageName -c android.intent.category.LAUNCHER 1 | Out-Null
    Start-Sleep -Seconds $WaitSeconds

    $logPath = Join-Path $OutDir "$Name-logcat.txt"
    adb logcat -d > $logPath

    $filteredPath = Join-Path $OutDir "$Name-mauiwebber.txt"
    Get-Content $logPath |
        Select-String -Pattern "MauiWebber|PrayAdFree|chromium|WebView|AndroidRuntime|FATAL|No assemblies" |
        ForEach-Object { $_.Line } |
        Set-Content $filteredPath

    return $filteredPath
}

function Pull-AppLogs {
    param([string]$OutDir)

    $appLogDir = Join-Path $OutDir "app-files"
    New-Item -ItemType Directory -Force -Path $appLogDir | Out-Null

    try {
        adb shell run-as $PackageName ls files/PrayAdFreeLogs | Out-Null
        adb exec-out run-as $PackageName cat files/PrayAdFreeLogs/PrayAdFree-events.log > (Join-Path $appLogDir "PrayAdFree-events.log")
    } catch {
        "PrayAdFree-events.log was not available through run-as." | Set-Content (Join-Path $appLogDir "PrayAdFree-events.log.txt")
    }

    try {
        adb shell run-as $PackageName ls files/MauiWebber | Out-Null
        adb exec-out run-as $PackageName find files/MauiWebber -maxdepth 4 -type f > (Join-Path $appLogDir "MauiWebber-files.txt")
    } catch {
        "MauiWebber files were not available through run-as." | Set-Content (Join-Path $appLogDir "MauiWebber-files.txt")
    }
}

function Clear-WebberActiveSlot {
    try {
        adb shell run-as $PackageName rm -rf files/MauiWebber/prayadfree-today/active
        adb shell run-as $PackageName rm -rf files/MauiWebber/prayadfree-today/staging
    } catch {
        Write-Warning "Could not clear Webber active slot via run-as: $($_.Exception.Message)"
    }
}

function Set-Network {
    param([bool]$Enabled)

    if ($Enabled) {
        adb shell svc wifi enable | Out-Null
        adb shell svc data enable | Out-Null
    } else {
        adb shell svc wifi disable | Out-Null
        adb shell svc data disable | Out-Null
    }
}

$root = Repo-Root
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outDir = Join-Path $root "build\mauiwebber-android-$timestamp"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Require-AdbDevice

if ($Install) {
    $resolvedApk = Resolve-ApkPath
    adb install --no-incremental -r $resolvedApk | Tee-Object -FilePath (Join-Path $outDir "install.txt")
    adb shell run-as $PackageName rm -rf files/.__override__ | Out-Null
}

if ($ClearWebberActive) {
    Clear-WebberActiveSlot
}

$coldLog = Start-AppAndCollectLogs -Name "cold-start" -OutDir $outDir
$warmLog = Start-AppAndCollectLogs -Name "warm-start" -OutDir $outDir

if ($IncludeNetworkToggle) {
    try {
        Set-Network -Enabled $false
        $offlineLog = Start-AppAndCollectLogs -Name "offline-start" -OutDir $outDir
    } finally {
        Set-Network -Enabled $true
    }
}

Pull-AppLogs -OutDir $outDir

@(
    "MauiWebber Android smoke output: $outDir",
    "Cold start filtered log: $coldLog",
    "Warm start filtered log: $warmLog",
    "Look for these events:",
    "  MauiWebber.ResolveStartupFile.Start / End",
    "  MauiWebber.WebView.Navigating / Navigated",
    "  MauiWebber.Today.GetSnapshot.First.Start / End",
    "  MauiWebber.JsTrace payload with bridgeReady",
    "  MauiWebber.JsTrace payload with renderComplete"
) | Tee-Object -FilePath (Join-Path $outDir "summary.txt")

if ($OpenLogsFolder) {
    Invoke-Item $outDir
}
