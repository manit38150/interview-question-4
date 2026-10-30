param([int]$Port = 5099)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$apiDirectory = Join-Path $projectRoot 'backend'
$apiDll = Join-Path $apiDirectory 'bin/Release/net10.0/Example.InterviewQuestion4.Api.dll'
if (-not (Test-Path -LiteralPath $apiDll)) {
    throw 'Build the backend in Release configuration before running this script.'
}

$baseUrl = "http://localhost:$Port"
$logPath = Join-Path ([System.IO.Path]::GetTempPath()) ('interview-question-4-' + [guid]::NewGuid())
$apiProcess = Start-Process -FilePath 'dotnet' -ArgumentList @($apiDll, '--urls', $baseUrl) -WorkingDirectory $apiDirectory -WindowStyle Hidden -PassThru -RedirectStandardOutput "$logPath.out.log" -RedirectStandardError "$logPath.err.log"

try {
    $ready = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        if ($apiProcess.HasExited) {
            throw "API exited unexpectedly. See $logPath.err.log"
        }
        try {
            $response = Invoke-WebRequest "$baseUrl/api/health" -UseBasicParsing -TimeoutSec 2
            $ready = $true
            break
        } catch {
            Start-Sleep -Milliseconds 250
        }
    }
    if (-not $ready) { throw "API did not start. See $logPath.out.log" }
    if ($response.StatusCode -ne 200 -or ($response.Content | ConvertFrom-Json).status -ne 'healthy') {
        throw 'Health response did not match HTTP 200 and status=healthy.'
    }
    if ($response.Headers['Content-Type'] -notmatch 'application/json') {
        throw 'Health response must be JSON.'
    }
    Write-Output 'PASS: GET /api/health returns HTTP 200 and healthy JSON.'

    $allowedOrigin = 'http://localhost:4200'
    $cors = Invoke-WebRequest "$baseUrl/api/health" -UseBasicParsing -Headers @{ Origin = $allowedOrigin }
    if ($cors.Headers['Access-Control-Allow-Origin'] -ne $allowedOrigin) { throw 'Allowed origin was rejected.' }
    Write-Output 'PASS: Angular development origin is allowed.'

    $preflight = Invoke-WebRequest "$baseUrl/api/health" -UseBasicParsing -Method Options -Headers @{
        Origin = $allowedOrigin
        'Access-Control-Request-Method' = 'GET'
        'Access-Control-Request-Headers' = 'content-type'
    }
    if ($preflight.StatusCode -ne 204 -or $preflight.Headers['Access-Control-Allow-Origin'] -ne $allowedOrigin) {
        throw 'CORS preflight failed.'
    }
    Write-Output 'PASS: CORS preflight succeeds.'

    $denied = Invoke-WebRequest "$baseUrl/api/health" -UseBasicParsing -Headers @{ Origin = 'http://localhost:4300' }
    if ($denied.Headers['Access-Control-Allow-Origin']) { throw 'An unconfigured origin received CORS permission.' }
    Write-Output 'PASS: Unconfigured origin receives no CORS permission.'
} finally {
    if (-not $apiProcess.HasExited) { Stop-Process -Id $apiProcess.Id }
}
