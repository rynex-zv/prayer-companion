param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,
    [int]$TimeoutSeconds = 45
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$process = Start-Process -FilePath $resolvedExe -PassThru
try {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $last = 'The application window did not become available.'
    while ([DateTime]::UtcNow -lt $deadline) {
        $process.Refresh()
        if ($process.HasExited) { throw "Application exited with code $($process.ExitCode)." }
        if ($process.MainWindowHandle -eq 0) {
            Start-Sleep -Milliseconds 250
            continue
        }

        $window = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
        $all = $window.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition)
        $documents = @($all | Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Document })
        $interactive = @($all | Where-Object {
            $_.Current.ControlType -in @(
                [System.Windows.Automation.ControlType]::Button,
                [System.Windows.Automation.ControlType]::Edit,
                [System.Windows.Automation.ControlType]::ComboBox,
                [System.Windows.Automation.ControlType]::CheckBox,
                [System.Windows.Automation.ControlType]::Slider)
        })
        $named = @($interactive | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Current.Name) })
        if ($documents.Count -gt 0 -and $named.Count -ge 5) {
            [pscustomobject]@{
                Passed = $true
                ProcessId = $process.Id
                Documents = $documents.Count
                InteractiveControls = $interactive.Count
                NamedInteractiveControls = $named.Count
                SampleNames = ($named | Select-Object -First 12 | ForEach-Object { $_.Current.Name }) -join ' | '
            } | Format-List
            exit 0
        }
        $last = "UIA documents=$($documents.Count), named interactive controls=$($named.Count)."
        Start-Sleep -Milliseconds 500
    }
    throw "React accessibility provider was not attached to the app window. $last"
} finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force }
}
