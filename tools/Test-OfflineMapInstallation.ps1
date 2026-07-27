param(
    [Parameter(Mandatory = $true)]
    [string]$ManifestUrl,

    [Parameter(Mandatory = $true)]
    [string]$Target
)

$ErrorActionPreference = "Stop"

function Read-JsonUrl([string]$Url) {
    $separator = if ($Url.Contains("?")) { "&" } else { "?" }
    $cacheBustedUrl = $Url + $separator + "inventoryCheck=" +
        [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $response = Invoke-WebRequest -Uri $cacheBustedUrl -UseBasicParsing `
        -Headers @{ "Cache-Control" = "no-cache" }
    $content = $response.Content
    if ($content -is [byte[]]) {
        $content = [Text.Encoding]::UTF8.GetString($content)
    }
    $content = $content.TrimStart([char]0xFEFF)
    if ($content.Length -ge 3 -and
        [int]$content[0] -eq 239 -and
        [int]$content[1] -eq 187 -and
        [int]$content[2] -eq 191) {
        $content = $content.Substring(3)
    }
    return $content | ConvertFrom-Json
}

function Get-RuntimeInventory([string]$Root) {
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd("\", "/")
    $mapPath = Join-Path $rootPath "map.gzd"
    $nodesPath = Join-Path $rootPath "nodes"

    if (-not (Test-Path -LiteralPath $mapPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $nodesPath -PathType Container)) {
        return $null
    }

    $files = @(
        Get-Item -LiteralPath $mapPath
        Get-ChildItem -LiteralPath $nodesPath -Recurse -File
    )
    if ($files.Count -lt 2) {
        return $null
    }

    $lines = New-Object string[] $files.Count
    $totalBytes = 0L
    for ($index = 0; $index -lt $files.Count; $index++) {
        $file = $files[$index]
        $relative = $file.FullName.Substring($rootPath.Length).
            TrimStart("\", "/").Replace("\", "/")
        $lines[$index] = "$relative|$($file.Length)"
        $totalBytes += $file.Length
    }
    [Array]::Sort($lines, [StringComparer]::Ordinal)

    $inventoryText = [string]::Join("`n", $lines)
    $hasher = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = (New-Object Text.UTF8Encoding($false)).GetBytes($inventoryText)
        $hashBytes = $hasher.ComputeHash($bytes)
    } finally {
        $hasher.Dispose()
    }

    return [pscustomobject]@{
        fileCount = $files.Count
        size = $totalBytes
        inventorySha256 = ([BitConverter]::ToString($hashBytes)).Replace("-", "")
    }
}

try {
    $manifest = Read-JsonUrl $ManifestUrl
    if ($null -eq $manifest.runtimeInventory -or
        -not $manifest.runtimeInventory.inventorySha256) {
        throw "Release manifest does not contain a runtime inventory."
    }

    $actual = Get-RuntimeInventory $Target
    if ($null -eq $actual) {
        Write-Host "No complete offline-map structure was found."
        exit 10
    }

    $expected = $manifest.runtimeInventory
    Write-Host (
        "Found {0} runtime files ({1:N2} GiB)." -f
        $actual.fileCount, ($actual.size / 1GB)
    )

    if ([int64]$actual.fileCount -ne [int64]$expected.fileCount -or
        [int64]$actual.size -ne [int64]$expected.size -or
        $actual.inventorySha256 -ne
            ([string]$expected.inventorySha256).ToUpperInvariant()) {
        Write-Host "Installed file inventory does not match the current release."
        exit 10
    }

    Write-Host "Installed file inventory matches the current release."
    exit 0
} catch {
    Write-Error $_
    exit 1
}
