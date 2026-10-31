# ponytail: slang-order subtitle pick. Exit 1 on fail.
# Keep in sync with SubtitleTrackUi.ParsePreferenceList / FindSidByPreferenceOrder.
$ErrorActionPreference = 'Stop'

function Test-AsciiLetterToken([string]$s) {
    if ([string]::IsNullOrEmpty($s)) { return $false }
    foreach ($c in $s.ToCharArray()) {
        if ([int][char]$c -gt 127 -or -not [char]::IsLetter($c)) { return $false }
    }
    return $true
}
function Test-TrackKeys([string]$text, [string[]]$keys) {
    if ([string]::IsNullOrEmpty($text)) { return $false }
    $h = $text.ToLowerInvariant()
    foreach ($key in $keys) {
        if ([string]::IsNullOrEmpty($key)) { continue }
        $k = $key.ToLowerInvariant()
        $codeLike = $k.Length -le 3 -and (Test-AsciiLetterToken $k)
        if ($codeLike) {
            $i = 0
            while (($i = $h.IndexOf($k, $i)) -ge 0) {
                $leftOk = ($i -eq 0) -or (-not [char]::IsLetterOrDigit($h[$i - 1]))
                $rightOk = ($i + $k.Length -ge $h.Length) -or (-not [char]::IsLetterOrDigit($h[$i + $k.Length]))
                if ($leftOk -and $rightOk) { return $true }
                $i += $k.Length
            }
        }
        elseif ($h.IndexOf($k) -ge 0) { return $true }
    }
    return $false
}
function Find-Sid([string[]]$prefs, $subs) {
    foreach ($key in $prefs) {
        if ([string]::IsNullOrEmpty($key)) { continue }
        foreach ($t in $subs) {
            $hay = ("$($t.Lang) $($t.Title)").Trim()
            if (-not $hay) { continue }
            if (Test-TrackKeys $hay @($key)) { return [int]$t.Id }
        }
    }
    return 0
}

$failed = 0
function Assert([bool]$cond, [string]$msg) {
    if ($cond) { Write-Host "ok: $msg" }
    else { Write-Host "FAIL: $msg"; $script:failed++ }
}

# eng default + 中文 title + chs lang: slang chs first → pick chs
$subs = @(
    @{ Id = 1; Lang = 'eng'; Title = 'English' },
    @{ Id = 2; Lang = ''; Title = '中文' },
    @{ Id = 3; Lang = 'chs'; Title = '' }
)
Assert ((Find-Sid @('chs','中文','eng') $subs) -eq 3) 'chs before 中文'
Assert ((Find-Sid @('中文','chs') $subs) -eq 2) '中文 before chs when ordered first'
Assert ((Find-Sid @('cht','繁体') @(
    @{ Id = 1; Lang = 'eng'; Title = '' },
    @{ Id = 2; Lang = ''; Title = '繁体中文' }
)) -eq 2) 'walks to 繁体'
Assert ((Find-Sid @('sc') @(
    @{ Id = 1; Lang = 'eng'; Title = 'Description' }
)) -eq 0) 'sc does not match Description'
Assert ((Find-Sid @('zh','chi') @(
    @{ Id = 1; Lang = 'chi'; Title = '' },
    @{ Id = 2; Lang = 'zh'; Title = '' }
)) -eq 2) 'zh key before chi key picks zh track first'

if ($failed -gt 0) { Write-Error "FAILED $failed"; exit 1 }
Write-Host 'all checks passed'
