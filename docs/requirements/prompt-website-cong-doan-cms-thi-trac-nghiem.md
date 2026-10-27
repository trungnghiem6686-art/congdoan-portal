# PROMPT XÂY DỰNG CỔNG THÔNG TIN CÔNG ĐOÀN, QUẢN TRỊ VÀ THI TRẮC NGHIỆM

> Tài liệu yêu cầu dành cho AI lập trình hoặc đội phát triển. Đây là prompt để triển khai trong bước tiếp theo, không phải xác nhận hệ thống đã được xây dựng. Sao chép toàn bộ phần “PROMPT THỰC THI” bên dưới để sử dụng.

## PROMPT THỰC THI

Bạn là chuyên gia kiến trúc phần mềm, thiết kế sản phẩm UI/UX, phát triển full-stack, quản trị Windows Server/IIS và kiểm thử hệ thống. Hãy chịu trách nhiệm xây dựng trọn vẹn website thay thế https://congdoandsvn.org.vn/ theo các yêu cầu dưới đây. Làm việc như một kỹ sư trưởng: phân tích đúng nghiệp vụ, thiết kế đẹp, triển khai chức năng thực tế, kiểm thử và bàn giao mã nguồn có thể bảo trì.

Không chỉ đưa ra tư vấn hoặc dựng một trang chủ mẫu rồi coi là hoàn thành. Sản phẩm cuối phải bao gồm giao diện công khai, quản trị, cổng thi, backend, cơ sở dữ liệu, phân quyền, chuyển dữ liệu và hướng dẫn triển khai. Nếu thiếu quyền truy cập hoặc dữ liệu, hoàn thành phần độc lập trước, ghi rõ phần bị chặn và đầu vào còn thiếu; không giả vờ đã kiểm thử hay đã triển khai.

### 1. Bối cảnh và yêu cầu không được thay đổi

- Đơn vị: Công đoàn Đường sắt Việt Nam.
- Website tham chiếu: https://congdoandsvn.org.vn/.
- Xây dựng ba khu vực riêng biệt về giao diện, điều hướng và quyền truy cập, dùng dữ liệu và định danh thống nhất khi phù hợp.
- Khu vực 1: cổng tin tức công khai, ưu tiên vẻ đẹp, sự trang trọng, dễ đọc và dễ tra cứu.
- Khu vực 2: admin quản lý toàn bộ nội dung, cấu hình hiển thị, tài khoản và nghiệp vụ thi theo quyền.
- Khu vực 3: cổng thi trắc nghiệm, người dùng phải có tài khoản hợp lệ và được cấp quyền đối với kỳ thi mới được dự thi.
- Tiếp tục vận hành trên môi trường máy chủ cũ; không tự chuyển sang hosting khác hoặc buộc dùng dịch vụ đám mây mới.
- Mọi nội dung biên tập và cấu hình hiển thị trên trang chủ phải quản lý được bằng CMS. Không để quản trị viên phải sửa mã nguồn để thay banner, menu, tin nổi bật, footer hoặc thứ tự các khối nội dung.
- Chưa được coi dữ liệu mẫu là dữ liệu chính thức. Không tự bịa logo, thông tin lãnh đạo, địa chỉ, số điện thoại, số liệu hoặc lịch sử tổ chức.
- Không tác động website đang chạy trong quá trình xây dựng. Triển khai thử riêng; chuyển production khi chủ dự án yêu cầu.

### 2. Khảo sát trước khi triển khai

#### 2.1. Khảo sát website và dữ liệu

Khảo sát các trang công khai, lập bảng gồm: URL/nhóm trang, nội dung hiện có, chức năng quan sát được, phần đề xuất nâng cấp, dữ liệu cần chuyển và điểm chưa xác minh.

Các nhóm đã được nhận diện sơ bộ gồm giới thiệu, hệ thống tổ chức, truyền thống, tin tức, văn bản, tài liệu, hình ảnh, video, nội bộ và liên hệ. Khảo sát lại hiện trạng trước khi triển khai. Các liên kết sang tên miền khác phải được lập danh sách riêng; không mặc định cùng backend hoặc cùng cơ sở dữ liệu.

Phân biệt rõ điều đã kiểm chứng và giả định. Không suy ra công nghệ, số lượng dữ liệu hoặc nghiệp vụ nội bộ chỉ từ HTML công khai. Ưu tiên dữ liệu xuất và bản sao cơ sở dữ liệu khi có; nội dung công khai không thay thế được backup đầy đủ.

#### 2.2. Khảo sát môi trường cũ

Lấy việc tương thích Windows Server/IIS làm hướng khảo sát ban đầu, nhưng xác minh cấu hình thực tế trước khi chốt kiến trúc. Cần xác định:

