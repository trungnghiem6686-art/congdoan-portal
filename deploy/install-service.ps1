<#
.SYNOPSIS
  Cài (hoặc cập nhật) dịch vụ chạy nền cho Cổng thông tin Công đoàn trên Windows Server.

.DESCRIPTION
  - API (.NET 8, Kestrel): cài thành Windows Service "CongDoanApi", tự khởi động và tự khởi động lại khi lỗi.
  - Web (Next.js standalone, chạy bằng node server.js): đăng ký Scheduled Task "CongDoanWeb" chạy lúc máy khởi động,
    tự chạy lại khi dừng. Cách này không cần phần mềm bên thứ ba để bọc node thành service.
  - Biến môi trường, gồm cả bí mật, đọc từ các tệp KEY=VALUE nằm ngoài thư mục release (tham số -ApiEnvFile, -WebEnvFile).
    Script không in giá trị của biến ra màn hình.
  - Tạo các thư mục dữ liệu bền vững (media, logs, backups) và cấp quyền Modify cho tài khoản chạy dịch vụ.

  CHƯA KIỂM CHỨNG trên Windows Server thật. Chạy thử trên staging trước.

.EXAMPLE
  .\install-service.ps1 -ApiDir D:\congdoan\releases\2026-10-27\api -WebDir D:\congdoan\releases\2026-10-27\web `
      -DataDir D:\congdoan-data -ApiEnvFile D:\congdoan-data\config\api.env -WebEnvFile D:\congdoan-data\config\web.env `
      -ServiceAccount "NT SERVICE\CongDoanApi"
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)] [string] $ApiDir,
    [Parameter(Mandatory)] [string] $WebDir,
    [Parameter(Mandatory)] [string] $DataDir,
    [Parameter(Mandatory)] [string] $ApiEnvFile,
    [Parameter(Mandatory)] [string] $WebEnvFile,
    # Tài khoản ảo của service (mặc định) hoặc tài khoản miền có ít quyền.
    [string] $ServiceAccount = "NT SERVICE\CongDoanApi",
    [string] $WebTaskUser = "NT AUTHORITY\LOCAL SERVICE",
    [string] $NodeExe = "C:\Program Files\nodejs\node.exe",
    [string] $ApiServiceName = "CongDoanApi",
    [string] $WebTaskName = "CongDoanWeb"
)

$ErrorActionPreference = "Stop"

function Read-EnvFile([string] $path) {
    if (-not (Test-Path $path)) { throw "Không thấy tệp cấu hình $path" }
    $pairs = @()
    foreach ($line in Get-Content -Path $path -Encoding UTF8) {
        $t = $line.Trim()
        if ($t.Length -eq 0 -or $t.StartsWith("#")) { continue }
        if ($t -notmatch "^[A-Za-z_][A-Za-z0-9_]*=") { throw "Dòng không hợp lệ trong $path (cần KEY=VALUE)." }
        $pairs += $t
    }
    return ,$pairs
}

$apiExe = Join-Path $ApiDir "CongDoan.Api.exe"
$serverJs = Join-Path $WebDir "server.js"
foreach ($p in @($apiExe, $serverJs, $NodeExe)) { if (-not (Test-Path $p)) { throw "Không thấy $p" } }

# 1. Thư mục dữ liệu bền vững, nằm ngoài thư mục release.
foreach ($sub in @("media", "logs", "backups", "config")) {
    $dir = Join-Path $DataDir $sub
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
}

