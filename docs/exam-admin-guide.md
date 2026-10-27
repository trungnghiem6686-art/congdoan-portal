# Hướng dẫn tổ chức thi trắc nghiệm

Dành cho người có vai trò **Quản lý kỳ thi**. Người xem báo cáo cần vai trò **Xem báo cáo**.

Quy chế mặc định có thể cấu hình cho từng kỳ thi:
- số lượt;
- cách lấy điểm;
- thời điểm công bố kết quả.

Quy chế thực tế của đơn vị **chưa được xác nhận**. Hãy đặt theo quy chế khi tạo kỳ thi.

## 1. Thí sinh

*Thí sinh* → **Thêm thí sinh**: nhập mã thí sinh (dùng làm tên đăng nhập), họ tên, đơn vị và mật khẩu ban đầu.

**Nhập danh sách** bằng tệp CSV, UTF-8, dòng đầu là tên cột:

```
ma,ho_ten,ma_don_vi,mat_khau
TS0001,Nguyễn Văn A,DV01,
TS0002,Trần Thị B,DV02,Tam12345a
```

- `ma_don_vi`: không bắt buộc. Nếu có, phải trùng mã đơn vị đã khai báo trong *Đơn vị, nhóm*.
- `mat_khau`: không bắt buộc. Nếu bỏ trống, hệ thống tạo mật khẩu ngẫu nhiên và hiện ra **một lần duy nhất** sau khi nhập; hãy lưu lại để phát cho thí sinh. Mật khẩu tự đặt cần ít nhất 8 ký tự, có cả chữ và số.
- Nhập theo nguyên khối: chỉ cần một dòng lỗi (mã trùng, thiếu họ tên…) là **không dòng nào được ghi**, và màn hình báo lỗi theo số dòng.
- Thí sinh phải đổi mật khẩu khi đăng nhập lần đầu.

Thao tác với tài khoản thí sinh:
- **Đặt lại MK**: tạo mật khẩu tạm mới, buộc đổi lại khi đăng nhập.
- **Khóa**: đăng xuất thí sinh khỏi mọi phiên ngay lập tức.

Nhóm thí sinh (*Đơn vị, nhóm*) giúp giao kỳ thi cho nhiều người cùng lúc.

## 2. Ngân hàng câu hỏi

*Ngân hàng câu hỏi* → **Thêm câu hỏi**:
- loại: một đáp án, nhiều đáp án, hoặc đúng/sai;
- chủ đề, mức độ (Dễ / Trung bình / Khó), điểm;
- các phương án, đánh dấu phương án đúng;
- giải thích (không bắt buộc).

Hệ thống từ chối câu không có phương án đúng hoặc có hai phương án trùng nhau.

- Câu hỏi mới ở trạng thái **Chưa duyệt**. Chỉ câu **Đã duyệt** mới đưa vào đề được. Người có quyền duyệt nhấn **Duyệt**.
- Sửa một câu đã dùng sẽ tạo **phiên bản mới**. Bài thi đã bắt đầu vẫn giữ nguyên câu như lúc bắt đầu.

**Nhập từ tệp** (CSV hoặc XLSX). Các cột:

```
loai,chu_de,muc_do,noi_dung,dap_an_a,dap_an_b,dap_an_c,dap_an_d,dap_an_dung,diem,giai_thich
mot_dap_an,An toàn lao động,de,Câu hỏi MẪU?,Phương án A,Phương án B,Phương án C,Phương án D,a,1,
nhieu_dap_an,Điều lệ,trung_binh,Câu hỏi MẪU 2?,A,B,C,,"a,c",2,Giải thích
```

- `loai`: `mot_dap_an` | `nhieu_dap_an` | `dung_sai`.
- `muc_do`: `de` | `trung_binh` | `kho`.
- Phương án dùng các cột `dap_an_a` … `dap_an_h`.
- `dap_an_dung`: các chữ cái của phương án đúng, cách nhau bằng dấu phẩy.

Màn hình **xem trước** báo lỗi theo dòng. Chỉ khi không còn lỗi mới xác nhận ghi được, và ghi theo nguyên khối.