- Phiên bản hệ điều hành, IIS, runtime backend/frontend và cơ sở dữ liệu.
- Cấu trúc source, file lock dependency, quy trình build/publish và các bản backup.
- Website binding, domain, chứng chỉ TLS, reverse proxy/rewrite, các cổng dịch vụ và đường dẫn API.
- Application Pool hoặc dịch vụ chạy backend, tài khoản vận hành và quyền thư mục.
- Thư mục media/upload, giới hạn upload, dung lượng, cơ chế backup và vị trí lưu log.
- Tài nguyên CPU/RAM, quy mô dữ liệu, số người dự kiến thi đồng thời và khả năng cài dependency.

Không khẳng định một file build hoặc publish có thể khôi phục đầy đủ source code. Nếu chỉ có bản deploy, báo cáo rõ phần có thể tận dụng và phần cần tái xây dựng.

Đề xuất một kiến trúc chính dựa trên kết quả khảo sát. Nếu hiện trạng có Next.js, backend .NET và cơ sở dữ liệu phù hợp, ưu tiên đánh giá khả năng tiếp tục sử dụng; đây không phải khẳng định về công nghệ của website tham chiếu. Không đổi framework hoặc database chỉ vì sở thích.

### 3. Kiến trúc ba khu vực

Ưu tiên phương án cùng tên miền để dễ vận hành trên IIS, trừ khi hệ thống cũ có ràng buộc khác:

| Khu vực | Đường dẫn đề xuất | Phạm vi |
|---|---|---|
| Cổng thông tin | `/` | Trang chủ, bài viết, chuyên mục, văn bản, thư viện, liên hệ |
| Quản trị | `/admin` | Nội dung, bố cục, tài khoản, quyền, kỳ thi, báo cáo, cấu hình |
| Cổng thi | `/thi` | Đăng nhập, kỳ thi được phép, làm bài, kết quả, lịch sử |

Ba khu vực có layout độc lập. Tách layout không bắt buộc thành ba codebase hoặc ba máy chủ. Tổ chức module để bảo trì dễ, tránh nhân bản nghiệp vụ. Nếu framework dùng route động, bảo đảm refresh trực tiếp mọi URL hoạt động qua IIS.

Tài khoản có thể mang nhiều vai trò; thí sinh không mặc nhiên có quyền admin. Backend xác thực mọi thao tác và phạm vi dữ liệu, không dựa vào việc ẩn nút hoặc kiểm tra ở frontend.

### 4. Thiết kế UI/UX cổng thông tin

#### 4.1. Phong cách

Thiết kế một cổng thông tin có chất lượng thị giác cao, mang tinh thần ngành đường sắt và công đoàn Việt Nam: đáng tin cậy, hiện đại, trang trọng, gần gũi người lao động. Có cá tính riêng, không dùng nguyên mẫu dashboard hoặc landing page quảng cáo cho trang tin.

Định hướng màu đề xuất: xanh navy #12365B, xanh #176BB0, đỏ nhấn #C62828, nền trắng và xám nhạt #F4F6F8. Điều chỉnh theo bộ nhận diện được xác minh. Không dùng quá nhiều màu nổi.

- Font tiếng Việt tốt: Be Vietnam Pro hoặc Noto Sans; tối ưu tải font.
- Nội dung chính từ 16px, khoảng cách dòng thoải mái; bài dài có độ rộng cột đọc hợp lý.
- Khung desktop khoảng 1200–1320px, lưới nhất quán và khoảng trắng rõ.
- Tiêu đề phân cấp tốt, hạn chế viết hoa toàn bộ; hình ảnh lớn vừa đủ, không làm chìm nội dung.
- Bo góc vừa phải, đường viền tinh tế, bóng nhẹ, chuyển động ngắn và tôn trọng reduced motion.
- Dùng ảnh hoạt động thật khi có quyền sử dụng; logo chính thức giữ đúng tỷ lệ.
- Không lạm dụng hiệu ứng kính, gradient, marquee, popup, carousel tự chạy hoặc banner quá cao.
- Thiết kế đầy đủ hover, focus, active, disabled, loading, empty, error và success.
- Hướng tới WCAG 2.2 AA, điều hướng bàn phím, tương phản rõ, nhãn form, alt ảnh và vùng bấm thuận tiện.

#### 4.2. Trang chủ

