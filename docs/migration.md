# Chuyển dữ liệu từ trang cũ

Công cụ `Importer` (mã nguồn: `backend/tools/CongDoan.Importer`) nhập chuyên mục, bài viết, ảnh từ bản xuất của trang cũ và tạo redirect 301 từ URL cũ sang URL mới.

> **Trạng thái:** chưa có bản xuất dữ liệu thật hay quyền khảo sát trang cũ. Công cụ mới được chạy trên dữ liệu **MẪU** (`backend/tools/CongDoan.Importer/sample`). Khi có dữ liệu thật, cần viết bước chuyển dữ liệu cũ sang định dạng dưới đây, rồi chạy thử trên môi trường staging trước. Không chạy trên trang đang hoạt động.

## Định dạng bản xuất

Một thư mục chứa `manifest.json` và các tệp ảnh. Đường dẫn ảnh tính từ thư mục này.

```json
{
  "categories": [
    { "key": "cat-hoat-dong", "name": "Hoạt động", "oldUrl": "/chuyen-muc-cu/hoat-dong.html" }
  ],
  "posts": [
    {
      "key": "101",
      "title": "Tiêu đề",
      "summary": "Tóm tắt (có thể null)",
      "bodyHtml": "<p>Nội dung</p><img src=\"images/a.jpg\">",
      "categoryKey": "cat-hoat-dong",
      "coverImage": "images/a.jpg",
      "publishedUtc": "2024-01-15T02:00:00Z",
      "oldUrl": "/tin-cu/101-tieu-de.html"
    }
  ]
}
```

| Trường | Ghi chú |
|---|---|
| `key` | Khóa duy nhất trong trang cũ (ví dụ ID bài). Ghép với `--batch` thành `SourceKey`, nên chạy lại không tạo bản trùng. |
| `bodyHtml` | Được làm sạch như nội dung soạn trong trang quản trị (bỏ script, thuộc tính sự kiện, URL không an toàn). |
| `img src`, `coverImage` | Phải là tệp trong thư mục xuất. Ảnh qua quy trình tải lên chuẩn (kiểm tra định dạng, nén, tạo ảnh thu nhỏ). Ảnh từ URL ngoài, ảnh thiếu hoặc hỏng sẽ bị **bỏ qua và ghi vào báo cáo**; bài vẫn được nhập. |
| `publishedUtc` | Ngày đăng gốc, theo giờ UTC. Thiếu thì lấy thời điểm nhập. |
| `oldUrl` | Đường dẫn cũ (có thể là URL đầy đủ). Tạo redirect 301 sang `/tin-tuc/{slug}` hoặc `/chuyen-muc/{slug}`. |

Chuyên mục được ghép theo đường dẫn tạo từ tên. Nếu đã có chuyên mục trùng thì dùng lại, không tạo mới.

## Chạy

Cấu hình kết nối lấy từ biến môi trường, giống API. **Không ghi mật khẩu vào lệnh hay mã nguồn.**

```powershell
$env:ConnectionStrings__Default = "<chuỗi kết nối staging>"
$env:Media__RootPath = "D:\CongDoan\media"          # cùng thư mục media mà API dùng
$env:Frontend__RevalidateUrl = "https://staging.example/next-api/revalidate"
$env:Frontend__RevalidateSecret = "<bí mật>"

# 1. Chạy thử: chỉ đọc, đếm và kiểm tra tệp ảnh, không ghi gì
dotnet Importer.dll run --source D:\xuat-du-lieu --dry-run --report chay-thu.csv

# 2. Nhập thật
dotnet Importer.dll run --source D:\xuat-du-lieu --batch legacy --report lan-1.csv

# 3. Chạy lại (sau khi sửa dữ liệu nguồn): bài đã có được bỏ qua
dotnet Importer.dll run --source D:\xuat-du-lieu --batch legacy --report lan-2.csv
```

Mã thoát của lệnh:
- `0`: không có bài lỗi.
- `2`: có bài không nhập được (xem báo cáo).
- `1`: lỗi tham số hoặc thiếu `manifest.json`.

## Báo cáo đối soát

Mỗi lần chạy ghi một tệp CSV (UTF-8 có BOM, mở được bằng Excel).

Phần đầu là các số tổng: `source_posts`, `source_categories`, `created`, `skipped` (đã có từ lần trước), `failed`, `images`, `redirects`. Luôn có `source_posts = created + skipped + failed`.

Phần sau liệt kê từng vấn đề theo dòng `source_key,kind,message`, với `kind` là một trong:
- `post`: bài không nhập được, ví dụ thiếu tiêu đề.
- `image`: ảnh thiếu, hỏng hoặc nằm ngoài thư mục xuất.
- `category`: không thấy chuyên mục.
- `redirect`: không tạo được redirect.

Cách đối soát sau mỗi lần nhập thật:
1. Số bài nguồn phải khớp với số bài đếm được trên trang cũ.
2. Số bài trong trang quản trị tăng đúng bằng `created`.
3. Xử lý hết các dòng `post` và `image`, sửa dữ liệu nguồn rồi chạy lại.

## Redirect

- URL cũ được chuẩn hóa: bỏ `?…`/`#…`, chuyển về chữ thường, bỏ `/` ở cuối. Trang mới trả **301** (Next.js trả 308, cũng là chuyển hướng vĩnh viễn) sang đường dẫn mới.
- URL cũ không có trong bảng redirect trả **404**. Không có chuyển hướng nào về trang chủ.
- **URL có tham số truy vấn** (ví dụ `/news.php?id=12`) không lưu được trong bảng redirect, vì phần truy vấn bị bỏ khi so khớp. Báo cáo ghi các URL này với `kind=redirect`. Nếu trang cũ dùng kiểu URL này, cần thêm quy tắc rewrite trên IIS, xem `deploy/web.config`.
- Đổi đường dẫn bài sau khi nhập: redirect cũ tự trỏ sang đường dẫn mới, không có chuỗi chuyển hướng.
- Quản lý redirect trong trang quản trị, mục **Chuyển hướng**.

## Tài khoản cũ

Không nhập mật khẩu từ hệ thống cũ, vì không giả định định dạng mã hóa mật khẩu tương thích. Tài khoản quản trị và thí sinh được tạo mới hoặc nhập bằng chức năng nhập thí sinh, kèm mật khẩu tạm buộc đổi khi đăng nhập lần đầu.

## Hoàn tác

Mỗi lần nhập thật cần sao lưu cơ sở dữ liệu và thư mục media trước (xem `docs/rollback.md`). Để gỡ một đợt nhập trên staging, khôi phục bản sao lưu đó. Không xóa trực tiếp bằng SQL.
