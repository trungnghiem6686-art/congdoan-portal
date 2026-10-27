# Ma trận nghiệm thu

Ma trận nối mỗi yêu cầu (theo mục 11 của tài liệu yêu cầu) với module, kiểm thử và kết quả.

## Môi trường kiểm thử

Kết quả lấy từ lần chạy ngày **2026-10-27**, commit `e0dd210`, trên môi trường **phát triển**:

| Thành phần | Phiên bản / cấu hình |
|---|---|
| Hệ điều hành | Linux container, 4 vCPU, 15 GB RAM |
| SQL Server | 2022, chạy trong Docker |
| .NET SDK | 8.0.132 |
| Node.js | 22.22.0 |
| Trình duyệt kiểm thử | Chromium của Playwright, múi giờ Asia/Ho_Chi_Minh |

Đây **không phải** Windows Server/IIS, nên mọi điều kiện cần IIS hay Windows đều ghi "chưa kiểm chứng".

| Bộ kiểm thử | Lệnh | Kết quả |
|---|---|---|
| Backend: tích hợp với SQL Server thật | `cd backend && dotnet test CongDoan.sln` | **151/151 đạt** (Shared 7, Identity 16, Cms 68, Exam 60) |
| Frontend: đơn vị | `cd frontend && npx vitest run` | **14/14 đạt** |
| Frontend: E2E, API và web thật | `cd frontend && npx playwright test` | **37/37 đạt**, chạy hai lần liên tiếp |
| Lint, kiểu | `npx tsc --noEmit && npx eslint .` | đạt |
| Build release | `dotnet build`, `next build` (standalone) | đạt. Bản standalone chạy được bằng `node server.js`: trang chủ, tệp tĩnh và `/api` đều trả 200 |
| Script migration idempotent | `dotnet ef migrations script --idempotent` (4 module), áp 2 lần lên database trống bằng `sqlcmd -I` | đạt: 47 bảng, khớp database phát triển; lần 2 không lỗi |
| Sao lưu/khôi phục (T-SQL) | lệnh trong `deploy/backup.ps1` và `restore.ps1`, chạy bằng sqlcmd trên database thử | đạt: xóa 27 dòng, khôi phục lại đủ 27, database về MULTI_USER |
| Công cụ nhập dữ liệu cũ | `Importer run` trên dữ liệu MẪU: chạy thử, chạy thật, chạy lại | đạt: 2 bài và 1 chuyên mục được tạo, 3 redirect, 1 cảnh báo ảnh thiếu; lần chạy lại không tạo thêm |
| Tải kỳ thi | `tests/Load/exam-load.js` (k6) | **chưa đo**: môi trường này không được phép tải k6. Kịch bản 3000 thí sinh là **giả định** |
| Hiệu năng trang công khai | | **chưa đo** |

Tên test trong bảng dưới:
- `Lớp.Phương_thức` là test backend (xUnit), ví dụ `StartAttemptTests.Two_Parallel_Starts_Create_One_Attempt`.
- `tên_test` là test Playwright trong `frontend/tests/e2e/`.

## Ma trận