1. Thanh tiện ích gọn: liên hệ, tìm kiếm, nút “Thi trực tuyến”; ngày giờ nếu có phải đúng múi giờ Việt Nam.
2. Header thương hiệu: logo, tên tổ chức rõ ràng, hình ảnh nhận diện vừa đủ.
3. Menu chính: Trang chủ, Giới thiệu, Tin tức, Truyền thống, Văn bản và tài liệu, Thư viện, Liên hệ; menu con có cấu trúc dễ dùng.
4. Vùng tin nổi bật: một tin chính lớn và các tin phụ; ảnh, tiêu đề, tóm tắt, ngày đăng cân đối. Ưu tiên bố cục báo điện tử tĩnh.
5. Lối tắt tiện ích: tra cứu văn bản, sổ tay, hệ thống tổ chức, thi trực tuyến.
6. Các khối chuyên mục: tin công đoàn, hoạt động đơn vị trực thuộc, tin ngành và chuyên đề phù hợp nguồn dữ liệu.
7. Thông báo và văn bản mới có metadata rõ, thao tác xem/tải dễ thấy.
8. Khối giới thiệu kỳ thi công khai: chỉ nội dung được duyệt, lịch và nút truy cập; không công khai đề hoặc kết quả cá nhân.
9. Thư viện ảnh/video; không tự phát âm thanh.
10. Liên kết hữu ích và footer đầy đủ thông tin đã xác minh.

Không nhồi mọi mục vào màn hình đầu. Mỗi khối có mục đích, phân cấp và hành động rõ. Giảm lặp tin giữa các khối, cho phép biên tập chọn cách hiển thị.

#### 4.3. Responsive và các trang con

- Mobile ưu tiên nội dung, header gọn, tìm kiếm dễ chạm, menu không che thao tác chính.
- Trang chuyên mục có breadcrumb, bộ lọc, phân trang và bố cục tin nhất quán.
- Trang bài viết có tác giả/nguồn nếu có, ngày đăng, ảnh/chú thích, tệp đính kèm, chia sẻ liên kết, bản in và bài liên quan.
- Trang văn bản tìm theo từ khóa, số ký hiệu, loại, cơ quan và ngày ban hành; xem PDF khi hỗ trợ, có tải thay thế. Không tự suy đoán hiệu lực pháp lý.
- Trang tổ chức có sơ đồ responsive; trang truyền thống có timeline khi dữ liệu được xác thực.
- Thư viện có album, xem ảnh, video, mô tả và hỗ trợ bàn phím.
- Tìm kiếm nội dung công khai hỗ trợ tiếng Việt có/không dấu, bộ lọc, kết quả rỗng và phân trang.
- Liên hệ có validation, chống spam, lưu yêu cầu và chỉ báo thành công sau khi backend đã tiếp nhận.
- Có trang 404, lỗi hệ thống và bảo trì phù hợp; không lộ stack trace.

### 5. CMS quản lý toàn bộ nội dung trang chủ

Hiểu “mọi thứ trên trang chủ đều CRUD được” là toàn bộ nội dung biên tập, liên kết và cấu hình trình bày đều có chức năng thêm, xem, sửa, xóa/ẩn/khôi phục theo quyền. Thành phần tự sinh như ngày hiện tại, phân trang và trạng thái đăng nhập hoạt động theo logic; không biến mã nguồn thực thi hoặc quy tắc bảo mật thành nội dung tùy ý chỉnh sửa.

| Nhóm | Phạm vi quản lý bắt buộc |
|---|---|
| Nhận diện | Logo, favicon, tên hiển thị, mô tả, ảnh thương hiệu, alt ảnh |
| Header/tiện ích | Thông tin liên hệ, liên kết, nhãn nút, trạng thái và thứ tự |
| Menu | Thêm/sửa/xóa mục, menu con, URL nội/ngoại, thứ tự, ẩn/hiện |
| Banner | Ảnh desktop/mobile, tiêu đề, liên kết, vị trí, lịch hiển thị |
| Tin tức | Bài, chuyên mục, thẻ, ảnh đại diện, tác giả, nguồn, SEO, lịch đăng |
| Tin nổi bật | Ghim/bỏ ghim, thứ tự, thời gian áp dụng, nguồn chọn tay/tự động |
| Khối trang chủ | Thêm, nhân bản, sửa, sắp xếp, ẩn, xóa, khôi phục, xem trước |
| Tiện ích nhanh | Tên, icon từ bộ được phép, mô tả, URL, thứ tự |
| Thông báo/văn bản | Nội dung, metadata, tệp, thứ tự và lịch công bố |
| Kỳ thi giới thiệu | Nội dung quảng bá được duyệt, ảnh, lịch, liên kết cổng thi |
| Thư viện | Album, ảnh, chú thích, video, thumbnail, nguồn nhúng cho phép |
| Tổ chức/trang tĩnh | Giới thiệu, bộ phận, đơn vị, sơ đồ, nội dung truyền thống |
| Footer | Các cột, thông tin đơn vị, liên kết, bản quyền, mạng xã hội |
| SEO/cấu hình | Metadata mặc định, chia sẻ mạng xã hội, thông báo hệ thống |

#### Trình dựng trang chủ theo khối

