param(
    [Parameter(Mandatory = $true)]
    [string]$Url,

    [Parameter(Mandatory = $true)]
    [string]$Destination
)

$ErrorActionPreference = "Stop"
$uri = [Uri]$Url
$toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path

if ($uri.AbsolutePath.EndsWith(".json", [StringComparison]::OrdinalIgnoreCase)) {
    & "$toolDirectory\Download-MultipartRelease.ps1" `
        -ManifestUrl $Url `
        -Destination $Destination
} else {
    & "$toolDirectory\Download-FileWithProgress.ps1" `
        -Url $Url `
        -Destination $Destination
}

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
