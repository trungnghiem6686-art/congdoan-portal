# Bàn giao: tiếp tục xây dựng trên máy của bạn

Thư mục này chứa spec, kế hoạch 23 task và yêu cầu gốc. Chưa có code sản phẩm nào. Lý do: môi trường tạo ra thư mục này không tải được thư viện NuGet nên không thể build .NET; phần build và kiểm thử phải chạy trên máy có internet bình thường.

## Công nghệ đã chốt

Backend C# .NET 8 (Clean Architecture 4 lớp, EF Core 8, SQL Server, FluentValidation, HtmlSanitizer, Serilog, Swagger, xUnit), frontend Next.js 16 + React 19 + TypeScript + Tailwind, đăng nhập cookie HttpOnly, chạy trên IIS/Windows Server. Chi tiết trong spec mục 3.

## Cần cài trên máy

1. Git.
2. .NET 8 SDK.
3. Node.js 22 (LTS).
4. SQL Server (bản Developer hoặc Express) hoặc LocalDB để chạy test tích hợp.
5. Claude Code (cài theo hướng dẫn chính thức tại https://docs.claude.com).

## Các bước

1. Giải nén, mở terminal trong thư mục `congdoan-portal`, chạy `git status` (thư mục đã có lịch sử git, nhánh `feat/portal-build`).
2. Chạy `dotnet --version` (phải là 8.x) và `node --version` (22.x).
3. Chạy `claude` trong thư mục này và gửi câu sau:

> Hãy thực hiện kế hoạch `docs/superpowers/plans/2026-10-27-congdoan-portal.md` theo từng task, bắt đầu từ Task 1, dùng quy trình subagent-driven. Spec nằm ở `docs/superpowers/specs/2026-10-27-congdoan-portal-design.md`. Kết nối SQL Server của tôi là: (bạn tự điền, không dán mật khẩu production). Sau mỗi task hãy chạy test thật và báo đúng kết quả.

Nếu máy chưa có bộ kỹ năng "superpowers", vẫn dùng được: kế hoạch tự đủ để làm tuần tự từng task (viết test, thấy test thất bại, viết code, thấy test đạt, commit).

## Lưu ý

- Không dán mật khẩu production vào cuộc trò chuyện; dùng `.env` hoặc biến môi trường cục bộ.
- Không chạy thử trên website đang vận hành; triển khai chỉ làm ở môi trường staging riêng (Task 23).
- Con số 3000 thí sinh đồng thời mới là ước tính; chỉ có số đo thật sau khi chạy kịch bản tải ở Task 23.
- Yêu cầu bổ sung đã chốt: ảnh tải từ máy và video chèn tự do trong bài, slug menu tự đổi theo tên và không trùng, mọi nội dung thêm/sửa/xóa được, mở rộng bằng module (chatbot, hồ sơ đoàn viên, báo cáo chuyên đề).
