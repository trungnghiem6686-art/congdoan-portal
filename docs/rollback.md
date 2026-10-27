# Quay lại bản trước (rollback) và khôi phục

## Nguyên tắc

- Mỗi bản phát hành nằm trong thư mục riêng `D:\congdoan\releases\<bản>`. Media, config và log nằm ngoài các thư mục đó, nên đổi bản không làm mất dữ liệu.
- Quay lại bản trước chỉ bằng **đổi thư mục** khi schema database của bản mới **tương thích** với bản cũ.
- Khi migration đã đổi hoặc xóa dữ liệu, đổi thư mục là chưa đủ: phải khôi phục database từ bản sao lưu chụp ngay trước khi phát hành (bước 1 trong `docs/deploy-iis.md` mục 5).
- Không thực hiện trong giờ thi. Mọi bài đang làm sẽ trở về trạng thái tại thời điểm sao lưu.

## Quy ước migration để rollback được

Migration chỉ thêm, không phá:
- thêm bảng, thêm cột cho phép NULL hoặc có giá trị mặc định;
- thêm chỉ mục.

Bản cũ bỏ qua những thứ nó không biết, nên vẫn chạy được trên schema mới. Ví dụ: cột `SourceKey` để nhập dữ liệu là cột cho phép NULL.

Thay đổi phá schema tách làm **hai lần phát hành**:
1. Lần 1: thêm cấu trúc mới, ghi cả hai nơi.
2. Lần 2, khi lần 1 đã chạy ổn: bỏ cấu trúc cũ.

Ví dụ thay đổi phá schema: đổi tên cột, xóa cột, đổi kiểu dữ liệu.

Mỗi bản phát hành ghi trong ghi chú phát hành một trong hai:
- "schema tương thích ngược", hoặc
- "**không** tương thích — rollback cần khôi phục database".

## Cách 1: chỉ đổi lại bản (schema tương thích)

```powershell
deploy\install-service.ps1 -ApiDir D:\congdoan\releases\<bản-cũ>\api -WebDir D:\congdoan\releases\<bản-cũ>\web `
  -DataDir D:\congdoan-data -ApiEnvFile D:\congdoan-data\config\api.env -WebEnvFile D:\congdoan-data\config\web.env
```

Sau đó chạy danh sách kiểm tra ở cuối tài liệu này.

## Cách 2: khôi phục database và media (schema không tương thích, hoặc dữ liệu hỏng)

```powershell
deploy\restore.ps1 -SqlInstance localhost -Database CongDoan `
  -BackupFile E:\congdoan-backup\CongDoan-<thời-điểm>.bak -MediaBackupDir E:\congdoan-backup\media-<thời-điểm> `
  -MediaDir D:\congdoan-data\media
deploy\install-service.ps1 -ApiDir D:\congdoan\releases\<bản-cũ>\api -WebDir D:\congdoan\releases\<bản-cũ>\web `
  -DataDir D:\congdoan-data -ApiEnvFile D:\congdoan-data\config\api.env -WebEnvFile D:\congdoan-data\config\web.env
```

`restore.ps1` hỏi xác nhận trước khi ghi đè. Trình tự của script:
1. Kiểm tra bản sao lưu đọc được (`RESTORE VERIFYONLY`). Nếu không đọc được thì dừng trước khi động vào dữ liệu.
2. Dừng API và web.
3. Lưu media hiện tại sang `<MediaDir>.before-restore-<thời-điểm>`.
4. Khôi phục database, chép lại media.
5. Khởi động dịch vụ.

Các bài viết và bài thi phát sinh sau thời điểm sao lưu sẽ mất. Ghi lại để nhập lại thủ công nếu cần.

## Thử khôi phục định kỳ (mỗi tháng, trên staging)

1. Chép bản `.bak` và thư mục `media-…` mới nhất sang máy staging.
2. Chạy `restore.ps1` trên staging.
3. Kiểm tra:
   - số bài viết, số thí sinh, số lượt thi khớp với production tại thời điểm sao lưu (xem *Bảng điều khiển*);
   - mở được ảnh và văn bản cũ;
   - đăng nhập được.
4. Ghi ngày thử, bản sao lưu đã dùng và kết quả vào sổ vận hành.

Lệnh T-SQL sao lưu và khôi phục đã được thử trên SQL Server 2022 (xem `docs/acceptance-matrix.md`). Script PowerShell **chưa kiểm chứng** trên Windows.

## Danh sách kiểm tra sau rollback hoặc khôi phục

- [ ] `/api/health` trả thành công. Trang chủ, một bài viết, trang `/thi` mở được.
- [ ] Đăng nhập admin. *Bảng điều khiển* hiển thị số liệu hợp lý.
- [ ] Mở một ảnh và một văn bản đã có. Tải lên một ảnh mới để thử quyền ghi media.
- [ ] Nếu có kỳ thi đang mở: kiểm tra *Kỳ thi* → *Báo cáo* xem lượt nào đang dở. Thông báo cho thí sinh liên quan theo quy chế.
- [ ] Ghi vào sổ vận hành: lý do, bản quay về, thời điểm sao lưu đã dùng, ai thực hiện.
