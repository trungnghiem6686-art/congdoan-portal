<#
.SYNOPSIS
  Sao lưu database và thư mục media, kiểm tra bản sao lưu, xóa bản cũ quá hạn.

.DESCRIPTION
  - BACKUP DATABASE ... WITH CHECKSUM, rồi RESTORE VERIFYONLY để chắc tệp đọc lại được.
  - Media được sao chép vào thư mục có dấu thời gian. robocopy chỉ đọc nguồn, không xóa gì ở thư mục đang dùng.
  - Dùng xác thực Windows (sqlcmd -E). Không truyền mật khẩu qua tham số dòng lệnh.
  - Ghi nhật ký vào <BackupDir>\backup.log. Mã thoát khác 0 nghĩa là sao lưu không đạt.
  Lên lịch bằng Task Scheduler, ví dụ mỗi đêm lúc 01:30, ngoài giờ thi.
  Lệnh T-SQL đã thử trên SQL Server 2022 (Linux container). Phần PowerShell CHƯA KIỂM CHỨNG trên Windows.

.EXAMPLE
  .\backup.ps1 -SqlInstance localhost -Database CongDoan -MediaDir D:\congdoan-data\media -BackupDir E:\congdoan-backup
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $SqlInstance,
    [Parameter(Mandatory)] [string] $Database,
    [Parameter(Mandatory)] [string] $MediaDir,
    [Parameter(Mandatory)] [string] $BackupDir,
    [int] $RetentionDays = 14,
    # SQL Server Express không hỗ trợ nén bản sao lưu.
    [switch] $NoCompression
)

$ErrorActionPreference = "Stop"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$log = Join-Path $BackupDir "backup.log"
New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
function Log([string] $m) { "$(Get-Date -Format s) $m" | Tee-Object -FilePath $log -Append }

if ($Database -notmatch '^[A-Za-z0-9_]+$') { throw "Tên database không hợp lệ." }
$bak = Join-Path $BackupDir "$Database-$stamp.bak"
$compression = if ($NoCompression) { "" } else { "COMPRESSION, " }

Log "Bắt đầu sao lưu $Database → $bak"
sqlcmd -S $SqlInstance -E -b -Q "BACKUP DATABASE [$Database] TO DISK = N'$bak' WITH $compression CHECKSUM, INIT, NAME = N'$Database $stamp'"
if ($LASTEXITCODE -ne 0) { Log "LỖI: BACKUP DATABASE thất bại ($LASTEXITCODE)"; exit 1 }
sqlcmd -S $SqlInstance -E -b -Q "RESTORE VERIFYONLY FROM DISK = N'$bak' WITH CHECKSUM"
if ($LASTEXITCODE -ne 0) { Log "LỖI: RESTORE VERIFYONLY thất bại ($LASTEXITCODE)"; exit 1 }
Log "Database: đã sao lưu và kiểm tra $bak ($([math]::Round((Get-Item $bak).Length / 1MB, 1)) MB)"

$mediaOut = Join-Path $BackupDir "media-$stamp"
robocopy $MediaDir $mediaOut /E /COPY:DAT /R:2 /W:5 /NP /NFL /NDL /LOG+:$log | Out-Null
# robocopy: 0–7 là thành công (có hoặc không có tệp được chép), từ 8 trở lên là lỗi.
if ($LASTEXITCODE -ge 8) { Log "LỖI: sao chép media thất bại (robocopy $LASTEXITCODE)"; exit 1 }
$srcCount = (Get-ChildItem -Recurse -File $MediaDir | Measure-Object).Count
$dstCount = (Get-ChildItem -Recurse -File $mediaOut | Measure-Object).Count
Log "Media: $dstCount/$srcCount tệp → $mediaOut"
if ($dstCount -lt $srcCount) { Log "LỖI: số tệp media sao lưu ít hơn nguồn"; exit 1 }

$cutoff = (Get-Date).AddDays(-$RetentionDays)
Get-ChildItem $BackupDir -Filter "$Database-*.bak" | Where-Object LastWriteTime -lt $cutoff | ForEach-Object { Log "Xóa bản cũ $($_.Name)"; Remove-Item $_.FullName }
Get-ChildItem $BackupDir -Directory -Filter "media-*" | Where-Object LastWriteTime -lt $cutoff | ForEach-Object { Log "Xóa bản cũ $($_.Name)"; Remove-Item $_.FullName -Recurse }
Log "Hoàn tất."
exit 0