## 3. Tạo kỳ thi

*Kỳ thi* → **Thêm mới**:

| Trường | Ý nghĩa |
|---|---|
| Tên kỳ thi, Mã kỳ thi | Hiển thị cho thí sinh |
| Giờ mở / Giờ đóng | Nhập theo giờ trên máy của bạn. Ngoài khoảng này thí sinh không bắt đầu được. Lượt đang làm kết thúc tại thời điểm sớm hơn trong hai mốc: *giờ bắt đầu + thời lượng*, hoặc *giờ đóng*. |
| Thời lượng (phút) | Đồng hồ tính theo máy chủ. Thí sinh chỉnh giờ máy không kéo dài được. |
| Số lượt tối đa | Hai yêu cầu bắt đầu gửi cùng lúc cũng không vượt được giới hạn này |
| Điểm cuối lấy theo | **Lượt cao nhất**, **Lượt gần nhất** hoặc **Lượt đầu tiên** |
| Điểm đạt (thang 10) | Ngưỡng đạt |
| Công bố kết quả | **Ngay sau khi nộp**, **Sau khi kỳ thi đóng**, hoặc **Sau khi duyệt** (phải nhấn *Duyệt công bố kết quả*) |
| Nguồn đề | **Chọn tay** từng câu, hoặc **Rút ngẫu nhiên theo ma trận**: số câu theo chủ đề và mức độ, không lặp câu |
| Hướng dẫn cho thí sinh | Hiện ở trang giới thiệu kỳ thi |
| Đối tượng được giao | Thí sinh, nhóm hoặc đơn vị. Thí sinh không được giao sẽ không thấy kỳ thi, và mở bằng đường dẫn trực tiếp cũng bị chặn. |

Các bước tiếp theo:
1. **Lưu đề** → **Kiểm tra đề**. Hệ thống báo nếu ngân hàng không đủ câu đã duyệt cho ma trận.
2. **Công bố**: kỳ thi hiện với thí sinh được giao. Từ lúc này không đổi được câu hỏi của những lượt đã bắt đầu.
3. **Đóng kỳ thi** khi cần dừng sớm.
4. Kỳ thi đã có kết quả thì không xóa được, chỉ **Lưu trữ**.

## 4. Trong giờ thi

- Thí sinh đăng nhập tại `/thi`, chọn kỳ thi rồi nhấn **Bắt đầu làm bài**.
- Câu trả lời được **tự động lưu** từng câu. Màn hình luôn cho biết đang lưu, đã lưu hay chưa lưu được.
- Khi mất mạng, câu trả lời được giữ trên máy và tự gửi lại khi có mạng.
- Tải lại trang hoặc đổi máy thì làm tiếp từ chỗ cũ, giữ nguyên thứ tự câu.
- Mỗi lúc chỉ **một tab** được ghi bài. Tab thứ hai mở ra ở chế độ chỉ xem, và có thể giành quyền ghi.
- Hết giờ, máy chủ tự đóng và chấm bài với các câu đã lưu, kể cả khi thí sinh đã tắt trình duyệt.
- Không phát hành phiên bản mới hay khởi động lại máy chủ trong giờ thi (xem `docs/deploy-iis.md`).

## 5. Kết quả và báo cáo

*Báo cáo* → chọn kỳ thi:
- Báo cáo tách riêng số **người** và số **lượt**: một người có thể làm nhiều lượt.
- **Chi tiết** cho xem bài làm từng câu. Chỉ người có quyền xem chi tiết mới thấy đáp án và giải thích.
- **Chấm lại**: nhập *Điểm mới (thang 10)* và *Lý do*. Điểm gốc vẫn được giữ, và thao tác được ghi vào nhật ký.
- **Xuất CSV** hoặc **Xuất Excel**. Ô bắt đầu bằng `=`, `+`, `-`, `@` được vô hiệu hóa để chống chèn công thức. Mỗi lần xuất được ghi vào nhật ký.

Thí sinh chỉ thấy kết quả của chính mình, theo chính sách công bố của kỳ thi. Đáp án đúng không bao giờ được gửi tới trình duyệt khi đang làm bài.
