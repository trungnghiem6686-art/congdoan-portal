# Triển khai trên Windows Server + IIS

> **Trạng thái:** chưa có thông tin máy chủ thật (phiên bản Windows Server, IIS, SQL Server, chứng chỉ). Các bước dưới đây chưa được chạy trên Windows.
>
> Đã kiểm chứng trên môi trường phát triển Linux:
> - build và publish;
> - thư mục chạy Next.js standalone;
> - script migration idempotent (áp dụng 2 lần lên database trống, kết quả khớp);
> - lệnh T-SQL sao lưu/khôi phục.
>
> Làm toàn bộ trên **staging** trước. Không thử trên trang đang hoạt động.

## 1. Sơ đồ

```
Trình duyệt ──HTTPS──> IIS (site, chỉ có web.config)
                        ├─ /api/*, /media/*  ─> Kestrel  127.0.0.1:5000  (Windows Service "CongDoanApi")
                        └─ còn lại           ─> Next.js  127.0.0.1:3000  (Scheduled Task "CongDoanWeb")
Kestrel ─> SQL Server (database CongDoan, 4 schema: identity, audit, cms, exam)
Kestrel ─> D:\congdoan-data\media  (ngoài thư mục release, IIS không phục vụ trực tiếp)
```

Cấu trúc thư mục đề xuất:

```
D:\congdoan\releases\<ngày-giờ>\api      bản publish API (thay mới mỗi lần phát hành)
D:\congdoan\releases\<ngày-giờ>\web      bản build Next.js standalone
D:\congdoan\site                         thư mục site IIS, chỉ chứa web.config
D:\congdoan-data\media                   tệp tải lên — KHÔNG BAO GIỜ nằm trong releases
D:\congdoan-data\logs                    log API và web, xoay vòng theo ngày, giữ 30 ngày
D:\congdoan-data\config\api.env          biến môi trường API (chứa bí mật; chỉ Administrators/SYSTEM đọc)
D:\congdoan-data\config\web.env          biến môi trường web
E:\congdoan-backup                       bản sao lưu (nên ở ổ hoặc máy khác)
```

## 2. Cài đặt một lần

1. **.NET 8 Hosting Bundle** (ASP.NET Core Runtime 8.0.x) và **Node.js 22 LTS**.
2. IIS: bật vai trò Web Server. Cài **URL Rewrite 2.1** và **Application Request Routing 3.0**.
3. IIS Manager → máy chủ → Application Request Routing Cache → *Server Proxy Settings*:
   - bật **Enable proxy**;
   - bỏ chọn *Reverse rewrite host in response headers*;
   - giữ chọn *Preserve client IP in the following header: X-Forwarded-For*.
4. Cho phép biến máy chủ dùng trong `web.config`: IIS Manager → site → URL Rewrite → *View Server Variables* → thêm `HTTP_X_FORWARDED_PROTO`. Hoặc chạy lệnh:
   ```powershell
   & "$env:windir\system32\inetsrv\appcmd.exe" set config -section:system.webServer/rewrite/allowedServerVariables /+"[name='HTTP_X_FORWARDED_PROTO']" /commit:apphost
   ```
5. **SQL Server:**
   - tạo database `CongDoan`;
   - tạo đăng nhập cho tài khoản dịch vụ. Với tài khoản ảo mặc định thì dùng `CREATE LOGIN [NT SERVICE\CongDoanApi] FROM WINDOWS`;
   - cấp quyền `db_datareader`, `db_datawriter`;
   - **không** cấp `db_owner` cho ứng dụng. Migration chạy bằng tài khoản quản trị riêng (mục 4).
6. **TLS:**
   - gắn chứng chỉ cho binding `https` của site;
   - sau khi HTTPS chạy ổn định, bật rule *Force HTTPS* và header HSTS trong `deploy/web.config`.

   Nếu máy chủ hiện chỉ có HTTP: phải cấu hình chứng chỉ trên chính máy chủ đó trước khi cho đăng nhập hoặc thi thật. Không tắt bảo vệ để chạy tạm.

## 3. Biến môi trường

`D:\congdoan-data\config\api.env`, mỗi dòng một cặp `KEY=VALUE`. Đây là ví dụ, thay giá trị thật trên máy chủ và không đưa vào Git:

```
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Default=Server=localhost;Database=CongDoan;Integrated Security=True;TrustServerCertificate=True
Media__RootPath=D:\congdoan-data\media
Logging__File__Path=D:\congdoan-data\logs\api-.log
Frontend__RevalidateUrl=http://127.0.0.1:3000/next-api/revalidate
Frontend__RevalidateSecret=<chuỗi ngẫu nhiên dài, trùng với REVALIDATE_SECRET của web>
Database__MigrateOnStartup=false
# Chỉ đặt ở lần chạy đầu để tạo quản trị viên ban đầu, sau đó XÓA hai dòng này và khởi động lại dịch vụ:
Bootstrap__AdminUserName=quantri
Bootstrap__AdminPassword=<mật khẩu tạm, buộc đổi khi đăng nhập lần đầu>
```

`D:\congdoan-data\config\web.env`:

```
API_BASE_URL=http://127.0.0.1:5000
REVALIDATE_SECRET=<cùng giá trị Frontend__RevalidateSecret>
NEXT_PUBLIC_SITE_URL=https://<tên miền>
```

Một số giá trị tùy chọn:
- `RateLimit__LoginPermitPerMinute`: mặc định 10 lần mỗi IP mỗi phút.
- `RateLimit__ContactPermitPerMinute`: mặc định 5.
- `ReverseProxy__KnownProxies__0=<IP proxy>`: chỉ cần khi IIS nằm trên máy khác. Mặc định chỉ tin proxy trên chính máy, tức `127.0.0.1` và `::1`.

## 4. Đóng gói một bản phát hành

Đóng gói trên máy build, không làm trên máy chủ chạy thật. Bắt đầu từ một commit Git đã gắn tag:

```powershell
git checkout v1.0.0
# API
dotnet publish backend\CongDoan.Api -c Release -r win-x64 --self-contained false -o out\api
# Script migration idempotent cho từng module (chạy lại an toàn)
foreach ($c in "IdentityDbContext","AuditDbContext","CmsDbContext","ExamDbContext") {
  dotnet ef migrations script --idempotent --project backend\CongDoan.Infrastructure `
    --startup-project backend\CongDoan.Infrastructure --context $c -o out\sql\$c.sql
}
# Web
cd frontend; npm ci; npm run build; cd ..
Copy-Item frontend\.next\standalone out\web -Recurse
Copy-Item frontend\.next\static out\web\.next\static -Recurse
# Công cụ nhập dữ liệu cũ (khi cần)
dotnet publish backend\tools\CongDoan.Importer -c Release -r win-x64 --self-contained false -o out\importer
```

Ghi tag Git và commit vào tên thư mục release, ví dụ `releases\2026-10-27_v1.0.0_68739bb`, để truy vết được bản nào đang chạy.

## 5. Phát hành (lần đầu và các lần sau)

**Không phát hành khi đang có kỳ thi mở.** Kiểm tra mục *Kỳ thi* trong trang quản trị, hoặc đếm lượt đang làm:

```sql
SELECT COUNT(*) FROM exam.Attempts WHERE Status = 'InProgress';
```

Lượt thi lưu thời hạn trong database, nên dịch vụ khởi động lại không kéo dài giờ thi. Tuy vậy, trong vài giây dịch vụ dừng, autosave của thí sinh sẽ báo "chưa lưu được" rồi tự gửi lại. Không nên gây ra điều này giữa giờ thi.

1. **Sao lưu:**
   ```powershell
   deploy\backup.ps1 -SqlInstance localhost -Database CongDoan -MediaDir D:\congdoan-data\media -BackupDir E:\congdoan-backup
   ```
   Mã thoát phải là 0.
2. Chép `out\api`, `out\web` vào `D:\congdoan\releases\<tên bản>`. Không chép đè thư mục media, config hay logs.
3. **Migration** (bằng tài khoản SQL có quyền DDL). Luôn thêm `-I`, vì chỉ mục lọc cần `QUOTED_IDENTIFIER ON`:
   ```powershell
   foreach ($c in "IdentityDbContext","AuditDbContext","CmsDbContext","ExamDbContext") {
     sqlcmd -S localhost -d CongDoan -E -b -I -i out\sql\$c.sql
     if ($LASTEXITCODE -ne 0) { throw "Migration $c lỗi — dừng, xem docs/rollback.md" }
   }
   ```
4. **Cài hoặc trỏ dịch vụ sang bản mới.** Script tự dừng dịch vụ cũ, cập nhật đường dẫn và khởi động lại:
   ```powershell
   deploy\install-service.ps1 -ApiDir D:\congdoan\releases\<bản>\api -WebDir D:\congdoan\releases\<bản>\web `
     -DataDir D:\congdoan-data -ApiEnvFile D:\congdoan-data\config\api.env -WebEnvFile D:\congdoan-data\config\web.env
   ```
