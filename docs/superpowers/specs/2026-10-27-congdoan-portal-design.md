# Thiết kế: Cổng thông tin, CMS và cổng thi trắc nghiệm Công đoàn Đường sắt VN

Ngày: 2026-10-27. Trạng thái: chờ duyệt. Nguồn yêu cầu: `prompt-website-cong-doan-cms-thi-trac-nghiem.md`.

## 1. Mục tiêu và phạm vi

Xây website thay thế https://congdoandsvn.org.vn/ gồm ba khu vực cùng tên miền:

| Khu vực | Đường dẫn | Đối tượng |
|---|---|---|
| Cổng thông tin | `/` | Công chúng |
| Quản trị | `/admin` | Tài khoản admin có vai trò |
| Cổng thi | `/thi` | Thí sinh có tài khoản và được giao kỳ thi |

Ràng buộc đã chốt với chủ dự án:
- Backend .NET Core, chạy trên Windows Server/IIS cũ, cơ sở dữ liệu **SQL Server**.
- Ước tính tối đa khoảng 3000 thí sinh làm bài đồng thời (con số ước tính, cần đo lại).
- Tài khoản thi và tài khoản admin tách riêng; admin có phân quyền riêng và menu quản lý tài khoản riêng.
- Hồ sơ đoàn viên là dữ liệu riêng, chỉ liên kết với thí sinh khi cần.
- Tin tức quản lý tập trung, không chia theo đơn vị.
- Admin sắp xếp khối trang chủ và dựng trang tĩnh mới từ các khối.
- Mọi menu, tin, media, banner, footer đều thêm/sửa/xóa-ẩn-khôi phục được trong CMS.
- Mở rộng về sau: chatbot, hồ sơ đoàn viên, báo cáo chuyên đề (không thuộc phạm vi đợt này, chỉ chừa chỗ gắn module).
- Dự án không liên quan VNR.

Ngoài phạm vi: tự luận, camera/sinh trắc học, chuyển hosting, thay nghiệp vụ nội bộ chưa khảo sát.

## 2. Giả định chưa xác minh

- Chưa có quyền truy cập website cũ, máy chủ, source hay database; cấu hình IIS, phiên bản Windows Server, SQL Server và chứng chỉ TLS chưa biết.
- Quy chế thi (số lượt, cách lấy điểm, công bố kết quả) dùng giá trị mặc định trong prompt và đều cấu hình được.
- Không có logo, thông tin liên hệ hay số liệu chính thức: dùng dữ liệu mẫu có nhãn "MẪU".
- Workspace phát triển hiện chưa có .NET SDK. Kế hoạch: thử cài SDK để build và chạy test thật; nếu không được, ghi rõ phần chưa build/kiểm thử.

## 3. Kiến trúc

Modular monolith + một frontend:

Backend (`backend/`, C# trên .NET 8, Clean Architecture 4 lớp, mỗi lớp chia thư mục theo module `Identity`, `Cms`, `Exam`, `Reporting`, `Shared`):
- `CongDoan.Domain`: thực thể và luật nghiệp vụ cốt lõi.
- `CongDoan.Application`: service, DTO, validation (FluentValidation), `IClock`, giao diện repository.
- `CongDoan.Infrastructure`: EF Core 8 (SQL Server, schema riêng cho từng module), lưu file, HtmlSanitizer, Serilog.
- `CongDoan.Api`: controller REST, middleware (request ID, lỗi thống nhất, giới hạn tần suất bằng AspNetCoreRateLimit, CSRF), Swagger (Swashbuckle).
- Module không truy cập chéo bảng của nhau; giao tiếp qua interface ở lớp Application.
- Kiểm thử: xUnit + coverlet. Test đơn vị có thể dùng SQLite; test tích hợp liên quan đồng thời, unique index lọc và transaction (bắt đầu thi, autosave, nộp bài) bắt buộc chạy trên SQL Server/LocalDB vì SQLite cư xử khác.

Frontend (`frontend/`, TypeScript): Next.js 16 (App Router), React 19, Tailwind CSS 3 (+ Typography), React Hook Form + Zod, lucide-react, Swiper, sharp, DOMPurify, ESLint 9; ba nhóm route `(public)`, `admin`, `thi` với layout riêng. Kiểm thử: Vitest và Playwright.

Đăng nhập dùng cookie HttpOnly, không lưu token trong localStorage (quyết định có chủ đích vì trình soạn thảo cho nội dung và nhúng video làm tăng bề mặt XSS).

Vận hành: Kestrel chạy như Windows Service; IIS làm reverse proxy (URL Rewrite/ARR) cho `/api` và Next.js; media lưu ngoài thư mục release; thư mục upload không cho thực thi. Dịch vụ nền (`BackgroundService`) cho xuất bản theo lịch và đóng lượt thi hết hạn, trạng thái nằm trong database nên restart không làm mất hay kéo dài giờ thi.

## 4. Mô hình dữ liệu (mức khái niệm)

Thời gian lưu UTC, hiển thị Asia/Ho_Chi_Minh. Bảng nghiệp vụ có khóa ngoại, unique, index, `RowVersion`; dữ liệu CMS dùng soft delete.

- **identity**: `AdminUser`, `Candidate` (tách bảng; cột tùy chọn `MemberId` để liên kết đoàn viên sau này), `Role`, `Permission`, `RolePermission`, `AdminUserRole`, `OrgUnit`, `CandidateGroup`, `Session`.
- **cms**: `Post`, `Category`, `Tag`, `PostTag`, `Page`, `PageSection`, `MediaAsset`, `Attachment`, `Menu`, `MenuItem`, `Banner`, `Document`, `Album`, `AlbumItem`, `HomepageRevision`, `HomepageSection`, `SiteSetting`, `ContactRequest`.
- **exam**: `Question`, `QuestionVersion`, `Choice`, `ExamTemplate`, `Exam`, `ExamVersion`, `ExamQuestion`, `ExamAssignment`, `Attempt`, `AttemptQuestion` (snapshot), `Answer`, `Result`, `GradeRevision`.
- **audit**: `AuditLog` dùng chung.

Bất biến quan trọng:
- Mỗi (Candidate, Exam) có tối đa một `Attempt` trạng thái đang làm (unique index lọc).
- Số lượt tạo ra không vượt hạn mức (kiểm tra trong cùng transaction với unique constraint).
- `AttemptQuestion` lưu nội dung câu, thứ tự câu, thứ tự lựa chọn, đáp án và quy tắc chấm tại thời điểm phát đề; sửa ngân hàng sau đó không ảnh hưởng bài đã thi.
- Không xóa dây chuyền kỳ thi/câu hỏi đã có kết quả; chỉ lưu trữ hoặc khóa.
- Cấu hình khối trang chủ là JSON được validate bằng schema; dữ liệu cần ràng buộc không lưu JSON.

## 5. Xác thực và phân quyền

- Hai scheme cookie tách biệt: `Admin` và `Candidate`; cookie HttpOnly, Secure (khi HTTPS), SameSite phù hợp, chống CSRF cho thao tác ghi.
- Mật khẩu băm bằng thuật toán chuẩn của ASP.NET Core Identity hasher; giới hạn thử đăng nhập; reset có thời hạn; buộc đổi mật khẩu lần đầu; khóa tài khoản thu hồi phiên; không cho khóa/xóa quản trị hệ thống cuối cùng.
- Tài khoản do admin tạo hoặc nhập danh sách; không tự đăng ký.
- RBAC theo permission (module + thao tác + phạm vi đơn vị khi cần). Vai trò mặc định: Quản trị hệ thống, Quản trị nội dung, Biên tập viên, Người duyệt, Quản lý kỳ thi, Người xem báo cáo. Mọi API kiểm tra quyền và quyền sở hữu/phạm vi ở backend để chống IDOR.

## 6. CMS và trình dựng trang chủ

- Quy trình bài viết: nháp → chờ duyệt → đã duyệt/lên lịch → xuất bản → lưu trữ; mọi bước ghi audit.
- Khối chuẩn: tin nổi bật, danh sách tin, lưới tin, banner, tiện ích nhanh, văn bản, thư viện, thông báo kỳ thi, văn bản giới thiệu, liên kết. Mỗi khối có ID ổn định, loại, nguồn dữ liệu, bộ lọc, số mục, kiểu trình bày được phép, thứ tự, trạng thái, lịch.
- Bố cục có bản nháp và bản đã xuất bản (`HomepageRevision`); xem trước desktop/tablet/mobile; xuất bản tạo phiên bản mới; khôi phục phiên bản cũ. Trang tĩnh mới dựng từ cùng bộ khối.
- Rich text được sanitize; không cho JavaScript tùy ý; embed theo danh sách nguồn cho phép; URL được kiểm tra an toàn; chống menu vòng lặp và trùng slug.
- Xuất bản làm mất hiệu lực cache theo thẻ (tag) ở frontend, không cần build lại.
- Bài viết: trình soạn thảo cho phép tải ảnh từ máy người dùng (chọn tệp, kéo thả, dán), chèn nhiều ảnh và video trong thân bài, kèm chú thích. Video có hai cách: tải tệp lên (mp4/webm, tải theo từng phần để chịu được tệp lớn, giới hạn cấu hình được) hoặc nhúng từ nguồn trong danh sách cho phép (ví dụ YouTube).
- Menu và đường dẫn: mỗi mục menu, chuyên mục và trang có đường dẫn (slug) tự sinh từ tên (bỏ dấu tiếng Việt, chữ thường, gạch nối). Đổi tên thì đường dẫn đổi theo; slug không được trùng, nếu trùng hệ thống báo lỗi và gợi ý slug khác. Slug cũ được giữ làm chuyển hướng 301 để liên kết cũ không hỏng. Admin có thể sửa tay slug.
- Mở rộng: hệ thống module có điểm đăng ký (menu admin, permission, route, migration theo schema riêng) để thêm Chatbot, Hồ sơ đoàn viên, Báo cáo chuyên đề mà không sửa lõi.
- Media: kiểm tra loại thật và kích thước, tên lưu an toàn, tạo thumbnail, hiển thị nơi đang dùng, không xóa tệp còn được tham chiếu; tệp công khai và tệp hạn chế phục vụ qua cơ chế khác nhau.
- Sửa đồng thời phát hiện bằng `RowVersion`; autosave nháp.

## 7. Lõi thi

Luồng: đăng nhập → danh sách kỳ thi được giao → bắt đầu → làm bài/autosave → nộp hoặc hết giờ → kết quả theo chính sách → lịch sử.

- **Bắt đầu:** trong một transaction kiểm tra được giao, đang mở, còn lượt, chưa có lượt đang làm; tạo `Attempt` và snapshot toàn bộ đề; hạn = min(bắt đầu + thời lượng, giờ đóng kỳ thi). Giờ do server quyết định.
- **Nguồn đề:** không bao giờ gửi đáp án đúng/lời giải xuống client khi đang thi (kể cả preload hay cache); dữ liệu thi cá nhân không cache dùng chung.
- **Autosave:** mỗi câu trả lời kèm số phiên bản tăng dần; request cũ hơn bị bỏ qua; bài đã kết thúc không nhận sửa. Mất mạng ngắn: tải lại tiếp tục cùng lượt với các đáp án đã được server xác nhận; thời gian không gia hạn.
- **Nộp bài:** idempotent theo `Attempt`; chốt trạng thái và chấm trong cùng transaction; nhấn lặp hay retry không tạo kết quả trùng.
- **Hết giờ:** API kiểm tra hạn ở mọi request; dịch vụ nền quét và đóng các lượt quá hạn, chấm bài kể cả khi trình duyệt đã đóng. Không dùng timestamp client để chấp nhận bài quá hạn; cửa sổ chấp nhận request tại hạn giờ được cấu hình rõ (mặc định 0 giây gia hạn).
- **Đa tab/thiết bị:** một lượt hoạt động; tab thứ hai thấy phiên bản mới hơn và chuyển sang chế độ chỉ đọc, không ghi đè chéo.
- **Chấm điểm:** từ snapshot ở backend; một đáp án, nhiều đáp án (đúng toàn bộ tập mới được điểm), đúng/sai; điểm đạt; làm tròn; điểm cuối theo cấu hình (cao nhất, gần nhất, đầu tiên). Chấm lại lưu `GradeRevision` (bản gốc, lý do, người thực hiện).
- **Kết quả:** công bố ngay / sau khi kỳ thi đóng / sau khi duyệt; điểm, chi tiết đúng-sai và lời giải phân quyền riêng; bảng xếp hạng mặc định tắt.
- **Ngân hàng câu hỏi:** CRUD theo chủ đề/mức độ/đơn vị/trạng thái duyệt; import CSV/XLSX có xem trước, báo lỗi theo dòng và xác nhận trước khi ghi (import nguyên khối, không nhập dở); phiên bản hóa câu hỏi.
- **Đề và kỳ thi:** chọn tay hoặc rút ngẫu nhiên theo ma trận; kiểm tra đủ câu trước khi công bố; giao theo cá nhân/nhóm/đơn vị; đảo câu/lựa chọn có cấu hình; vòng đời nháp → kiểm tra → công bố → mở → đóng → lưu trữ.
- **Báo cáo:** theo kỳ thi, đơn vị, nhóm; phân biệt số người và số lượt, nêu rõ mẫu số; xuất CSV/XLSX theo quyền, chống formula injection, ghi nhật ký xuất.
- **Chống gian lận:** chỉ ghi log nghiệp vụ; sự kiện chuyển tab (nếu bật) chỉ là tín hiệu, không tự kết luận.

## 8. API và quy ước

REST JSON; phân trang, lọc; lỗi thống nhất (mã, thông điệp, request ID); envelope `{ success, data, error }` bóc tách tập trung ở một client duy nhất của frontend; validation ở server; tài liệu OpenAPI. Tìm kiếm công khai chỉ trả nội dung đã xuất bản, hỗ trợ tiếng Việt có/không dấu.

## 9. Giao diện

- **Cổng tin:** Be Vietnam Pro, navy `#12365B`, xanh `#176BB0`, đỏ nhấn `#C62828`, nền `#F4F6F8`; khung 1200–1320px; trang chủ kiểu báo điện tử tĩnh (thanh tiện ích, header, menu, tin chính + tin phụ, các khối chuyên mục, thông báo/văn bản, kỳ thi, thư viện, footer); không carousel tự chạy; hướng tới WCAG 2.2 AA; có trang 404/lỗi/bảo trì. Màu và nhận diện điều chỉnh theo bộ nhận diện chính thức khi có.
- **Admin:** tiếng Việt, sidebar theo nghiệp vụ, tìm nhanh, bộ lọc, phân trang, thùng rác, thao tác hàng loạt theo quyền, dashboard số liệu thật từ database.
- **Cổng thi:** đồng bộ nhận diện nhưng tập trung nhiệm vụ; đồng hồ do server quyết định; lưới số câu, đánh dấu xem lại, trạng thái đang lưu/đã lưu/lỗi; mobile không để đồng hồ hay nút cố định che câu hỏi.

## 10. Bảo mật và vận hành

HTTPS bắt buộc khi chạy thật (cấu hình chứng chỉ trên chính máy chủ cũ); secret ngoài source (`.env.example`/biến môi trường); chống XSS, injection, mass assignment, IDOR; không ghi mật khẩu/đáp án/token vào log; log xoay vòng; health check; sao lưu database và media, có kiểm tra phục hồi; deploy không ghi đè uploads/secret/log; staging riêng; không phát hành trong giờ thi; rollback tương thích schema.

## 11. Chuyển dữ liệu

Script import chạy lại được (khóa nguồn chống trùng), log lỗi, báo cáo đối soát; ánh xạ URL cũ → mới với redirect 301; không giả định hash mật khẩu cũ tương thích. Chỉ thực hiện được sau khi có dữ liệu xuất hoặc quyền khảo sát trang cũ.

## 12. Kiểm thử và nghiệm thu

Ma trận yêu cầu → module → kiểm thử → kết quả theo mục 11 của prompt gốc. Ưu tiên integration test cho: giới hạn lượt thi khi hai request đồng thời, autosave ngược thứ tự, nộp lặp, hết giờ không cần trình duyệt, không lộ đáp án trong payload, phân quyền và IDOR, snapshot bất biến, chấm điểm các ca biên. Kiểm tra giao diện ở 360/390/768/1024/1440px. Kiểm thử tải thi chỉ báo số đo thực tế kèm cấu hình; không cam kết năng lực chưa đo.

## 13. Các mốc thực hiện

1. Nền tảng: solution, database, Identity, RBAC, audit, media.
2. CMS và cổng thông tin công khai, trình dựng khối.
3. Cổng thi và module Exam, báo cáo.
4. Import thử, kiểm thử tích hợp, đo tải, sửa lỗi.
5. Đóng gói release, tài liệu vận hành IIS, backup/restore, rollback.

## 14. Đầu vào còn thiếu (không chặn việc bắt đầu)

- Bản sao source/database cũ nếu có; thông tin IIS/Windows Server/SQL Server hiện tại.
- Logo, thông tin liên hệ và nội dung chính thức.
- Quy chế thi thực tế (số lượt, cách lấy điểm, công bố kết quả).
- Số người thi đồng thời đã xác nhận để đo tải.