- Cung cấp bộ khối chuẩn: tin nổi bật, danh sách tin, lưới tin, banner, tiện ích, văn bản, thư viện, thông báo kỳ thi, văn bản giới thiệu và liên kết.
- Mỗi khối có ID ổn định, loại, tiêu đề, mô tả, nguồn dữ liệu, bộ lọc, số mục, kiểu trình bày được phép, thứ tự, trạng thái và lịch xuất bản.
- Kéo thả sắp xếp, đồng thời có nút lên/xuống để hỗ trợ bàn phím.
- Preview desktop/tablet/mobile trước khi xuất bản; thay đổi nháp không tự xuất hiện công khai.
- Xuất bản đồng bộ một phiên bản bố cục; có lịch sử và khôi phục phiên bản trước.
- Có quy tắc tránh cấu hình sai, khối rỗng, trùng slug, menu vòng lặp, URL không an toàn hoặc phá bố cục.
- Không cho chèn JavaScript tùy ý. Nội dung rich text được làm sạch; embed dùng danh sách nguồn cho phép.
- Thay đổi đã xuất bản cập nhật frontend bằng cơ chế cache invalidation rõ ràng, không cần build/deploy lại cho thao tác nội dung thông thường.

#### Trải nghiệm biên tập

Giao diện admin bằng tiếng Việt, sidebar theo nghiệp vụ, tìm nhanh, breadcrumb, bộ lọc và phân trang. Có autosave nháp, cảnh báo rời trang khi chưa lưu, xem trước, nhân bản bài, thao tác hàng loạt theo quyền, thùng rác và khôi phục. Trình soạn thảo hỗ trợ heading, bảng, liên kết, ảnh, chú thích và tài liệu đính kèm. Kho media tìm kiếm được, hiển thị nơi đang sử dụng; không xóa vật lý tệp còn được tham chiếu mà không xử lý phụ thuộc. Phát hiện chỉnh sửa đồng thời bằng version hoặc optimistic concurrency, không âm thầm ghi đè.

Quy trình mặc định: nháp → chờ duyệt → đã duyệt/lên lịch → xuất bản → lưu trữ. Người có quyền cao có thể thực hiện nhiều bước nhưng mọi thao tác cần lưu dấu vết.

### 6. Admin và phân quyền toàn hệ thống

Thiết kế RBAC với quyền theo module, thao tác và phạm vi đơn vị khi cần:

| Vai trò | Quyền mặc định |
|---|---|
| Quản trị hệ thống | Cấu hình, tài khoản, phân quyền, tất cả module theo chính sách |
| Quản trị nội dung | CMS, bố cục, media, menu, trang tĩnh |
| Biên tập viên | Tạo/sửa nháp trong phạm vi được cấp, gửi duyệt |
| Người duyệt | Duyệt, xuất bản, gỡ bài theo quyền |
| Quản lý kỳ thi | Ngân hàng câu hỏi, đề, lịch thi, đối tượng dự thi, báo cáo được cấp |
| Người xem báo cáo | Đọc/xuất báo cáo đúng phạm vi, không sửa điểm |
| Thí sinh | Hồ sơ của mình, kỳ thi được giao, bài làm và kết quả được phép xem |

Admin có dashboard số liệu thực lấy từ database, quản lý phản hồi liên hệ, cấu hình hệ thống và nhật ký hoạt động. Không dùng số thống kê giả. Mỗi API và truy vấn tài nguyên phải kiểm tra quyền sở hữu/phạm vi để chống truy cập dữ liệu người khác bằng cách đổi ID.

Tài khoản do admin tạo hoặc nhập danh sách là mặc định. Tự đăng ký chỉ bật nếu chủ dự án yêu cầu, có quy trình duyệt phù hợp. Hỗ trợ khóa/mở, đổi mật khẩu, reset có thời hạn và buộc đổi mật khẩu ban đầu. Khóa tài khoản phải vô hiệu phiên theo chính sách. Không gửi hoặc lưu mật khẩu rõ. Không để khóa/xóa quản trị viên cuối cùng làm mất quyền vận hành.

### 7. Cổng thi trắc nghiệm dành cho người có tài khoản

#### 7.1. Hành trình thí sinh

Đăng nhập → xem kỳ thi được phép → đọc thể lệ/lịch/số lần thi → bắt đầu → làm bài và lưu tự động → nộp hoặc hết giờ → xem kết quả theo chính sách → lịch sử bài thi.

Trang cổng thi có nhận diện đồng bộ với trang tin nhưng tập trung vào nhiệm vụ. Có danh sách sắp mở/đang mở/đã kết thúc/đã tham gia; trang chi tiết hiển thị thời lượng, số câu, số lần được thi, thời gian đóng và quy tắc tính kết quả. Người đã đăng nhập nhưng chưa được giao kỳ thi không được mở đề bằng URL trực tiếp.

#### 7.2. Ngân hàng câu hỏi

