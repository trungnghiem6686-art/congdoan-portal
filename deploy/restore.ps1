<#
.SYNOPSIS
  Khôi phục database và media từ một bản sao lưu do backup.ps1 tạo.

.DESCRIPTION
  THAO TÁC GHI ĐÈ DỮ LIỆU ĐANG CÓ. Cần thêm -Confirm:$false, hoặc trả lời xác nhận khi được hỏi.
  Các bước:
  1. Dừng API và web.
  2. Lưu lại media hiện tại thành <MediaDir>.before-restore-<thời điểm> để có thể quay lại.
  3. RESTORE DATABASE ... WITH REPLACE.
  4. Chép lại media (robocopy /MIR).
  5. Khởi động lại dịch vụ và gọi /api/health.
  Không chạy trong giờ thi. Bài đang làm sẽ trở về trạng thái tại thời điểm sao lưu.
  Thử định kỳ trên staging để biết bản sao lưu khôi phục được (xem docs/rollback.md).
  Phần PowerShell CHƯA KIỂM CHỨNG trên Windows.

.EXAMPLE
  .\restore.ps1 -SqlInstance localhost -Database CongDoan -BackupFile E:\congdoan-backup\CongDoan-20261027-013000.bak `
      -MediaBackupDir E:\congdoan-backup\media-20261027-013000 -MediaDir D:\congdoan-data\media
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = "High")]
param(
    [Parameter(Mandatory)] [string] $SqlInstance,
    [Parameter(Mandatory)] [string] $Database,
    [Parameter(Mandatory)] [string] $BackupFile,
    [Parameter(Mandatory)] [string] $MediaBackupDir,
    [Parameter(Mandatory)] [string] $MediaDir,
    [string] $ApiServiceName = "CongDoanApi",
    [string] $WebTaskName = "CongDoanWeb"
)

$ErrorActionPreference = "Stop"
if ($Database -notmatch '^[A-Za-z0-9_]+$') { throw "Tên database không hợp lệ." }
foreach ($p in @($BackupFile, $MediaBackupDir)) { if (-not (Test-Path $p)) { throw "Không thấy $p" } }
if (-not $PSCmdlet.ShouldProcess("$Database và $MediaDir", "Ghi đè bằng bản sao lưu $BackupFile")) { return }

sqlcmd -S $SqlInstance -E -b -Q "RESTORE VERIFYONLY FROM DISK = N'$BackupFile' WITH CHECKSUM"
if ($LASTEXITCODE -ne 0) { throw "Bản sao lưu không đọc được, dừng lại trước khi đụng tới dữ liệu." }

Write-Host "Dừng dịch vụ..."
Stop-ScheduledTask -TaskName $WebTaskName -ErrorAction SilentlyContinue
Stop-Service -Name $ApiServiceName -Force -ErrorAction SilentlyContinue

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$keep = "$MediaDir.before-restore-$stamp"
robocopy $MediaDir $keep /E /COPY:DAT /R:2 /W:5 /NP /NFL /NDL | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Không lưu được media hiện tại sang $keep (robocopy $LASTEXITCODE)." }
Write-Host "Media hiện tại đã lưu ở $keep"

Write-Host "Khôi phục database..."
sqlcmd -S $SqlInstance -E -b -Q "ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [$Database] FROM DISK = N'$BackupFile' WITH REPLACE, CHECKSUM; ALTER DATABASE [$Database] SET MULTI_USER;"
if ($LASTEXITCODE -ne 0) {
    sqlcmd -S $SqlInstance -E -Q "ALTER DATABASE [$Database] SET MULTI_USER;" | Out-Null
    throw "RESTORE DATABASE thất bại ($LASTEXITCODE). Dịch vụ vẫn đang dừng; kiểm tra trước khi khởi động lại."
}

Write-Host "Khôi phục media..."
robocopy $MediaBackupDir $MediaDir /MIR /COPY:DAT /R:2 /W:5 /NP /NFL /NDL | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Khôi phục media thất bại (robocopy $LASTEXITCODE). Bản trước khi khôi phục ở $keep." }

Write-Host "Khởi động lại dịch vụ..."
Start-Service -Name $ApiServiceName
Start-ScheduledTask -TaskName $WebTaskName
Start-Sleep -Seconds 10
$h = Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:5000/api/health" -TimeoutSec 15
Write-Host "API /api/health: $($h.StatusCode). Kiểm tra tiếp theo danh sách sau khôi phục trong docs/rollback.md."