# 2. API service.
$apiEnv = Read-EnvFile $ApiEnvFile
$existing = Get-Service -Name $ApiServiceName -ErrorAction SilentlyContinue
if ($existing) {
    if ($existing.Status -ne "Stopped") { Stop-Service -Name $ApiServiceName -Force }
    sc.exe config $ApiServiceName binPath= "`"$apiExe`"" start= delayed-auto obj= "$ServiceAccount" | Out-Null
} else {
    New-Service -Name $ApiServiceName -BinaryPathName "`"$apiExe`"" -DisplayName "Cong Doan API" -StartupType Automatic | Out-Null
    sc.exe config $ApiServiceName start= delayed-auto obj= "$ServiceAccount" | Out-Null
}
# Restart on failure: 5s, 5s, then 30s; reset the counter after a day.
sc.exe failure $ApiServiceName reset= 86400 actions= restart/5000/restart/5000/restart/30000 | Out-Null
# Service environment (REG_MULTI_SZ). The registry key is readable by administrators and SYSTEM only.
$svcKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$ApiServiceName"
$apiEnv += "ASPNETCORE_URLS=http://127.0.0.1:5000"
$apiEnv += "ASPNETCORE_CONTENTROOT=$ApiDir"
New-ItemProperty -Path $svcKey -Name Environment -PropertyType MultiString -Value $apiEnv -Force | Out-Null
Write-Host "Đã cấu hình service $ApiServiceName với $($apiEnv.Count) biến môi trường (không in giá trị)."

# 3. Quyền thư mục: tài khoản service được ghi media/logs, chỉ đọc release.
icacls (Join-Path $DataDir "media") /grant "${ServiceAccount}:(OI)(CI)M" | Out-Null
icacls (Join-Path $DataDir "logs") /grant "${ServiceAccount}:(OI)(CI)M" | Out-Null
icacls $ApiDir /grant "${ServiceAccount}:(OI)(CI)RX" | Out-Null
icacls (Join-Path $DataDir "config") /inheritance:r /grant "Administrators:(OI)(CI)F" "SYSTEM:(OI)(CI)F" | Out-Null

# 4. Web (Next.js) as a startup task that restarts itself.
$webEnv = Read-EnvFile $WebEnvFile
$launcher = Join-Path $WebDir "start-web.cmd"
$lines = @("@echo off", "cd /d `"$WebDir`"", "set PORT=3000", "set HOSTNAME=127.0.0.1", "set NODE_ENV=production")
foreach ($kv in $webEnv) { $lines += "set `"$kv`"" }
$lines += "`"$NodeExe`" server.js >> `"$(Join-Path $DataDir 'logs\web.log')`" 2>&1"
Set-Content -Path $launcher -Value $lines -Encoding ASCII
icacls $launcher /inheritance:r /grant "Administrators:F" "SYSTEM:F" "${WebTaskUser}:RX" | Out-Null

$action = New-ScheduledTaskAction -Execute "cmd.exe" -Argument "/c `"$launcher`""
$trigger = New-ScheduledTaskTrigger -AtStartup
$settings = New-ScheduledTaskSettingsSet -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) `
    -ExecutionTimeLimit ([TimeSpan]::Zero) -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
$principal = New-ScheduledTaskPrincipal -UserId $WebTaskUser -LogonType ServiceAccount
if (Get-ScheduledTask -TaskName $WebTaskName -ErrorAction SilentlyContinue) {
    Stop-ScheduledTask -TaskName $WebTaskName
    Unregister-ScheduledTask -TaskName $WebTaskName -Confirm:$false
}
Register-ScheduledTask -TaskName $WebTaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal | Out-Null

# 5. Start and check.
Start-Service -Name $ApiServiceName
Start-ScheduledTask -TaskName $WebTaskName
Start-Sleep -Seconds 10
try {
    $h = Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:5000/api/health" -TimeoutSec 10
    Write-Host "API: $($h.StatusCode)"
} catch { Write-Warning "API chưa trả lời /api/health: $($_.Exception.Message). Xem $DataDir\logs." }
try {
    $w = Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:3000/" -TimeoutSec 20
    Write-Host "Web: $($w.StatusCode)"
} catch { Write-Warning "Web chưa trả lời: $($_.Exception.Message). Xem $DataDir\logs\web.log." }