- CRUD câu hỏi theo chủ đề, mức độ, đơn vị quản lý và trạng thái duyệt.
- Hỗ trợ một đáp án đúng, nhiều đáp án đúng và đúng/sai; không mở rộng tự luận nếu chưa có yêu cầu.
- Câu hỏi có nội dung, ảnh nếu cần, các lựa chọn, đáp án, lời giải, điểm và phiên bản.
- Mặc định nhiều đáp án chấm đúng toàn bộ tập lựa chọn mới nhận điểm; chỉ dùng điểm từng phần khi có quy tắc rõ, được cấu hình và kiểm thử.
- Validate câu hỏi thiếu đáp án, trùng lựa chọn, điểm không hợp lệ và câu hỏi chưa duyệt.
- Import CSV/XLSX theo mẫu, xem trước, báo lỗi theo dòng và xác nhận trước khi ghi; không nhập dở dang mà không báo cáo.
- Export ngân hàng chỉ dành cho quyền phù hợp; tránh lộ đáp án trong luồng dành cho thí sinh.

#### 7.3. Đề thi và kỳ thi

Phân biệt ngân hàng câu hỏi, mẫu đề, phiên bản đề, kỳ thi và lượt làm bài.

- Tạo đề chọn tay hoặc rút ngẫu nhiên theo ma trận chủ đề/mức độ/số câu.
- Kiểm tra ngân hàng đủ câu trước khi công bố.
- Cấu hình số câu, tổng điểm, điểm đạt, thời lượng, lịch mở/đóng, số lượt và cách lấy điểm cuối: cao nhất, gần nhất hoặc đầu tiên.
- Giao kỳ thi theo cá nhân, nhóm hoặc đơn vị; có danh sách đối tượng dự thi rõ ràng.
- Đảo thứ tự câu/lựa chọn có cấu hình; không đảo các câu phụ thuộc thứ tự nếu chưa xử lý nội dung tương ứng.
- Quy định mặc định: hạn lượt làm bài là thời điểm sớm hơn giữa thời điểm bắt đầu cộng thời lượng và thời điểm đóng kỳ thi. Chính sách khác phải được công bố rõ.
- Lưu thời gian UTC, hiển thị theo Asia/Ho_Chi_Minh.
- Quy trình nháp → kiểm tra/xem trước → công bố → mở thi → đóng → lưu trữ.
- Snapshot phiên bản đề, câu hỏi, lựa chọn, đáp án và quy tắc chấm khi phát đề; chỉnh ngân hàng về sau không được làm thay đổi bài đã thi.
- Không xóa dây chuyền câu hỏi hoặc kỳ thi đã có kết quả. Dùng lưu trữ/khóa và giữ hồ sơ phục vụ đối soát.

#### 7.4. Giao diện làm bài

- Nội dung câu hỏi dễ đọc, vùng chọn đáp án rộng, ảnh không tràn.
- Đồng hồ, tiến độ, danh sách số câu, trạng thái chưa làm/đã làm/đánh dấu xem lại.
- Chuyển câu, quay lại, đánh dấu và xem tổng quan trước khi nộp.
- Nộp bài có xác nhận và cảnh báo số câu còn trống.
- Lưu từng thay đổi với debounce hợp lý; hiển thị rõ đang lưu/đã lưu/lỗi lưu.
- Tải lại hoặc mất mạng ngắn có thể tiếp tục cùng lượt, giữ câu trả lời đã được server xác nhận; thời gian không tự gia hạn.
- Nếu có hàng đợi cục bộ, đánh dấu chưa đồng bộ và không hứa bảo toàn phần chưa tới server; giới hạn lưu trên thiết bị dùng chung, xóa đúng lúc và không lưu đáp án chuẩn.
- Khi hết giờ, giao diện khóa thao tác; backend vẫn phải đóng lượt và chấm bài ngay cả khi trình duyệt đã đóng.
- Thiết kế mobile không để đồng hồ hoặc nút cố định che câu hỏi.

#### 7.5. Tính đúng đắn và an toàn của lượt thi

- Server là nguồn quyết định thời gian, điều kiện dự thi, số lượt, trạng thái và điểm.
- Không gửi đáp án đúng, lời giải hoặc khóa chấm xuống client trong quá trình thi, kể cả trong JSON ẩn hay dữ liệu preload.
- Khi bắt đầu, dùng transaction/unique constraint phù hợp để tránh hai request tạo hai lượt vượt hạn mức.
- Lưu thứ tự câu và lựa chọn của từng lượt; reload không xáo lại đề.
- Autosave có version/thứ tự cập nhật để request cũ không ghi đè đáp án mới.
- Submit phải idempotent: nhấn nhiều lần hoặc retry không tạo kết quả trùng và không chấm lại ngoài ý muốn.
- Xử lý tranh chấp giữa autosave, nộp bài và hết giờ theo transaction; bài đã kết thúc không nhận sửa đáp án.
- Chốt chính sách cửa sổ tiếp nhận request tại hạn giờ; không dùng timestamp tự khai của client để chấp nhận bài quá hạn.
- Có tác vụ bền vững đóng lượt hết hạn, đồng thời kiểm tra hạn ở API; IIS recycle hoặc restart không làm kéo dài bài thi.
- Quy định một lượt đang hoạt động cho mỗi thí sinh/kỳ thi; xử lý tab/thiết bị thứ hai rõ ràng để không ghi đè chéo.
- Điểm tính từ snapshot và câu trả lời hợp lệ ở backend. Nếu có phúc khảo/chấm lại, lưu nguyên bản, lý do, người thực hiện và kết quả điều chỉnh.
- Ghi log nghiệp vụ cần thiết; không quảng cáo chống gian lận tuyệt đối. Chuyển tab chỉ là tín hiệu nếu bật, không tự kết luận gian lận; không thêm camera/nhận diện sinh trắc học ngoài phạm vi.

