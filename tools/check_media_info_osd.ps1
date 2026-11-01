# ponytail: MediaInfoOsdFontSize / LineEstimate. Keep in sync with Commands.Media.cs.
$ErrorActionPreference = 'Stop'

function MediaInfoOsdLineEstimate([string]$text) {
    if ([string]::IsNullOrEmpty($text)) { return 1 }
    $lines = 0
    foreach ($part in $text -split "`n") {
        $len = $part.Length
        if ($len -le 0) { $lines += 1 }
        else { $lines += [int][Math]::Floor(($len + 89) / 90) }
    }
    if ($lines -lt 1) { return 1 }
    return $lines
}

function MediaInfoOsdFontSize([int]$oh, [int]$lines) {
    if ($lines -lt 1) { $lines = 1 }
    if ($oh -lt 1) { $oh = 720 }
    $n = [int][Math]::Floor(($oh - 64) / ($lines * 1.5))
    if ($n -lt 8) { return 8 }
    if ($n -gt 18) { return 18 }
    return $n
}

$failed = 0
function Assert([bool]$cond, [string]$msg) {
    if ($cond) { Write-Host "ok: $msg" }
    else { Write-Host "FAIL: $msg"; $script:failed++ }
}

Assert ((MediaInfoOsdLineEstimate "a`nb`nc") -eq 3) 'basic lines'
Assert ((MediaInfoOsdLineEstimate ('N: ' + ('x' * 200))) -ge 3) 'long line wraps'
Assert ((MediaInfoOsdFontSize 720 5) -eq 18) 'short list caps at 18'
Assert ((MediaInfoOsdFontSize 600 30) -le 12) '30 lines shrinks on 600px'
Assert ((MediaInfoOsdFontSize 600 50) -eq 8) '50 lines hits floor'
Assert ((MediaInfoOsdFontSize 0 10) -eq (MediaInfoOsdFontSize 720 10)) 'bad height → 720'

if ($failed -gt 0) { Write-Error "FAILED $failed"; exit 1 }
Write-Host 'all checks passed'
