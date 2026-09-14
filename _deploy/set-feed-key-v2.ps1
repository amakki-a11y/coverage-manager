# Coverage Manager v2 -- write the Live Bridge feed key file and lock its ACL.
#
# The key file (LiveBridge:ApiKeyFile) must be readable by the account the
# coverage-api-v2 service RUNS AS, and by nobody else except SYSTEM and
# Administrators. This script reads that account from the installed service
# (Win32_Service.StartName), so the ACL always matches the real service:
#
#   SYSTEM          Full    (LocalSystem services; also backup/restore)
#   Administrators  Full    (so an elevated admin can rotate the key)
#   <service acct>  Read    (only when it is not already SYSTEM)
#
# Inheritance is removed, so the folder's other grants (e.g. makkioo on the
# secrets folder) do not reach the file. Broad principals (Everyone, Users,
# Authenticated Users, Interactive, Guests) are refused as a service account.
#
# The key is typed at a masked prompt: not on the command line, not in
# PSReadLine history, not on the clipboard. It is never printed.
#
# Usage (ELEVATED Windows PowerShell, repo root):
#   .\_deploy\set-feed-key-v2.ps1                         # service installed: account read from it
#   .\_deploy\set-feed-key-v2.ps1 -ServiceAccount LocalSystem   # before the service is installed
#   .\_deploy\set-feed-key-v2.ps1 -AclOnly                # re-apply the ACL (e.g. after changing the
#                                                         # service account); key untouched
#
# Afterwards, with LiveBridge:Enabled still false, start the service and check
#   http://127.0.0.1:5100/api/exposure/diagnostics -> feedKey.readable = true, feedKey.runningAs = the service account
# (a read-only preflight; nothing dials).
#
# Encoding: ASCII only.

param(
    [string]$ServiceName = "coverage-api-v2",
    [string]$ServiceAccount = "",
    [string]$KeyFile = "C:\ProgramData\CoverageManagerV2\secrets\livebridge_v2_key.txt",
    [switch]$AclOnly = $false
)

$ErrorActionPreference = "Stop"

function Refuse($msg) {
    Write-Host "REFUSED: $msg" -ForegroundColor Red
    exit 2
}

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Refuse "run from an elevated PowerShell."
}

# ---- Which account does the service run as? --------------------------------
$svc = Get-CimInstance Win32_Service -Filter "Name='$ServiceName'" -ErrorAction SilentlyContinue
if ($svc) {
    if ($ServiceAccount -and ($ServiceAccount -ne $svc.StartName)) {
        Refuse "service $ServiceName is installed as '$($svc.StartName)', not '$ServiceAccount'. Drop -ServiceAccount or fix the service first."
    }
    $account = $svc.StartName
    Write-Host "Service $ServiceName is installed; runs as: $account"
} elseif ($ServiceAccount) {
    $account = $ServiceAccount
    Write-Host "Service $ServiceName is not installed; using -ServiceAccount $account. Re-run with -AclOnly after installing if the account differs." -ForegroundColor Yellow
} else {
    Refuse "service $ServiceName is not installed. Pass -ServiceAccount (the runbook plans LocalSystem) or install the service first."
}

# ---- Account -> SID ---------------------------------------------------------
switch -Regex ($account) {
    '^(LocalSystem|NT AUTHORITY\\SYSTEM|\.\\LocalSystem)$'                   { $sid = 'S-1-5-18'; break }
    '^(NT AUTHORITY\\LocalService|NT AUTHORITY\\LOCAL SERVICE)$'             { $sid = 'S-1-5-19'; break }
    '^(NT AUTHORITY\\NetworkService|NT AUTHORITY\\NETWORK SERVICE)$'         { $sid = 'S-1-5-20'; break }
    default {
        $name = $account
        if ($name.StartsWith('.\')) { $name = "$env:COMPUTERNAME\" + $name.Substring(2) }
        try {
            $sid = (New-Object Security.Principal.NTAccount($name)).Translate([Security.Principal.SecurityIdentifier]).Value
        } catch {
            Refuse "cannot resolve account '$account' to a SID."
        }
    }
}
$broad = @('S-1-1-0', 'S-1-5-11', 'S-1-5-32-545', 'S-1-5-4', 'S-1-5-32-546', 'S-1-5-7')
if ($broad -contains $sid) { Refuse "account '$account' ($sid) is a broad group; the key would be readable by other users." }

# ---- Create the file (empty), lock the ACL, then write the key -------------
$dir = Split-Path $KeyFile -Parent
if (-not (Test-Path $dir)) { Refuse "folder $dir does not exist." }
if ($AclOnly -and -not (Test-Path $KeyFile)) { Refuse "-AclOnly: $KeyFile does not exist." }
if (-not (Test-Path $KeyFile)) { New-Item -ItemType File -Path $KeyFile | Out-Null }

$grants = @('*S-1-5-18:F', '*S-1-5-32-544:F')
if ($sid -ne 'S-1-5-18') { $grants += "*${sid}:R" }
& icacls $KeyFile /inheritance:r /grant:r $grants | Out-Null
if ($LASTEXITCODE -ne 0) { Refuse "icacls failed (exit $LASTEXITCODE); key not written." }
# Remove every other explicit grant that may already be on an existing file.
$keep = @('S-1-5-18', 'S-1-5-32-544', $sid)
$acl = Get-Acl $KeyFile
foreach ($ace in @($acl.Access)) {
    $aceSid = $ace.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value
    if ($keep -notcontains $aceSid) { [void]$acl.RemoveAccessRuleAll($ace) }
}
Set-Acl -Path $KeyFile -AclObject $acl

if (-not $AclOnly) {
    $secure = Read-Host 'coverage-manager-v2 key' -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try {
        $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
        if ([string]::IsNullOrWhiteSpace($plain)) { Refuse "empty key; nothing written." }
        [IO.File]::WriteAllText($KeyFile, $plain.Trim(), [Text.Encoding]::ASCII)
    } finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        $plain = $null
    }
}

Write-Host ""
Write-Host "Key file: $KeyFile" -ForegroundColor Cyan
& icacls $KeyFile
Write-Host ("Length (characters): " + (Get-Item $KeyFile).Length)
Write-Host "Readable by: SYSTEM, Administrators, and $account ($sid). Nobody else." -ForegroundColor Green