#### 7.6. Kết quả và báo cáo

- Cấu hình công bố ngay, sau khi kỳ thi kết thúc hoặc sau khi duyệt.
- Phân quyền riêng cho điểm, chi tiết đúng/sai và lời giải; không lộ qua endpoint phụ trước lịch công bố.
- Thí sinh chỉ xem kết quả của mình; bảng xếp hạng mặc định tắt, chỉ bật với chính sách hiển thị phù hợp.
- Báo cáo theo kỳ thi, đơn vị, nhóm, người tham gia, đã nộp, chưa thi, điểm và thời gian.
- Giải thích mẫu số và phạm vi của tỷ lệ hoàn thành/đạt; phân biệt số người và số lượt.
- Xuất CSV/XLSX theo quyền, chống formula injection và có nhật ký xuất dữ liệu.
- Có trang chi tiết lượt để đối soát snapshot, đáp án đã lưu, thời điểm và lịch sử chấm; không cho sửa điểm âm thầm.

### 8. Dữ liệu, API và tích hợp

Đề xuất ERD và data dictionary tối thiểu bao gồm:

- User, Role, Permission, UserRole, OrganizationUnit, Group, Session.
- Post, Category, Tag, Page, MediaAsset, Attachment, Menu, MenuItem.
- HomepageRevision, HomepageSection, Banner, SiteSetting, Document, Album, Video, ContactRequest.
- Question, QuestionVersion, Choice, ExamTemplate, ExamVersion, ExamQuestion.
- ExamSession/KỳThi, Eligibility/Assignment, Attempt, AttemptQuestionSnapshot, Answer, Result, GradeRevision, AuditLog.

Tên entity có thể điều chỉnh theo stack, nhưng quan hệ và bất biến nghiệp vụ phải rõ. Định nghĩa khóa ngoại, index, unique constraints, version, soft delete và chính sách lưu giữ. Không lạm dụng JSON cho dữ liệu nghiệp vụ cần ràng buộc; cấu hình khối CMS có thể dùng JSON schema được validate.

API cần có tài liệu, phân trang, bộ lọc, lỗi thống nhất, request ID, validation server và cơ chế xử lý đồng thời. Nếu dùng envelope `{ success, data, error }`, frontend phải bóc tách nhất quán; không để component nhận nhầm envelope thay cho mảng hoặc đối tượng dữ liệu.

Không cache dùng chung dữ liệu cá nhân, đề thi hay kết quả riêng tư. Cache công khai có key và invalidation khi xuất bản nội dung. Tìm kiếm công khai không trả bài nháp, tài liệu nội bộ hoặc câu hỏi thi.

### 9. Bảo mật, hiệu năng và vận hành

- Hash mật khẩu bằng thuật toán phù hợp; cookie phiên HttpOnly, Secure khi HTTPS, SameSite đúng kiến trúc; bảo vệ CSRF nếu dùng cookie.
- Giới hạn thử đăng nhập, reset an toàn, thu hồi phiên; hỗ trợ MFA cho admin nếu hạ tầng cho phép.
- HTTPS là yêu cầu khi vận hành đăng nhập/thi thực tế. Nếu máy chủ cũ còn HTTP, nêu việc cần cấu hình chứng chỉ trên chính môi trường đó, không tự đổi hosting hoặc tắt bảo vệ để chạy tạm.
- Chống XSS, injection, mass assignment, IDOR và truy cập trái quyền; validate đầu vào, sanitize rich text.
- Upload giới hạn kích thước và loại thực, tên lưu an toàn, không thực thi tệp trong thư mục upload; có phương án kiểm tra tệp độc hại phù hợp môi trường.
- Tệp công khai và tệp hạn chế quyền phải có cơ chế phục vụ khác nhau; không để tài liệu riêng có URL tĩnh vượt kiểm tra quyền.
- Không ghi mật khẩu, token, đáp án chuẩn hoặc dữ liệu nhạy cảm vào log công khai.
- Tối ưu ảnh, thumbnail, responsive image, lazy loading và truy vấn/index; không tải cả ngân hàng câu hỏi hoặc toàn bộ báo cáo vào một request.
- Dùng secret/env bên ngoài source; cung cấp `.env.example` không chứa bí mật.
- Đo tải thi đồng thời theo con số được xác nhận. Nếu chưa có, lập kịch bản giả định có nhãn rõ, không cam kết năng lực chưa đo.
- Có health check, log xoay vòng, cảnh báo lỗi, sao lưu database/media và kiểm tra phục hồi.
- Job lên lịch xuất bản và đóng lượt phải hoạt động ổn định sau restart; không dựa duy nhất vào bộ nhớ tiến trình hoặc tab trình duyệt.

