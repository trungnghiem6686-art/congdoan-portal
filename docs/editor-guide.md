# Hướng dẫn biên tập nội dung

Dành cho biên tập viên, người duyệt và quản trị nội dung.

Đăng nhập tại `/admin`. Mỗi người chỉ thấy các mục mình có quyền dùng. Nếu thiếu một mục cần dùng, hãy liên hệ quản trị hệ thống.

## Vai trò

| Vai trò | Làm được |
|---|---|
| Biên tập viên | Tạo và sửa bài **của mình**, tải tệp lên, gửi duyệt. Không xuất bản được. |
| Người duyệt | Sửa bài, duyệt hoặc trả lại, xuất bản, lên lịch. |
| Quản trị nội dung | Toàn bộ mục *Nội dung*: trang chủ, menu, banner, văn bản, thư viện, cấu hình website, chuyển hướng. |
| Quản trị hệ thống | Mọi quyền, kể cả tài khoản, vai trò và nhật ký. |

Quyền có thể giới hạn theo **đơn vị**: người được giao quyền cho một đơn vị chỉ thao tác được trên dữ liệu của đơn vị đó.

## Bài viết

1. Vào *Bài viết* → **Thêm mới**, nhập tiêu đề.
   - Đường dẫn được tạo từ tiêu đề, bỏ dấu. Muốn đặt riêng thì sửa ô đường dẫn.
   - Nếu trùng với bài khác, hệ thống sẽ gợi ý một đường dẫn khác.
2. Soạn nội dung trong khung soạn thảo.
   - **Ảnh:** dùng nút chèn ảnh để chọn từ máy, hoặc dán hay kéo thả ảnh vào khung. Ảnh được tải lên, nén lại và tạo ảnh thu nhỏ tự động.
   - **Video:** chèn tệp video đã tải lên, hoặc dán liên kết YouTube. Chỉ nhúng được video từ các trang trong danh sách cho phép.
   - **Tệp đính kèm** (PDF, Word…): thêm ở phần đính kèm. Tệp sẽ hiện cuối bài.
   - Nội dung dán từ nơi khác được làm sạch: mã script, sự kiện và liên kết `javascript:` bị loại bỏ.
3. Nhấn **Lưu bản nháp**.
   - Hệ thống tự lưu định kỳ khi đang soạn.
   - Nếu rời trang khi còn thay đổi chưa lưu, sẽ có cảnh báo.
4. Quy trình duyệt:
   - **Nháp** → **Gửi duyệt** → (người duyệt) **Duyệt** hoặc **Trả lại** kèm lý do → **Xuất bản**.
   - Muốn bài tự xuất bản vào một thời điểm, chọn *Lên lịch xuất bản* rồi nhấn **Lên lịch**. Bài sẽ xuất bản đúng giờ, kể cả khi máy chủ vừa khởi động lại.
5. Gỡ bài đã xuất bản:
   - **Gỡ xuống** đưa bài về nháp;
   - **Lưu trữ** cất bài đi mà vẫn giữ;
   - **Xóa** chuyển bài vào thùng rác và khôi phục được.
6. **Sửa cùng lúc:** nếu người khác đã lưu bài trong lúc bạn đang sửa, khi bạn lưu sẽ hiện thông báo xung đột. Chọn **Tải lại bản mới nhất**, rồi áp lại phần thay đổi của bạn. Hệ thống không âm thầm ghi đè.

Đổi đường dẫn của bài đã xuất bản: đường dẫn cũ tự chuyển hướng 301 sang đường dẫn mới, nên liên kết cũ vẫn dùng được.

## Trang chủ (dựng theo khối)

*Trang chủ* gồm các khối:
- Tin nổi bật, Danh sách tin, Lưới tin;
- Banner, Tiện ích nhanh, Liên kết;
- Văn bản, Thư viện ảnh/video;
- Thông báo kỳ thi, Văn bản giới thiệu.

1. Dùng **Thêm khối** để thêm một khối. Đổi thứ tự bằng cách kéo thả hoặc dùng nút lên/xuống.
2. Mỗi khối có:
   - tiêu đề;
   - kiểu trình bày;
   - nguồn dữ liệu, ví dụ chuyên mục hoặc số bài;
   - thời gian hiển thị (*Hiện từ* / *Ẩn sau*), không bắt buộc.
3. **Lưu bản nháp** chỉ lưu lại, chưa ai ngoài trang quản trị thấy.
4. **Xem trước bản nháp đã lưu** ở ba cỡ màn hình: máy tính, máy tính bảng, điện thoại.
5. **Xuất bản trang chủ**: trang công khai cập nhật trong vài giây, không cần nhờ kỹ thuật.
6. **Lịch sử xuất bản** → **Khôi phục** đưa một phiên bản cũ trở lại, dưới dạng một lần xuất bản mới.

Trang tĩnh (*Trang tĩnh*) cũng dựng theo khối như trên.

## Các mục nội dung khác

| Mục | Ghi chú |
|---|---|
| Chuyên mục | Cây chuyên mục, thứ tự, ẩn/hiện. Đường dẫn: `/chuyen-muc/<tên>`. |
| Menu | Menu chính và menu chân trang. Mục menu có thể trỏ tới chuyên mục, trang, bài viết hoặc liên kết ngoài. Đổi tên thì đường dẫn cũ tự chuyển hướng. |
| Chuyển hướng | Danh sách đường dẫn cũ → mới, có số lượt dùng. Thêm tay khi cần giữ một liên kết cũ. Xóa thì đường dẫn cũ trả về "không tìm thấy". |
| Banner | Ảnh, liên kết, vị trí và lịch hiển thị. Ngoài lịch thì banner tự ẩn. |
| Văn bản | Số hiệu, loại, cơ quan ban hành, ngày ban hành/hiệu lực, tệp đính kèm. Người xem lọc được theo số hiệu và ngày. |
| Thư viện ảnh/video | Album ảnh hoặc video (tệp tải lên hoặc YouTube). |
| Tệp đã tải lên | Mọi tệp đã tải lên. Tệp đang được dùng không xóa được. Tệp *hạn chế* chỉ người có quyền tải về được. |
| Liên hệ | Thư người xem gửi qua trang *Liên hệ*. Đánh dấu đã xử lý hoặc rác, kèm ghi chú. |
| Cấu hình website | Tên đơn vị, logo, thông tin liên hệ, chân trang. Hiện đang dùng dữ liệu **MẪU**; thay bằng thông tin chính thức trước khi mở trang. |

## Lưu ý

- Không đăng thông tin chưa được phép công bố. Bài nháp và bài chờ duyệt không xuất hiện ở trang công khai hay trong kết quả tìm kiếm.
- Ảnh tối đa 10 MB, văn bản tối đa 50 MB. Video lớn được tải lên theo từng phần, tối đa 1 GB.
- Mọi thao tác xuất bản, xóa, đổi quyền đều được ghi vào *Nhật ký*.
