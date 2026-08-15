[CmdletBinding()]
param(
    [switch]$StartServices,
    [string]$BaseUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Stop"
$script:Passed = 0
$script:Failed = 0

function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if ($Condition) {
        $script:Passed++
        Write-Host "[PASS] $Message" -ForegroundColor Green
        return
    }

    $script:Failed++
    Write-Host "[FAIL] $Message" -ForegroundColor Red
    throw "Assertion failed: $Message"
}

function Invoke-Api {
    param(
        [Parameter(Mandatory)] [string]$Method,
        [Parameter(Mandatory)] [string]$Url,
        [hashtable]$Headers = @{},
        [object]$Body = $null
    )

    $params = @{
        Method = $Method
        Uri = $Url
        Headers = $Headers
        ErrorAction = "Stop"
    }

    if ($null -ne $Body) {
        $params.ContentType = "application/json"
        $params.Body = ($Body | ConvertTo-Json -Depth 10 -Compress)
    }

    try {
        $response = Invoke-WebRequest @params
        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Body = $response.Content
            Headers = $response.Headers
        }
    }
    catch {
        if ($null -eq $_.Exception.Response) { throw }

        $body = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { "" }
        return [pscustomobject]@{
            StatusCode = [int]$_.Exception.Response.StatusCode
            Body = $body
            Headers = $_.Exception.Response.Headers
        }
    }
}

function Wait-ForHealth {
    $deadline = (Get-Date).AddMinutes(2)
    do {
        try {
            $response = Invoke-WebRequest -Uri "$BaseUrl/health" -Method Get -ErrorAction Stop
            if ([int]$response.StatusCode -eq 200) { return }
        }
        catch { }

        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)

    throw "API did not become healthy within the timeout."
}

$startedByScript = $false
try {
    if ($StartServices) {
        Write-Host "Starting Docker Compose stack..." -ForegroundColor Cyan
        docker compose down -v --remove-orphans 2>$null | Out-Null
        docker compose up -d --build
        if ($LASTEXITCODE -ne 0) { throw "docker compose up failed." }
        $startedByScript = $true
    }

    Wait-ForHealth

    Write-Host "[1/7] Health and authorization" -ForegroundColor Cyan
    $health = Invoke-Api -Method Get -Url "$BaseUrl/health"
    Assert-Condition ($health.StatusCode -eq 200) "Health endpoint returns 200"

    $validBody = @{
        clientOrderId = "e2e-order-001"
        symbol = "btcusd"
        price = 100
        volume = 2
    }

    $unauthorized = Invoke-Api -Method Post -Url "$BaseUrl/api/v1/orders" -Body $validBody
    Assert-Condition ($unauthorized.StatusCode -eq 401) "Unauthenticated create request returns 401"

    $userId = [guid]::NewGuid().ToString()
    $userHeaders = @{ "X-User-Id" = $userId }
    $forbidden = Invoke-Api -Method Post -Url "$BaseUrl/api/v1/orders" -Headers $userHeaders -Body $validBody
    Assert-Condition ($forbidden.StatusCode -eq 403) "Authenticated user without permission receives 403"

    $headers = @{
        "X-User-Id" = $userId
        "X-Permissions" = "orders:create"
    }

    Write-Host "[2/7] Request validation" -ForegroundColor Cyan
    $invalid = Invoke-Api -Method Post -Url "$BaseUrl/api/v1/orders" -Headers $headers -Body @{
        clientOrderId = "short"
        symbol = "???"
        price = 0
        volume = 0
    }
    Assert-Condition ($invalid.StatusCode -eq 400) "Invalid request returns 400"

    Write-Host "[3/7] Create and read order" -ForegroundColor Cyan
    $createdResponse = Invoke-Api -Method Post -Url "$BaseUrl/api/v1/orders" -Headers $headers -Body $validBody
    Assert-Condition ($createdResponse.StatusCode -eq 201) "First request creates the order with 201"

    $created = $createdResponse.Body | ConvertFrom-Json
    Assert-Condition ($created.symbol -eq "BTCUSD") "Symbol is normalized to uppercase"
    Assert-Condition ($created.state -eq "active") "New order state is active"
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($created.id)) "Created order has an id"

    $getResponse = Invoke-Api -Method Get -Url "$BaseUrl/api/v1/orders/$($created.id)" -Headers $userHeaders
    Assert-Condition ($getResponse.StatusCode -eq 200) "Owner can read the created order"

    Write-Host "[4/7] Idempotency" -ForegroundColor Cyan
    $retryResponse = Invoke-Api -Method Post -Url "$BaseUrl/api/v1/orders" -Headers $headers -Body $validBody
    Assert-Condition ($retryResponse.StatusCode -eq 200) "Idempotent retry returns 200"
    $retry = $retryResponse.Body | ConvertFrom-Json
    Assert-Condition ($retry.id -eq $created.id) "Idempotent retry returns the same order id"

    $conflictBody = @{} + $validBody
    $conflictBody["price"] = 101
    $conflict = Invoke-Api -Method Post -Url "$BaseUrl/api/v1/orders" -Headers $headers -Body $conflictBody
    Assert-Condition ($conflict.StatusCode -eq 409) "Same ClientOrderId with different payload returns 409"

    Write-Host "[5/7] User isolation" -ForegroundColor Cyan
    $otherHeaders = @{ "X-User-Id" = [guid]::NewGuid().ToString() }
    $privateRead = Invoke-Api -Method Get -Url "$BaseUrl/api/v1/orders/$($created.id)" -Headers $otherHeaders
    Assert-Condition ($privateRead.StatusCode -eq 404) "Another user cannot read the order"

    Write-Host "[6/7] Trading rule validation" -ForegroundColor Cyan
    $tooLarge = Invoke-Api -Method Post -Url "$BaseUrl/api/v1/orders" -Headers $headers -Body @{
        clientOrderId = "e2e-order-too-large"
        symbol = "BTCUSD"
        price = 100000
        volume = 101
    }
    Assert-Condition ($tooLarge.StatusCode -eq 422) "Order above max notional returns 422"

    Write-Host "[7/7] Missing resource" -ForegroundColor Cyan
    $missing = Invoke-Api -Method Get -Url "$BaseUrl/api/v1/orders/$([guid]::NewGuid())" -Headers $userHeaders
    Assert-Condition ($missing.StatusCode -eq 404) "Unknown order returns 404"
}
finally {
    Write-Host ""
    Write-Host "========================================"
    Write-Host " Test summary"
    Write-Host "========================================"
    Write-Host "Passed: $script:Passed"
    Write-Host "Failed: $script:Failed"

    if ($startedByScript) {
        docker compose down -v --remove-orphans | Out-Null
    }
}

if ($script:Failed -gt 0) { exit 1 }
Write-Host "All end-to-end checks passed." -ForegroundColor Green
