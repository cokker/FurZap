$ErrorActionPreference = 'Stop'
$expected = 'b51436e8960307316135e64ac14753b1f3b0e7a46afe1bd6081353b82de20f09'
$target = Join-Path $PSScriptRoot '..\tools\TgWsProxy_windows.exe'
$folder = Split-Path $target -Parent
New-Item -ItemType Directory -Path $folder -Force | Out-Null
if ((Test-Path $target) -and (Get-FileHash $target -Algorithm SHA256).Hash -eq $expected.ToUpperInvariant()) {
  Write-Host 'Verified official TG WS Proxy already present.'
  exit 0
}
$temp = "$target.download"
try {
  Invoke-WebRequest -Uri 'https://github.com/Flowseal/tg-ws-proxy/releases/download/v1.10.4/TgWsProxy_windows.exe' -OutFile $temp
  if ((Get-FileHash $temp -Algorithm SHA256).Hash -ne $expected.ToUpperInvariant()) { throw 'TG WS Proxy SHA-256 mismatch' }
  Move-Item -Path $temp -Destination $target -Force
  Write-Host 'Verified official TG WS Proxy v1.10.4.'
} finally {
  if (Test-Path $temp) { Remove-Item $temp -Force }
}