### 10. Triển khai trên môi trường cũ và chuyển dữ liệu

#### 10.1. Triển khai

Tạo tài liệu đúng cấu hình đã xác minh: runtime, build, publish, IIS binding, rewrite/reverse proxy, API route, static files, quyền upload, TLS, dịch vụ tự khởi động, biến môi trường và migration database. Nếu có Next.js image optimizer, kiểm tra ảnh upload mới qua proxy, host/path và cấu hình ảnh; không chỉ kiểm tra ảnh có sẵn.

Media nằm ở vị trí bền vững ngoài thư mục release bị thay thế. Deploy không ghi đè uploads, secret, database hoặc log cần giữ. Bàn giao source cùng lockfile, migration, cấu hình mẫu và release tương ứng, lưu Git từ đầu để source và bản deploy truy vết được.

Có staging riêng, release checklist, kiểm tra sau deploy và rollback tương thích schema. Không hứa rollback chỉ bằng thay thư mục khi migration đã phá dữ liệu. Tránh release trong thời gian thi; quy trình phải tính đến bài đang làm và các request chưa hoàn tất.

#### 10.2. Chuyển dữ liệu cũ

- Kiểm kê bài, chuyên mục, ảnh, tài liệu, trang tĩnh và liên kết ngoài.
- Sao lưu trước khi xử lý; map trường và danh mục cũ → mới.
- Giữ nguồn gốc, ngày đăng, tác giả, nội dung và tệp khi có dữ liệu.
- Chuyển ảnh về kho phù hợp quyền sử dụng, kiểm tra tệp hỏng và link trong HTML.
- Import có thể chạy lại an toàn, chống trùng bằng khóa nguồn, log lỗi và báo cáo đối soát.
- Map URL và redirect 301 để giữ đường dẫn truy cập/SEO; không redirect mọi trang cũ về trang chủ.
- Chạy thử, kiểm tra mẫu và tổng số, đồng bộ phát sinh trước thời điểm chuyển.
- Nếu chuyển tài khoản, không giả định hash cũ tương thích; dùng phương án migration/reset an toàn.
- Phân biệt hệ thống nội bộ cũ với cổng thi mới; không thay thế nghiệp vụ nội bộ chưa được khảo sát.

### 11. Kiểm thử và tiêu chí nghiệm thu

Tạo ma trận: yêu cầu → module → kiểm thử → kết quả. Ưu tiên integration/E2E cho nghiệp vụ rủi ro, không chỉ test giao diện hiển thị.

| Nhóm | Điều kiện nghiệm thu tối thiểu |
|---|---|
| UI/UX | Kiểm tra 360, 390, 768, 1024, 1440px; không tràn ngang ngoài vùng dữ liệu cho phép; menu, focus, form hoạt động |
| CMS | Tạo/sửa/xóa hoặc ẩn/khôi phục từng nhóm nội dung ở mục 5; trang chủ cập nhật sau xuất bản không cần deploy |
| Bố cục | Nháp không rò ra công khai; sắp xếp, preview, publish và rollback đúng phiên bản |
| Biên tập | Vai trò đúng quyền; chặn sửa ngoài phạm vi; phát hiện ghi đè đồng thời |
| Media | Upload mới, thay ảnh, tải tài liệu và hiển thị qua IIS vẫn đúng sau release/restart |
| Tài khoản | Login/logout/reset/khóa phiên đúng; thí sinh không truy cập admin bằng URL/API |
| Điều kiện thi | Chặn chưa mở, đã đóng, chưa được giao, vượt lượt; hai request bắt đầu không vượt hạn mức |
| Đề thi | Đúng ma trận, không trùng câu ngoài chính sách; snapshot và thứ tự giữ nguyên sau reload |
| Lưu bài | Autosave, mất mạng, request ngược thứ tự, reload và đa tab không âm thầm mất dữ liệu đã xác nhận |
| Kết thúc | Nộp lặp idempotent; hết giờ vẫn đóng khi tắt trình duyệt; sửa đồng hồ máy không kéo dài lượt |
| Chấm điểm | Một đáp án, nhiều đáp án, bỏ trống, điểm đạt, làm tròn và cách lấy điểm cuối có ca kiểm thử rõ |
| Bí mật đề | Payload, HTML, cache và API thí sinh không lộ đáp án/lời giải trước chính sách công bố |
| Báo cáo | Điểm và số lượt đối soát được; phạm vi truy cập/xuất file đúng, chống formula injection |
| SEO/migration | Metadata, canonical, sitemap, redirect và link nội bộ đúng; có báo cáo dữ liệu thiếu |
| Vận hành | Build thành công, smoke test trên môi trường tương đương, thử backup/restore và tác vụ sau restart |

