param(
    [Parameter(Mandatory = $true)]
    [string]$ManifestUrl,

    [Parameter(Mandatory = $true)]
    [string]$Destination
)

$ErrorActionPreference = "Stop"
$toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$destinationPath = [IO.Path]::GetFullPath($Destination)
$partDirectory = "$destinationPath.parts"
[IO.Directory]::CreateDirectory($partDirectory) | Out-Null

Write-Host "Reading release manifest..."
$separator = if ($ManifestUrl.Contains("?")) { "&" } else { "?" }
$manifestRequestUrl = $ManifestUrl + $separator + "downloadCheck=" +
    [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$manifestResponse = Invoke-WebRequest -Uri $manifestRequestUrl -UseBasicParsing `
    -Headers @{ "Cache-Control" = "no-cache" }
$manifestContent = $manifestResponse.Content
if ($manifestContent -is [byte[]]) {
    $manifestContent = [Text.Encoding]::UTF8.GetString($manifestContent)
}
$manifestContent = $manifestContent.TrimStart([char]0xFEFF)
if ($manifestContent.Length -ge 3 -and
    [int]$manifestContent[0] -eq 239 -and
    [int]$manifestContent[1] -eq 187 -and
    [int]$manifestContent[2] -eq 191) {
    $manifestContent = $manifestContent.Substring(3)
}
$manifest = $manifestContent | ConvertFrom-Json
if (-not $manifest.parts -or -not $manifest.sha256) {
    throw "The release manifest is missing parts or the archive checksum."
}

$manifestUri = [Uri]$ManifestUrl
$partCount = $manifest.parts.Count

if (Test-Path -LiteralPath $destinationPath -PathType Leaf) {
    $existingArchive = Get-Item -LiteralPath $destinationPath
    if ([int64]$existingArchive.Length -eq [int64]$manifest.size) {
        Write-Host "A complete assembled archive already exists. Verifying it..."
        $existingArchiveHash =
            (Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256).Hash
        if ($existingArchiveHash -eq $manifest.sha256) {
            Write-Host "Existing archive is valid; no parts need to be downloaded."
            if (Test-Path -LiteralPath $partDirectory) {
                Remove-Item -LiteralPath $partDirectory -Recurse -Force
            }
            exit 0
        }
    }
    Write-Host "Existing assembled archive is incomplete or invalid; rebuilding it."
}

for ($index = 0; $index -lt $partCount; $index++) {
    $part = $manifest.parts[$index]
    $partPath = Join-Path $partDirectory $part.name
    $partUrl = [Uri]::new($manifestUri, [Uri]::EscapeDataString([string]$part.name)).AbsoluteUri

    Write-Host ""
    Write-Host ("Part {0}/{1}: {2}" -f ($index + 1), $partCount, $part.name)

    $validExistingPart = $false
    if (Test-Path -LiteralPath $partPath) {
        $existingPart = Get-Item -LiteralPath $partPath
        if ([int64]$existingPart.Length -eq [int64]$part.size) {
            $existingHash =
                (Get-FileHash -LiteralPath $partPath -Algorithm SHA256).Hash
            $validExistingPart = $existingHash -eq $part.sha256
            if (-not $validExistingPart) {
                Write-Host "Completed part has an invalid checksum; restarting it."
                Remove-Item -LiteralPath $partPath -Force
            }
        } elseif ([int64]$existingPart.Length -gt [int64]$part.size) {
            Write-Host "Existing part is larger than expected; restarting it."
            Remove-Item -LiteralPath $partPath -Force
        } else {
            Write-Host (
                "Resuming at {0:N2} MiB of {1:N2} MiB." -f
                ($existingPart.Length / 1MB), ([int64]$part.size / 1MB)
            )
        }
    }

    if ($validExistingPart) {
        Write-Host "Already downloaded and verified."
    } else {
        & "$toolDirectory\Download-FileWithProgress.ps1" `
            -Url $partUrl `
            -Destination $partPath

        $actualHash = (Get-FileHash -LiteralPath $partPath -Algorithm SHA256).Hash
        if ($actualHash -ne $part.sha256) {
            throw "Checksum verification failed for $($part.name)."
        }
    }
}

Write-Host ""
Write-Host "Joining $partCount verified parts..."
$output = New-Object IO.FileStream(
    $destinationPath,
    [IO.FileMode]::Create,
    [IO.FileAccess]::Write,
    [IO.FileShare]::None,
    8MB,
    [IO.FileOptions]::SequentialScan
)

try {
    $buffer = New-Object byte[] (8MB)
    for ($index = 0; $index -lt $partCount; $index++) {
        $partPath = Join-Path $partDirectory $manifest.parts[$index].name
        $input = [IO.File]::OpenRead($partPath)
        try {
            while (($read = $input.Read($buffer, 0, $buffer.Length)) -gt 0) {
                $output.Write($buffer, 0, $read)
            }
        } finally {
            $input.Dispose()
        }
        $percent = (($index + 1) * 100.0) / $partCount
        Write-Progress -Activity "Joining offline map archive" `
            -Status ("Part {0}/{1}" -f ($index + 1), $partCount) `
            -PercentComplete $percent
    }
} finally {
    $output.Dispose()
    Write-Progress -Activity "Joining offline map archive" -Completed
}

Write-Host "Verifying complete archive..."
$archiveHash = (Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256).Hash
if ($archiveHash -ne $manifest.sha256) {
    Remove-Item -LiteralPath $destinationPath -Force
    throw "Complete archive checksum verification failed."
}

Remove-Item -LiteralPath $partDirectory -Recurse -Force
Write-Host "Multipart package downloaded and verified."
