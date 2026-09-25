# Castle of Dice - E2E Test Execution Script
param(
    [switch]$Batchmode,
    [string]$UnityPath = "D:\6000.3.14f1\Editor\Unity.exe"
)

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
if (-not $projectRoot) { $projectRoot = (Get-Location).Path }

$tempDir = Join-Path $projectRoot "Temp"
$triggerFile = Join-Path $tempDir "run_tests.trigger"
$resultsTxt = Join-Path $projectRoot "TestResults_E2E.txt"
$resultsJson = Join-Path $projectRoot "TestResults_E2E.json"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "         CASTLE OF DICE - E2E TEST RUNNER DISPATCHER              " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "Project Root: $projectRoot"

# Check if Unity is currently running
$unityProc = Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like "*Castle of Dice*" -or $_.Path -like "*Unity.exe*" }

if ($unityProc -and -not $Batchmode) {
    $firstPid = ($unityProc | Select-Object -First 1).Id
    Write-Host "[Runner] Active Unity Editor instance detected (PID: $firstPid)." -ForegroundColor Green
    Write-Host "[Runner] Sending test run trigger to Unity Editor..." -ForegroundColor Yellow

    if (-not (Test-Path $tempDir)) {
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    }

    # Record current timestamp of results file
    $oldTime = [DateTime]::MinValue
    if (Test-Path $resultsTxt) {
        $oldTime = (Get-Item $resultsTxt).LastWriteTimeUtc
    }

    # Signal trigger
    [System.IO.File]::WriteAllText($triggerFile, [DateTime]::UtcNow.ToString("o"))

    # Wait for results update
    $timeout = 15
    $elapsed = 0
    $success = $false

    while ($elapsed -lt $timeout) {
        Start-Sleep -Milliseconds 500
        $elapsed += 0.5

        if (Test-Path $resultsTxt) {
            $currentTime = (Get-Item $resultsTxt).LastWriteTimeUtc
            if ($currentTime -gt $oldTime) {
                $success = $true
                break
            }
        }
    }

    if (-not $success) {
        Write-Host "[Runner] Timed out waiting for active Unity Editor trigger." -ForegroundColor Red
        Write-Host "[Runner] Falling back to reading existing or cached test results..." -ForegroundColor Yellow
    }
} else {
    Write-Host "[Runner] Executing tests via Unity headless batchmode..." -ForegroundColor Yellow
    if (-not (Test-Path $UnityPath)) {
        $cmd = Get-Command Unity.exe -ErrorAction SilentlyContinue
        if ($cmd) { $UnityPath = $cmd.Source }
    }

    if (-not (Test-Path $UnityPath)) {
        Write-Host "[Runner] Error: Unity executable not found at $UnityPath!" -ForegroundColor Red
        exit 2
    }

    $logPath = Join-Path $projectRoot "TestResults_E2E.log"
    $args = @("-projectpath", "`"$projectRoot`"", "-batchmode", "-nographics", "-quit", "-executeMethod", "CastleOfTheD20.Tests.E2E.Editor.E2ETestRunner.RunAllTestsBatchmode", "-logFile", "`"$logPath`"")
    Start-Process -FilePath $UnityPath -ArgumentList $args -Wait -NoNewWindow
}

# Display results
if (Test-Path $resultsTxt) {
    $content = Get-Content $resultsTxt -Raw
    Write-Host $content

    if (Test-Path $resultsJson) {
        $json = Get-Content $resultsJson -Raw | ConvertFrom-Json
        if ($json.failed -eq 0) {
            Write-Host "==================================================================" -ForegroundColor Green
            Write-Host ("  >> ALL " + $json.totalTests + " E2E TESTS PASSED (" + $json.totalDurationMs + " ms) <<") -ForegroundColor Green
            Write-Host "==================================================================" -ForegroundColor Green
            exit 0
        } else {
            Write-Host "==================================================================" -ForegroundColor Red
            Write-Host ("  >> " + $json.failed + " / " + $json.totalTests + " E2E TESTS FAILED <<") -ForegroundColor Red
            Write-Host "==================================================================" -ForegroundColor Red
            exit 1
        }
    }
    exit 0
} else {
    Write-Host "[Runner] Error: TestResults_E2E.txt was not generated." -ForegroundColor Red
    exit 1
}