Đo hiệu năng trang công khai và tải kỳ thi với cấu hình, dữ liệu, số người đồng thời được ghi rõ. Báo cáo số đo và hạn chế; không ghi “production-ready”, “bảo mật tuyệt đối” hoặc “đã đạt” nếu chưa có bằng chứng.

### 12. Trình tự thực hiện và sản phẩm bàn giao

Thực hiện tuần tự theo các mốc có sản phẩm kiểm tra được:

1. Khảo sát hiện trạng, bảng yêu cầu, giả định, đầu vào còn thiếu và đề xuất kiến trúc tương thích.
2. Sitemap ba khu vực, user flow, ERD, ma trận phân quyền và hợp đồng API.
3. Design system và thiết kế trang chủ desktop/mobile, trang bài viết, admin CMS, danh sách kỳ thi và màn hình làm bài.
4. Xây nền tảng auth, database, API, media, logging và quyền.
5. Hoàn thành cổng thông tin cùng CMS quản lý toàn bộ nội dung/bố cục.
6. Hoàn thành ngân hàng câu hỏi, đề, kỳ thi, lượt thi, chấm điểm và báo cáo.
7. Chuyển dữ liệu thử, kiểm thử tích hợp, kiểm thử tải và sửa lỗi.
8. Đóng gói release, tài liệu vận hành và kế hoạch chuyển đổi trên môi trường cũ.

Bàn giao đầy đủ:

- Source frontend/backend có cấu trúc rõ, dependency lock và lịch sử Git.
- Schema/migration, dữ liệu mẫu có nhãn và cách tạo quản trị viên ban đầu an toàn.
- Tài liệu API, quyền, cấu hình, biến môi trường và sơ đồ triển khai.
- Hướng dẫn biên tập nội dung, tùy chỉnh trang chủ, tạo kỳ thi, nhập câu hỏi, cấp quyền và xem báo cáo.
- Hướng dẫn build/deploy trên máy chủ cũ, backup/restore, rollback và xử lý lỗi thông dụng.
- Script chuyển dữ liệu, URL mapping và báo cáo đối soát.
- Checklist nghiệm thu, kết quả kiểm thử thực tế, hạn chế và phần còn cần đầu vào.

### 13. Cách làm việc và định dạng phản hồi

Giao tiếp bằng tiếng Việt, giải thích rõ quyết định ảnh hưởng tới người dùng và vận hành. Chủ động chọn giải pháp hợp lý cho chi tiết có thể thay đổi; không hỏi lại các yêu cầu đã chốt trong prompt này.

Nếu còn thiếu thông tin chặn kiến trúc hoặc triển khai, hỏi tối đa ba câu ngắn trong một lần về: source/database hiện có, cấu hình môi trường cũ và quy mô/chính sách kỳ thi. Tiếp tục hoàn thành những việc không phụ thuộc câu trả lời. Không yêu cầu gửi mật khẩu production trong hội thoại.

Mỗi mốc báo cáo: đã hoàn thành, cách kiểm chứng, việc còn lại và ràng buộc thực tế. Phân biệt prototype dùng dữ liệu mẫu với hệ thống có backend thật. Không để nút bấm giả hoặc thông báo thành công giả.

Bắt đầu bằng bản tóm tắt kiến trúc ba khu vực, thiết kế trang chủ đề xuất, ma trận CMS và kế hoạch triển khai. Sau đó tiến hành xây dựng theo phạm vi đã được giao, không dừng ở một bản kế hoạch hoặc giao diện minh họa.

---

## Ghi chú sử dụng

- Yêu cầu đã chốt: ba khu vực riêng, admin toàn hệ thống, thi có tài khoản, CMS quản lý toàn bộ nội dung trang chủ, giao diện hiện đại và giữ môi trường máy chủ cũ.
- Thông số máy chủ, source/database, số người thi đồng thời và quy chế thi cần được xác minh khi thực thi; không có cấu hình nào trong tài liệu được coi là đã khảo sát trực tiếp máy chủ.
- Website tham chiếu dùng để khảo sát nội dung: https://congdoandsvn.org.vn/.
- Khi muốn bắt đầu triển khai với trợ lý, gửi: **HÃY THỰC THI PROMPT NÀY** cùng mã nguồn hoặc thông tin môi trường cần thiết.