| Nhóm | Điều kiện | Module | Kiểm thử | Kết quả |
|---|---|---|---|---|
| **UI/UX** | Không tràn ngang ở 360, 390, 768, 1024, 1440px | Web công khai | `no_horizontal_scroll_at_{360,390,768,1024,1440}` | Đạt |
| | Xem trước trang chủ ở 3 cỡ màn hình | Admin | `preview_three_viewports_does_not_change_public` | Đạt |
| | Đồng hồ thi không che câu hỏi trên điện thoại | Thi | `mobile_timer_does_not_cover_question` | Đạt |
| | Menu, focus, form dùng được bằng bàn phím | Web | (kiểm tra tay) | Chưa kiểm chứng, chưa có test tự động |
| **CMS** | Tạo, sửa, ẩn/hiện, xóa, khôi phục từng nhóm nội dung | Cms | `ContentCrudTests.Each_Group_Create_Update_Hide_Restore` (theo từng nhóm), `PostWorkflowTests.Soft_Delete_And_Restore` | Đạt |
| | Trang chủ cập nhật sau xuất bản mà không cần deploy | Cms + Web | `HomepageBuilderTests.Publish_Swaps_Atomically_And_Invalidates_Cache`, `publish_then_public_updates_without_deploy` | Đạt |
| | Bài hẹn giờ tự xuất bản, kể cả sau khi khởi động lại | Cms | `SchedulingTests.Scheduled_Post_Publishes_When_Clock_Passes`, `SchedulingTests.Worker_Restart_Does_Not_Double_Publish` | Đạt |
| | Chèn ảnh, video, YouTube vào bài | Admin | `insert_image_from_computer_into_article_body_and_shows_publicly`, `paste_and_drag_drop_image`, `insert_uploaded_video_and_youtube_embed` | Đạt |
| **Bố cục** | Nháp không rò ra trang công khai | Cms | `HomepageBuilderTests.Draft_Change_Not_In_Public_Response`, `PostWorkflowTests.Draft_Not_Visible_On_Public_Endpoint` | Đạt |
| | Sắp xếp, xem trước, xuất bản, rollback đúng phiên bản | Cms + Admin | `HomepageBuilderTests.Stable_SectionId_Survives_Reorder`, `HomepageBuilderTests.Rollback_Restores_Previous_Version`, `block_reorder_by_drag_and_by_buttons`, `rollback_restores` | Đạt |
| **Biên tập** | Vai trò đúng quyền | Identity | `RbacTests.Editor_Cannot_Publish_Returns_403`, `editor_cannot_see_publish_button_and_api_403`, `HomepageBuilderTests.Editor_Cannot_Publish_Homepage` | Đạt |
| | Chặn sửa ngoài phạm vi | Identity + Cms | `RbacTests.Scoped_Role_Cannot_Access_Other_OrgUnit_Resource`, `PostWorkflowTests.Editor_Cannot_Edit_Someone_Elses_Post` | Đạt |
| | Phát hiện ghi đè đồng thời | Cms | `PostWorkflowTests.Concurrent_Edit_With_Stale_RowVersion_Returns_409`, `stale_version_shows_conflict_dialog` | Đạt |
| | Cảnh báo rời trang khi chưa lưu | Admin | `unsaved_changes_warning` | Đạt |
| **Media** | Kiểm tra loại tệp thật, giới hạn kích thước, tên lưu ngẫu nhiên | Cms | `MediaTests.Rejects_Exe_Renamed_As_Jpg_By_Magic_Bytes`, `MediaTests.Upload_Over_Configured_Limit_Rejected_With_Clear_Message`, `MediaTests.Stored_Name_Is_Random_Not_User_Supplied` | Đạt |
| | Tệp hạn chế cần quyền | Cms | `MediaTests.Restricted_File_Requires_Permission` | Đạt |
| | Tải video theo phần, ảnh thu nhỏ | Cms | `MediaTests.Video_Mp4_Uploaded_In_Chunks_Reassembles_Identical_Bytes`, `MediaTests.Image_Gets_Thumbnail` | Đạt |
| | Upload mới hiển thị qua IIS sau release/restart | Vận hành | Danh sách smoke test trong `docs/deploy-iis.md` mục 6 | **Chưa kiểm chứng**: chưa có máy chủ IIS |
| | Quét mã độc tệp tải lên | Vận hành | | **Chưa làm**: cần phương án theo máy chủ thật (ví dụ Microsoft Defender quét thư mục media) |
| **Tài khoản** | Đăng nhập, đăng xuất, khóa sau 5 lần sai, khóa tài khoản thì hủy phiên | Identity | `AuthTests.Login_Locks_After_5_Failures_For_15_Minutes`, `AuthTests.Logout_Revokes_Session`, `AuthTests.Locked_Account_Invalidates_Existing_Session`, `RbacTests.Locking_User_Revokes_Their_Sessions` | Đạt |
| | Đổi mật khẩu lần đầu, sai mật khẩu và sai tên trả cùng một lỗi | Identity | `AuthTests.FirstLogin_Forces_Password_Change`, `AuthTests.Wrong_Password_And_Unknown_User_Return_Same_Error` | Đạt |
| | Thí sinh không vào được admin bằng URL hay API | Identity | `AuthTests.Candidate_Cookie_Cannot_Call_Admin_Endpoint_Returns_403`, `login_required_redirects` | Đạt |
| | CSRF; cookie Secure sau proxy HTTPS | Identity | `AuthTests.Write_Without_Antiforgery_Token_Rejected`, `AuthTests.Behind_Https_Proxy_Session_Cookie_Is_Secure` | Đạt |
| | MFA cho admin | Identity | | **Chưa làm** (yêu cầu ghi "nếu hạ tầng cho phép") |
| **Điều kiện thi** | Chặn khi chưa mở, đã đóng, chưa được giao, vượt số lượt | Exam | `StartAttemptTests.Before_Open_And_After_Close_Rejected`, `StartAttemptTests.Not_Assigned_Returns_403`, `StartAttemptTests.Over_Max_Attempts_Rejected`, `unassigned_exam_url_blocked` | Đạt |
| | Hai request bắt đầu đồng thời không vượt hạn mức | Exam | `StartAttemptTests.Two_Parallel_Starts_Create_One_Attempt` (SQL Server thật) | Đạt |
| **Đề thi** | Đúng ma trận, không trùng câu | Exam | `ExamSetupTests.Random_Draw_Has_No_Duplicate_Questions`, `ExamSetupTests.Publish_Fails_When_Bank_Has_Fewer_Questions_Than_Matrix`, `exam_publish_blocked_when_bank_short` | Đạt |
| | Snapshot bất biến, thứ tự giữ nguyên sau reload | Exam + Thi | `StartAttemptTests.Editing_Bank_After_Start_Does_Not_Change_Attempt`, `StartAttemptTests.Reload_Keeps_Order`, `reload_resumes_same_order_and_answers` | Đạt |
| | Nhập câu hỏi theo nguyên khối, báo lỗi theo dòng | Exam | `QuestionTests.Import_With_One_Bad_Row_Writes_Nothing_And_Reports_Row`, `QuestionTests.Import_Xlsx_Uses_Same_Rules`, `import_preview_shows_row_errors_and_blocks_commit` | Đạt |
| **Lưu bài** | Autosave hiển thị trạng thái | Thi | `autosave_shows_saving_saved_error` | Đạt |
| | Mất mạng rồi có mạng lại | Thi | `offline_then_online_marks_unsynced_then_syncs` | Đạt |
| | Request đến sai thứ tự | Exam | `AutosaveTests.Stale_Seq_Does_Not_Overwrite_Newer_Answer` | Đạt |
| | Mở nhiều tab | Exam | `AutosaveTests.Second_Tab_Gets_ReadOnly_Conflict` | Đạt |
| | Lưu bài cùng lúc với nộp bài: câu đã báo "đã lưu" luôn được chấm, bài nộp luôn được chấm | Exam | `SaveSubmitRaceTests.First_Saves_Racing_Submit_Leave_No_Ungraded_Answers` (12 bài × 8 câu, SQL Server thật) | Đạt |
| **Kết thúc** | Nộp lặp idempotent | Exam | `SubmitTests.Submit_Twice_Returns_Same_Result_One_Row`, `ExpiryTests.Expired_Then_Submit_Is_Idempotent_Noop` | Đạt |
| | Hết giờ vẫn đóng khi đã tắt trình duyệt; khởi động lại không kéo dài giờ | Exam | `ExpiryTests.Expired_Attempt_Closed_And_Graded_Without_Browser`, `ExpiryTests.Worker_Restart_Mid_Exam_Does_Not_Extend_Deadline` | Đạt |
| | Sửa đồng hồ máy không kéo dài lượt | Exam + Thi | `AutosaveTests.Save_After_Deadline_Rejected_Ignoring_Client_Timestamp`, `timer_uses_server_deadline_not_device_clock`, `after_deadline_ui_locked`, `ExpiryTests.Timestamps_Read_From_Database_Are_Serialized_As_Utc` | Đạt |
| **Chấm điểm** | Một đáp án, nhiều đáp án, bỏ trống, trọng số | Exam | `SubmitTests.Blank_Answer_Scores_Zero`, `SubmitTests.Multiple_Choice_Partial_Selection_Scores_Zero`, `GradingTests.Weighted_Points_Are_Respected` | Đạt |
| | Làm tròn, điểm đạt, cách lấy điểm cuối | Exam | `GradingTests.Rounding_And_PassScore_Boundaries`, `GradingTests.FinalScore_Highest_Latest_First` | Đạt |
| | Chấm lại giữ bản gốc và lý do | Exam | `ResultVisibilityTests.Regrade_Keeps_Original_And_Reason` | Đạt |
| **Bí mật đề** | API thí sinh không lộ đáp án hay lời giải | Exam | `StartAttemptTests.Payload_Contains_No_Correct_Flag_Or_Explanation`, `AuditTests.Audit_Redacts_Password_And_CorrectAnswer_Fields` | Đạt |
| | Kết quả theo chính sách công bố; xem chi tiết cần quyền riêng | Exam | `ResultVisibilityTests.Result_Hidden_Before_Release`, `ResultVisibilityTests.After_Approval_Needs_Admin_Approval`, `ResultVisibilityTests.Detail_And_Explanation_Need_Separate_Permission`, `ResultVisibilityTests.Candidate_Cannot_Read_Other_Result_By_Id` | Đạt |
| | Tìm kiếm công khai không trả nội dung thi | Cms | `PublicApiTests.Search_Never_Returns_Draft_Or_Internal_Or_Exam_Content` | Đạt |
| | Cache: trang `/thi` và `/admin` gửi `Cache-Control: no-store` | Web | (cấu hình trong `next.config.ts`) | Chưa có test tự động |
| **Báo cáo** | Điểm và số lượt đối soát được | Reporting | `ReportTests.Report_Distinguishes_People_From_Attempts`, `DashboardTests.Dashboard_Counts_Match_Database`, `dashboard_numbers_match_database_seed` | Đạt |
| | Xuất file chống formula injection, có ghi nhật ký | Reporting | `ExportTests.Export_Escapes_Formula_Prefixes`, `ExportTests.Csv_Quotes_Commas_Quotes_And_Newlines`, `ExportTests.Export_Writes_AuditRow`, `report_export_downloads_csv` | Đạt |
| **SEO / chuyển dữ liệu** | Metadata, canonical, sitemap, robots | Web + Cms | `sitemap_robots_and_canonical`, `SitemapTests.Sitemap_Lists_Public_Content_Only` | Đạt |
| | Đổi đường dẫn thì có redirect 301 (Next.js trả 308), không chuyển về trang chủ | Cms + Web | `ContentCrudTests.Rename_Updates_Slug_And_Old_Url_Redirects_301`, `ContentCrudTests.Renamed_Post_Old_Slug_Redirects_301`, `renamed_menu_url_redirects_permanently`, `rename_menu_item_changes_url_and_old_url_redirects`, `manage_redirects_from_admin` | Đạt |
| | Nhập chạy lại an toàn, ảnh hỏng không làm dừng, có báo cáo đối soát | Importer | `ImporterTests.Rerun_Does_Not_Duplicate`, `ImporterTests.Broken_Image_Logged_Not_Fatal`, `ImporterTests.Unreadable_Image_File_Is_Reported_Not_Fatal`, `ImporterTests.Old_Url_Redirects_301_Not_Home`, `ImporterTests.Reconciliation_Counts_Match_Fixture` | Đạt trên dữ liệu MẪU |
| | Chuyển dữ liệu thật của trang cũ | Importer | | **Chưa làm**: chưa có bản xuất hay quyền khảo sát trang cũ |
| **Vận hành** | Build thành công | Toàn bộ | build release (xem bảng trên) | Đạt |
| | Tác vụ nền đúng sau khởi động lại | Cms + Exam | `SchedulingTests.Worker_Restart_Does_Not_Double_Publish`, `ExpiryTests.Worker_Restart_Mid_Exam_Does_Not_Extend_Deadline` | Đạt |
| | Lỗi trả theo envelope có requestId, không lộ stack trace | Api | `ExceptionMiddlewareTests.ExceptionMiddleware_ReturnsEnvelopeWithRequestId_NoStackTrace` | Đạt |
| | Không ghi mật khẩu, token, đáp án vào log hay nhật ký | Audit | `AuditTests.Failed_Login_Is_Audited_Without_Password`, `AuditTests.Audit_Redacts_Password_And_CorrectAnswer_Fields`; đã kiểm tra tệp log API không chứa "password=" | Đạt |
| | Thử sao lưu và khôi phục | Vận hành | T-SQL đã thử (bảng trên) | Đạt một phần: script PowerShell **chưa kiểm chứng** trên Windows |
| | Smoke test trên môi trường tương đương production (IIS, TLS, Windows Service) | Vận hành | `docs/deploy-iis.md` mục 6 | **Chưa kiểm chứng** |
| | Đo tải kỳ thi với số thí sinh đã xác nhận | Exam | `tests/Load/exam-load.js` | **Chưa đo**. Số 3000 là giả định, chưa được xác nhận |

## Còn cần để nghiệm thu đầy đủ

1. Máy chủ staging Windows Server + IIS + SQL Server giống production. Trên đó chạy smoke test (`docs/deploy-iis.md` mục 6), `backup.ps1`/`restore.ps1` và đo tải bằng k6 với số thí sinh đã xác nhận. Ghi kết quả vào bảng này kèm cấu hình máy.
2. Chứng chỉ TLS cho tên miền thật.
3. Bản xuất dữ liệu trang cũ để viết bước chuyển đổi và chạy `Importer` thật.
4. Logo, thông tin liên hệ, nội dung chính thức thay cho dữ liệu MẪU.
5. Quy chế thi thực tế (số lượt, cách lấy điểm, công bố kết quả) để cấu hình kỳ thi.
6. Quyết định về MFA cho admin và về phương án quét mã độc tệp tải lên.

Không ghi "production-ready" hay "đã đạt" cho các mục chưa có bằng chứng ở trên.