5. Lần đầu: tạo site IIS trỏ tới `D:\congdoan\site`, chép `deploy\web.config` vào đó, gắn binding `http` và `https`. App pool để *No Managed Code*, vì site chỉ làm proxy.
6. Lần đầu: đăng nhập `/admin` bằng tài khoản bootstrap và đổi mật khẩu. Sau đó **xóa** hai dòng `Bootstrap__…` khỏi `api.env` rồi chạy lại bước 4.

## 6. Kiểm tra sau phát hành (smoke test)

- [ ] `https://<miền>/api/health` trả `{"success":true,…}`.
- [ ] Trang chủ, một bài viết, một chuyên mục, tìm kiếm, `/sitemap.xml`, `/robots.txt` hiển thị đúng. Thử trên điện thoại.
- [ ] Đăng nhập `/admin`. Cookie phải có cờ `Secure` khi truy cập qua HTTPS: kiểm tra trong DevTools → Application → Cookies.
- [ ] **Tải một ảnh MỚI** trong *Tệp đã tải lên*, chèn vào một bài nháp và xem trước. Ảnh hiển thị tức là đường `/media` qua IIS và quyền ghi thư mục media đều đúng. Không chỉ kiểm tra ảnh đã có sẵn.
- [ ] Tải và mở một văn bản PDF. Một tệp "hạn chế" phải trả 401/403 khi chưa đăng nhập.
- [ ] Xuất bản một thay đổi nhỏ trên trang chủ. Trang công khai phải cập nhật trong vài giây mà không cần phát hành lại; điều này xác nhận đường revalidate từ API sang web hoạt động.
- [ ] Đăng nhập `/thi` bằng một thí sinh thử, mở một kỳ thi thử, trả lời một câu, tải lại trang, rồi nộp bài.
- [ ] `D:\congdoan-data\logs\api-<ngày>.log` có log mới và không chứa mật khẩu hay token.
- [ ] Khởi động lại máy chủ, hoặc lần lượt `Restart-Service CongDoanApi` rồi `Stop-ScheduledTask`/`Start-ScheduledTask CongDoanWeb`. Hai dịch vụ phải tự chạy lại, bài viết hẹn giờ vẫn được xuất bản, lượt thi hết giờ vẫn được đóng.

## 7. Vận hành

- **Sao lưu:** lên lịch `backup.ps1` mỗi đêm ngoài giờ thi bằng Task Scheduler. Mỗi tháng thử khôi phục một bản lên staging (xem `docs/rollback.md`).
- **Log:** API ghi `api-YYYYMMDD.log`, mỗi tệp tối đa 50 MB, giữ 30 tệp. Next.js ghi `web-YYYYMMDD.log`, tự xóa tệp cũ hơn 30 ngày.
- **Cảnh báo:** theo dõi `/api/health` bằng công cụ giám sát sẵn có của đơn vị, cùng dung lượng ổ chứa media và backup.
- **Media:** dịch vụ chỉ có quyền Modify trên `media` và `logs`. IIS không có đường dẫn nào trỏ vào thư mục media, nên tệp tải lên không bao giờ được thực thi. Tệp hạn chế chỉ phục vụ qua API, có kiểm tra quyền.
- **Đổi bí mật:**
  1. Sửa `api.env` / `web.env`; `REVALIDATE_SECRET` phải giống nhau ở hai tệp.
  2. Chạy lại `install-service.ps1`.
  3. Mọi phiên đăng nhập vẫn còn hiệu lực, vì phiên lưu trong database. Muốn buộc đăng nhập lại thì khóa rồi mở tài khoản.

## 8. Lỗi thường gặp

| Hiện tượng | Kiểm tra |
|---|---|
| 502.3 từ IIS | Dịch vụ API/web chưa chạy: `Get-Service CongDoanApi`, `Get-ScheduledTask CongDoanWeb`, xem log |
| 500.50 "rewrite" | Chưa cho phép biến `HTTP_X_FORWARDED_PROTO` (mục 2 bước 4) |
| Đăng nhập xong vẫn bị đẩy ra | Cookie `Secure` nhưng truy cập bằng HTTP: bật HTTPS hoặc kiểm tra `X-Forwarded-Proto` |
| Mọi người cùng bị "thao tác quá nhanh" khi đăng nhập | IIS không gửi `X-Forwarded-For` (bật *Preserve client IP*); proxy ở máy khác thì khai báo `ReverseProxy__KnownProxies__0` |
| Ảnh mới tải lên lỗi 500 | Tài khoản dịch vụ thiếu quyền ghi `Media__RootPath` |
| Trang công khai không cập nhật sau khi xuất bản | `Frontend__RevalidateUrl`/`REVALIDATE_SECRET` không khớp. Tìm "revalidate" trong log API |
| Migration báo lỗi 1934 | Thiếu tham số `-I` của sqlcmd |
