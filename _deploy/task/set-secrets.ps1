<#
.SYNOPSIS
  Stores Coverage Manager secrets as USER-scope environment variables.

.DESCRIPTION
  Run this yourself, in your own PowerShell window, as the Windows account that
  runs the CoverageManager-* scheduled tasks. Each value is typed with hidden
  input and written straight to the user environment (HKCU\Environment). This
  script never prints, logs, or saves the values anywhere else.

  Default variables:
    Supabase__Key       service-role key; the API reads it as Supabase:Key (required)
  Optional (pass with -Names):
    LiveBridge__ApiKey  Live Bridge feed credential (only once the feed exists)

  MT5 Manager and LP (coverage) credentials are NOT environment variables in
  this app: enter them in the dashboard (Settings -> Connections); they are
  stored in the account_settings table.

  Restart the API task afterwards so it picks up new values:
    Stop-ScheduledTask CoverageManager-Api; Start-ScheduledTask CoverageManager-Api

  ASCII only: Windows PowerShell 5.1 reads BOM-less files as cp1252.
#>
param(
  [string[]]$Names = @('Supabase__Key'),
  [switch]$Remove
)

foreach ($name in $Names) {
  if ($Remove) {
    [Environment]::SetEnvironmentVariable($name, $null, 'User')
    Write-Host "  $name removed from the user environment"
    continue
  }

  $existing = [Environment]::GetEnvironmentVariable($name, 'User')
  $hint = if ($existing) { 'already set; leave empty to keep it' } else { 'not set yet' }
  $secure = Read-Host -AsSecureString "Value for $name ($hint, input hidden)"
  $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
  try   { $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
  finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }

  if ([string]::IsNullOrEmpty($plain)) {
    Write-Host "  $name unchanged"
    continue
  }
  [Environment]::SetEnvironmentVariable($name, $plain, 'User')
  $plain = $null
  Write-Host "  $name stored (User scope)"
}

Write-Host 'Done. Restart CoverageManager-Api to apply: Stop-ScheduledTask CoverageManager-Api; Start-ScheduledTask CoverageManager-Api'
