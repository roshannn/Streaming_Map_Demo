param(
    [Parameter(Mandatory = $true)]
    [string]$Url,

    [Parameter(Mandatory = $true)]
    [string]$Destination
)

$ErrorActionPreference = "Stop"

function Format-ByteCount([double]$Bytes) {
    if ($Bytes -ge 1TB) { return "{0:N2} TB" -f ($Bytes / 1TB) }
    if ($Bytes -ge 1GB) { return "{0:N2} GB" -f ($Bytes / 1GB) }
    if ($Bytes -ge 1MB) { return "{0:N2} MB" -f ($Bytes / 1MB) }
    if ($Bytes -ge 1KB) { return "{0:N2} KB" -f ($Bytes / 1KB) }
    return "{0:N0} B" -f $Bytes
}

function Format-Duration([double]$Seconds) {
    if ([double]::IsInfinity($Seconds) -or [double]::IsNaN($Seconds)) {
        return "--:--:--"
    }
    $span = [TimeSpan]::FromSeconds([Math]::Max(0, $Seconds))
    if ($span.TotalDays -ge 1) {
        return "{0}d {1:00}:{2:00}:{3:00}" -f [Math]::Floor($span.TotalDays), $span.Hours, $span.Minutes, $span.Seconds
    }
    return "{0:00}:{1:00}:{2:00}" -f [Math]::Floor($span.TotalHours), $span.Minutes, $span.Seconds
}

$destinationPath = [IO.Path]::GetFullPath($Destination)
$destinationDirectory = [IO.Path]::GetDirectoryName($destinationPath)
[IO.Directory]::CreateDirectory($destinationDirectory) | Out-Null

$existingBytes = if ([IO.File]::Exists($destinationPath)) {
    (Get-Item -LiteralPath $destinationPath).Length
} else {
    0L
}

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Add-Type -AssemblyName System.Net.Http
$handler = New-Object Net.Http.HttpClientHandler
$client = New-Object Net.Http.HttpClient($handler)
$client.Timeout = [TimeSpan]::FromHours(24)
$request = New-Object Net.Http.HttpRequestMessage([Net.Http.HttpMethod]::Get, $Url)
if ($existingBytes -gt 0) {
    $request.Headers.Range = New-Object Net.Http.Headers.RangeHeaderValue($existingBytes, $null)
}

$response = $null
$inputStream = $null
$outputStream = $null

try {
    $response = $client.SendAsync(
        $request,
        [Net.Http.HttpCompletionOption]::ResponseHeadersRead
    ).GetAwaiter().GetResult()
    [void]$response.EnsureSuccessStatusCode()

    $resuming = $existingBytes -gt 0 -and
        $response.StatusCode -eq [Net.HttpStatusCode]::PartialContent
    if (-not $resuming) {
        $existingBytes = 0L
    }

    $responseBytes = $response.Content.Headers.ContentLength
    $totalBytes = if ($null -ne $responseBytes) {
        $existingBytes + $responseBytes
    } else {
        0L
    }

    $fileMode = if ($resuming) {
        [IO.FileMode]::Append
    } else {
        [IO.FileMode]::Create
    }

    $inputStream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
    $outputStream = New-Object IO.FileStream(
        $destinationPath,
        $fileMode,
        [IO.FileAccess]::Write,
        [IO.FileShare]::None,
        1MB,
        [IO.FileOptions]::SequentialScan
    )

    $buffer = New-Object byte[] (1MB)
    $downloadedBytes = $existingBytes
    $sessionBytes = 0L
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()

    while (($read = $inputStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
        $outputStream.Write($buffer, 0, $read)
        $downloadedBytes += $read
        $sessionBytes += $read

        $elapsedSeconds = [Math]::Max($stopwatch.Elapsed.TotalSeconds, 0.001)
        $bytesPerSecond = $sessionBytes / $elapsedSeconds
        $speed = "$(Format-ByteCount $bytesPerSecond)/s"
        $elapsed = Format-Duration $elapsedSeconds

        if ($totalBytes -gt 0) {
            $percent = [Math]::Min(100, ($downloadedBytes * 100.0 / $totalBytes))
            $remainingSeconds = if ($bytesPerSecond -gt 0) {
                ($totalBytes - $downloadedBytes) / $bytesPerSecond
            } else {
                [double]::PositiveInfinity
            }
            $status = "$(Format-ByteCount $downloadedBytes) / $(Format-ByteCount $totalBytes)  |  $speed  |  elapsed $elapsed  |  ETA $(Format-Duration $remainingSeconds)"
            Write-Progress -Activity "Downloading offline map" -Status $status -PercentComplete $percent
        } else {
            $status = "$(Format-ByteCount $downloadedBytes)  |  $speed  |  elapsed $elapsed  |  ETA unknown"
            Write-Progress -Activity "Downloading offline map" -Status $status
        }
    }

    $outputStream.Flush()
    Write-Progress -Activity "Downloading offline map" -Completed
    Write-Host "Downloaded $(Format-ByteCount $downloadedBytes) in $(Format-Duration $stopwatch.Elapsed.TotalSeconds)."
} finally {
    if ($null -ne $outputStream) { $outputStream.Dispose() }
    if ($null -ne $inputStream) { $inputStream.Dispose() }
    if ($null -ne $response) { $response.Dispose() }
    $request.Dispose()
    $client.Dispose()
    $handler.Dispose()
}
