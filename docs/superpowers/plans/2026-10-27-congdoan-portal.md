# Cổng thông tin, CMS và cổng thi Công đoàn Đường sắt VN — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dựng hệ thống ba khu vực (`/`, `/admin`, `/thi`) với CMS quản lý toàn bộ nội dung trang chủ và cổng thi trắc nghiệm có tài khoản.

**Architecture:** ASP.NET Core 8 Web API theo Clean Architecture 4 lớp (Domain, Application, Infrastructure, Api), mỗi lớp chia thư mục theo module (Identity, Cms, Exam, Reporting) trên SQL Server; một frontend Next.js với ba nhóm route layout riêng. Kestrel chạy như Windows Service sau IIS reverse proxy; dịch vụ nền xử lý xuất bản theo lịch và đóng lượt thi hết hạn.

**Tech Stack:** Backend: .NET 8, EF Core 8 (SQL Server), FluentValidation, HtmlSanitizer, AngleSharp, AspNetCoreRateLimit, Serilog, Swashbuckle, xUnit + coverlet (test tích hợp trên SQL Server/LocalDB). Frontend: Next.js 16, React 19, TypeScript, Tailwind CSS 3 (+ Typography), React Hook Form, Zod, lucide-react, Swiper, sharp, DOMPurify, ESLint 9, Vitest, Playwright.

**Path mapping (áp dụng cho mọi task bên dưới):** `src/Api` → `backend/CongDoan.Api`; `src/Shared` và `src/Modules/<M>/…` → thư mục `<M>` (hoặc `Shared`) trong các lớp `backend/CongDoan.{Domain,Application,Infrastructure,Api}` tùy trách nhiệm; `tests/<M>.Tests` → `backend/tests/CongDoan.<M>.Tests`; `web/` → `frontend/`; `tools/Importer` → `backend/tools/CongDoan.Importer`.

**Spec:** `docs/superpowers/specs/2026-10-27-congdoan-portal-design.md`

## Global Constraints

- Backend .NET Core trên Windows Server/IIS, cơ sở dữ liệu SQL Server; không chuyển hosting.
- Thời gian lưu UTC, hiển thị Asia/Ho_Chi_Minh; mọi mã dùng `IClock`, không gọi `DateTime.UtcNow` trực tiếp.
- Hạn lượt thi = min(bắt đầu + thời lượng, giờ đóng kỳ thi); gia hạn sau hạn mặc định 0 giây.
- Tối đa một `Attempt` đang làm cho mỗi (Candidate, Exam); nhiều đáp án đúng phải chọn đúng toàn bộ tập mới có điểm; điểm cuối cấu hình: cao nhất / gần nhất / đầu tiên.
- Không gửi đáp án, lời giải hay khóa chấm xuống client khi đang thi.
- Test liên quan đồng thời, unique index lọc và transaction (Task 13–15) bắt buộc chạy trên SQL Server/LocalDB, không dùng SQLite.
- Cookie HttpOnly, Secure khi HTTPS; hai scheme tách biệt `Admin` và `Candidate`; không tự đăng ký.
- Không JavaScript tùy ý trong CMS; rich text sanitize; embed theo danh sách cho phép.
- Dữ liệu mẫu gắn nhãn "MẪU"; không bịa logo, liên hệ, lãnh đạo, số liệu.
- Màu: navy `#12365B`, xanh `#176BB0`, đỏ nhấn `#C62828`, nền `#F4F6F8`; font Be Vietnam Pro; nội dung từ 16px.
- Slug tự sinh từ tên (bỏ dấu), đổi tên thì đổi slug, không trùng, slug cũ chuyển hướng 301; ảnh tải từ máy và video chèn tự do trong bài viết.
- Module mới (Chatbot, Hồ sơ đoàn viên, Báo cáo chuyên đề) phải thêm được bằng một module riêng với schema và permission riêng, không sửa lõi.
- Secret ngoài source; có `.env.example`; không ghi mật khẩu/đáp án/token vào log.
- Mỗi commit kết thúc bằng hai dòng `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>` và `Claude-Session: https://claude.ai/code/session_01TnzMWwzDe7T6JZHSSo3FVR`.

## Review Focus

1. Hai request "bắt đầu thi" đồng thời của cùng thí sinh → chỉ một lượt được tạo, request kia nhận lượt đó hoặc lỗi rõ ràng (Task 13).
2. Autosave đến muộn với số phiên bản cũ → không ghi đè đáp án mới hơn (Task 14).
3. Nộp bài hai lần / retry sau timeout → một kết quả, điểm không đổi (Task 14).
4. Ô Excel/CSV bắt đầu bằng `=`, `+`, `-`, `@` khi xuất báo cáo → không thành công thức (Task 16).
5. Import câu hỏi có một dòng lỗi → không ghi dòng nào, báo lỗi theo dòng (Task 11).

## File Structure

```
CongDoan.sln
src/Api/                      host, Program.cs, middleware, cấu hình
src/Shared/                   IClock, ApiError, AuditLog, ApiEnvelope
src/Modules/Identity/         entities, DbContext, auth, RBAC
src/Modules/Cms/              posts, menus, banners, media, homepage builder
src/Modules/Exam/             questions, exams, attempts, grading, reports
tests/{Shared,Identity,Cms,Exam}.Tests/
web/src/app/(public)|admin|thi/   Next.js route groups
web/src/lib/api.ts            client duy nhất bóc envelope
tools/Importer/               import dữ liệu cũ
deploy/                       IIS web.config, service script, backup scripts
docs/                         spec, plan, hướng dẫn vận hành, ma trận nghiệm thu
```

---

### Task 1: Toolchain, solution và Shared

**Files:** Create `CongDoan.sln`, `src/Api/Program.cs`, `src/Shared/{IClock.cs,ApiError.cs,ApiEnvelope.cs}`, `tests/Shared.Tests/ClockTests.cs`, `.gitignore`, `.env.example`.

**Interfaces:**
- Produces: `interface IClock { DateTime UtcNow { get; } }`, `FakeClock : IClock { void Advance(TimeSpan) }`; `record ApiEnvelope<T>(bool Success, T? Data, ApiError? Error)`; `record ApiError(string Code, string Message, string RequestId)`; middleware gắn `X-Request-Id`.

- [ ] **Step 1:** Cài .NET 8 SDK vào workspace (`dotnet-install.sh --channel 8.0` qua proxy). Chạy `dotnet --version`; Expected: `8.x`. Nếu không cài được, ghi vào `docs/BUILD-STATUS.md` rằng backend chưa được build và dừng báo cáo "đã kiểm thử".
- [ ] **Step 2:** Viết test `FakeClock_Advance_MovesUtcNow` và `ExceptionMiddleware_ReturnsEnvelopeWithRequestId_NoStackTrace`.
- [ ] **Step 3:** Chạy `dotnet test tests/Shared.Tests`; Expected: FAIL (chưa có kiểu).
- [ ] **Step 4:** Tạo solution, các project, `SystemClock`, `FakeClock`, middleware lỗi thống nhất và request ID.
- [ ] **Step 5:** Chạy lại; Expected: PASS. Commit `feat: solution skeleton and shared kernel`.

### Task 2: Identity — tài khoản và hai scheme đăng nhập

**Files:** Create `src/Modules/Identity/{Domain,Persistence,Endpoints}/*`, migration đầu, `tests/Identity.Tests/AuthTests.cs`.

**Interfaces:**
- Consumes: `IClock`, `ApiEnvelope`.
- Produces: entities `AdminUser`, `Candidate` (`MemberId` nullable), `Session`; `IPasswordService { string Hash(string); bool Verify(string hash, string pw) }`; endpoint `POST /api/admin/auth/login`, `POST /api/thi/auth/login`, `POST /api/*/auth/logout`, `POST /api/*/auth/change-password`; scheme `"Admin"` và `"Candidate"`.

- [ ] **Step 1:** Test `Candidate_Cookie_Cannot_Call_Admin_Endpoint_Returns_403`, `Login_Locks_After_5_Failures_For_15_Minutes`, `FirstLogin_Forces_Password_Change`, `Locked_Account_Invalidates_Existing_Session`.
- [ ] **Step 2:** Chạy, Expected: FAIL.
- [ ] **Step 3:** Implement entities, DbContext schema `identity`, hasher của ASP.NET Core, cookie HttpOnly/SameSite/Secure-khi-HTTPS, antiforgery cho request ghi, rate limit đăng nhập.
- [ ] **Step 4:** Chạy test; Expected: PASS. Commit `feat(identity): admin and candidate auth`.

### Task 3: RBAC và quản trị tài khoản

**Files:** Create `src/Modules/Identity/Rbac/*`, endpoints `/api/admin/users`, `/api/admin/roles`, `/api/admin/candidates` (gồm import danh sách), `tests/Identity.Tests/RbacTests.cs`.

**Interfaces:**
- Produces: `[RequirePermission("cms.post.publish")]` attribute; `IPermissionChecker.Has(userId, permission, orgUnitId?)`; seed 6 vai trò admin theo spec mục 5; hằng số permission trong `Permissions` static class.

- [ ] **Step 1:** Test `Editor_Cannot_Publish_Returns_403`, `Cannot_Lock_Or_Delete_Last_SystemAdmin`, `Role_Change_Takes_Effect_On_Next_Request`, `Scoped_Role_Cannot_Access_Other_OrgUnit_Resource`.
- [ ] **Step 2:** Chạy, FAIL → implement policy handler đọc quyền từ database (cache ngắn, vô hiệu khi đổi vai trò) → PASS.
- [ ] **Step 3:** Commit `feat(identity): rbac and account management`.

### Task 4: Audit log

**Files:** Create `src/Shared/Audit/{AuditLog.cs,IAuditWriter.cs}`, `tests/Shared.Tests/AuditTests.cs`.

**Interfaces:** Produces `IAuditWriter.Write(string action, string entity, string entityId, object? detail)` ghi actor, thời gian UTC, request ID, không ghi trường mật khẩu/đáp án.

- [ ] **Step 1:** Test `Audit_Redacts_Password_And_CorrectAnswer_Fields`, `Publish_Writes_AuditRow_With_Actor`. FAIL → implement → PASS → commit `feat: audit log`.

### Task 5: Media

**Files:** Create `src/Modules/Cms/Media/*`, `tests/Cms.Tests/MediaTests.cs`.

**Interfaces:** Produces `IMediaStore.Save(Stream, string declaredName, Visibility) -> MediaAsset`, endpoint `POST /api/admin/media`, `GET /media/{id}` (công khai), `GET /api/files/{id}` (kiểm tra quyền); `GET /api/admin/media/{id}/usages`.

- [ ] **Step 1:** Test `Video_Mp4_Uploaded_In_Chunks_Reassembles_Identical_Bytes`, `Upload_Over_Configured_Limit_Rejected_With_Clear_Message`, `Rejects_Exe_Renamed_As_Jpg_By_Magic_Bytes`, `Stored_Name_Is_Random_Not_User_Supplied`, `Delete_Blocked_While_Referenced`, `Restricted_File_Requires_Permission`, `Image_Gets_Thumbnail`.
- [ ] **Step 2:** FAIL → implement (thư mục gốc cấu hình ngoài release, giới hạn kích thước, kiểm tra loại thật, thumbnail) → PASS → commit `feat(cms): media library`.

### Task 6: Bài viết, chuyên mục, thẻ

**Files:** Create `src/Modules/Cms/Posts/*`, `tests/Cms.Tests/PostWorkflowTests.cs`.

**Interfaces:** Produces `Post` với `Status` (Draft, PendingReview, Approved, Scheduled, Published, Archived), `RowVersion`; `PostService.Transition(postId, PostStatus to, actor)`; endpoints CRUD + `/transition`; sanitizer `IHtmlSanitizer`.

- [ ] **Step 1:** Test `Editor_Cannot_Skip_To_Published`, `Concurrent_Edit_With_Stale_RowVersion_Returns_409`, `Draft_Not_Visible_On_Public_Endpoint`, `Script_Tag_Stripped_From_Body`, `Soft_Delete_And_Restore`, `Duplicate_Slug_Rejected`.
- [ ] **Step 2:** FAIL → implement → PASS → commit `feat(cms): posts workflow`.

### Task 7: Menu, banner, văn bản, thư viện, trang tĩnh, cấu hình site, footer

**Files:** Create `src/Modules/Cms/{Menus,Banners,Documents,Albums,Pages,Settings}/*`, `tests/Cms.Tests/ContentCrudTests.cs`.

**Interfaces:** Produces CRUD + ẩn/hiện + khôi phục + sắp xếp cho từng nhóm trong bảng mục 5 của prompt gốc; `UrlValidator.IsSafe(string)` chỉ cho `http(s)`, đường dẫn nội bộ; `EmbedAllowList` cấu hình.

- [ ] **Step 1:** Test `Slug_Derived_From_Title_Without_Diacritics` ("Giới thiệu" → `gioi-thieu`), `Rename_Updates_Slug_And_Old_Url_Redirects_301`, `Duplicate_Slug_Rejected_With_Suggestion`, `Manual_Slug_Edit_Persists`, `Menu_Cycle_Rejected`, `javascript_Url_Rejected`, `Banner_Outside_Schedule_Not_Public`, `Reorder_Persists`, `Embed_From_Unlisted_Host_Rejected`, `Each_Group_Create_Update_Hide_Restore` (tham số hóa theo nhóm).
- [ ] **Step 2:** FAIL → implement → PASS → commit `feat(cms): menus, banners, documents, albums, pages, settings`.

### Task 8: Trình dựng khối trang chủ và trang tĩnh

**Files:** Create `src/Modules/Cms/Homepage/*`, `src/Modules/Cms/Homepage/Schemas/*.json`, `tests/Cms.Tests/HomepageBuilderTests.cs`.

**Interfaces:** Produces `HomepageRevision` (Draft/Published, `Version`), `SectionConfig` validate theo JSON schema theo loại khối (10 loại ở spec mục 6); `HomepageService.SaveDraft`, `.Publish(revisionId)`, `.Rollback(version)`; `GET /api/public/homepage` chỉ trả bản Published; phát tín hiệu `ICacheInvalidator.Invalidate("homepage")`.

- [ ] **Step 1:** Test `Draft_Change_Not_In_Public_Response`, `Publish_Swaps_Atomically_And_Invalidates_Cache`, `Rollback_Restores_Previous_Version`, `Empty_Section_Rejected`, `Unknown_Section_Type_Rejected`, `Stable_SectionId_Survives_Reorder`, `Page_Built_From_Sections_Same_Rules`.
- [ ] **Step 2:** FAIL → implement → PASS → commit `feat(cms): homepage builder`.

### Task 9: API công khai, tìm kiếm, liên hệ

**Files:** Create `src/Modules/Cms/Public/*`, `tests/Cms.Tests/PublicApiTests.cs`.

**Interfaces:** Produces `GET /api/public/{posts,categories,documents,albums,pages/{slug},menus,site}`, `GET /api/public/search?q=&type=&page=`, `POST /api/public/contact` (rate limit + honeypot).

- [ ] **Step 1:** Test `Search_Matches_Without_Diacritics` ("cong doan" tìm "Công đoàn"), `Search_Never_Returns_Draft_Or_Internal_Or_Exam_Content`, `Contact_Returns_Success_Only_After_Row_Saved`, `Contact_Rate_Limited`, `Document_Filter_By_Number_And_Date`.
- [ ] **Step 2:** FAIL → implement (collation hoặc cột chuẩn hóa không dấu) → PASS → commit `feat(cms): public api and search`.

### Task 10: Xuất bản theo lịch

**Files:** Create `src/Modules/Cms/Scheduling/PublishWorker.cs`, `tests/Cms.Tests/SchedulingTests.cs`.

**Interfaces:** Consumes `IClock`; Produces `PublishWorker : BackgroundService` quét mỗi 30 giây, trạng thái ở database.

- [ ] **Step 1:** Test `Scheduled_Post_Publishes_When_Clock_Passes`, `Worker_Restart_Does_Not_Double_Publish`. FAIL → implement → PASS → commit `feat(cms): scheduled publishing`.

### Task 11: Ngân hàng câu hỏi và import

**Files:** Create `src/Modules/Exam/Questions/*`, `tests/Exam.Tests/QuestionTests.cs`, `tests/Exam.Tests/ImportTests.cs`.

**Interfaces:** Produces `Question`, `QuestionVersion`, `Choice`, `QuestionType {Single, Multiple, TrueFalse}`; `QuestionValidator.Validate(...)`; `POST /api/admin/questions/import/preview` và `/import/commit` (commit nhận mã xem trước, ghi nguyên khối); template CSV/XLSX.

- [ ] **Step 1:** Test `Rejects_No_Correct_Choice`, `Rejects_Duplicate_Choices`, `Import_With_One_Bad_Row_Writes_Nothing_And_Reports_Row`, `Edit_Creates_New_Version`, `Unapproved_Question_Not_Usable_In_Exam`.
- [ ] **Step 2:** FAIL → implement → PASS → commit `feat(exam): question bank and import`.

### Task 12: Mẫu đề, kỳ thi, giao thi

**Files:** Create `src/Modules/Exam/Exams/*`, `tests/Exam.Tests/ExamSetupTests.cs`.

**Interfaces:** Produces `Exam` (`OpensAtUtc`, `ClosesAtUtc`, `DurationMinutes`, `MaxAttempts`, `ScoreRule`, `PassScore`, `ResultRelease`, `ShuffleQuestions`, `ShuffleChoices`), `ExamAssignment` (cá nhân/nhóm/đơn vị), `ExamService.Publish(examId)` kiểm tra đủ câu theo ma trận.

- [ ] **Step 1:** Test `Publish_Fails_When_Bank_Has_Fewer_Questions_Than_Matrix`, `Random_Draw_Has_No_Duplicate_Questions`, `Exam_With_Results_Cannot_Be_Deleted_Only_Archived`, `Assignment_By_Group_Expands_To_Members`.
- [ ] **Step 2:** FAIL → implement → PASS → commit `feat(exam): exams and assignments`.

### Task 13: Bắt đầu lượt thi và snapshot

**Files:** Create `src/Modules/Exam/Attempts/AttemptStarter.cs`, unique index lọc, `tests/Exam.Tests/StartAttemptTests.cs`.

**Interfaces:**
- Consumes: `IClock`, `ExamAssignment`.
- Produces: `AttemptStarter.Start(candidateId, examId) -> StartResult { Attempt Attempt, bool Resumed }`; `AttemptQuestion` lưu nội dung, thứ tự câu/lựa chọn, đáp án, điểm; `GET /api/thi/attempts/{id}` trả đề **không** có đáp án đúng.

- [ ] **Step 1:** Test `Not_Assigned_Returns_403`, `Before_Open_And_After_Close_Rejected`, `Over_Max_Attempts_Rejected`, `Two_Parallel_Starts_Create_One_Attempt` (50 task song song), `Deadline_Is_Min_Of_Start_Plus_Duration_And_Close`, `Reload_Keeps_Order`, `Payload_Contains_No_Correct_Flag_Or_Explanation` (quét JSON), `Editing_Bank_After_Start_Does_Not_Change_Attempt`.
- [ ] **Step 2:** FAIL → implement trong một transaction `Serializable`/unique index, bắt lỗi trùng → PASS → commit `feat(exam): start attempt with snapshot`.

### Task 14: Autosave, nộp bài, chấm điểm

**Files:** Create `src/Modules/Exam/Attempts/{AnswerSaver.cs,Submitter.cs,Grader.cs}`, `tests/Exam.Tests/{AutosaveTests.cs,SubmitTests.cs,GradingTests.cs}`.

**Interfaces:**
- Produces: `AnswerSaver.Save(attemptId, questionId, IReadOnlyList<Guid> choiceIds, long clientSeq) -> SaveResult`; `Submitter.Submit(attemptId) -> Result` idempotent; `Grader.Grade(Attempt) -> decimal`; `PUT /api/thi/attempts/{id}/answers/{questionId}`, `POST /api/thi/attempts/{id}/submit`.

- [ ] **Step 1:** Test `Stale_Seq_Does_Not_Overwrite_Newer_Answer`, `Save_After_Submit_Rejected`, `Save_After_Deadline_Rejected_Ignoring_Client_Timestamp`, `Submit_Twice_Returns_Same_Result_One_Row`, `Multiple_Choice_Partial_Selection_Scores_Zero`, `Blank_Answer_Scores_Zero`, `Rounding_And_PassScore_Boundaries`, `FinalScore_Highest_Latest_First` (3 ca), `Second_Tab_Gets_ReadOnly_Conflict`.
- [ ] **Step 2:** FAIL → implement (kiểm tra hạn ở API, transaction, version tăng dần) → PASS → commit `feat(exam): autosave, submit, grading`.

### Task 15: Đóng lượt hết hạn

**Files:** Create `src/Modules/Exam/Attempts/ExpiryWorker.cs`, `tests/Exam.Tests/ExpiryTests.cs`.

**Interfaces:** Produces `ExpiryWorker : BackgroundService` quét lượt quá hạn mỗi 10 giây, chấm và đóng bằng `Submitter`.

- [ ] **Step 1:** Test `Expired_Attempt_Closed_And_Graded_Without_Browser`, `Worker_Restart_Mid_Exam_Does_Not_Extend_Deadline`, `Expired_Then_Submit_Is_Idempotent_Noop`.
- [ ] **Step 2:** FAIL → implement → PASS → commit `feat(exam): expiry worker`.

### Task 16: Kết quả, báo cáo, xuất file

**Files:** Create `src/Modules/Exam/Reports/*`, `src/Modules/Exam/Results/*`, `tests/Exam.Tests/{ResultVisibilityTests.cs,ReportTests.cs,ExportTests.cs}`.

**Interfaces:** Produces `GET /api/thi/attempts/{id}/result` theo `ResultRelease`; `GET /api/admin/reports/exams/{id}?unit=&group=`; `POST /api/admin/reports/exams/{id}/export?format=csv|xlsx`; `GradeRevision` khi chấm lại; `CsvSanitizer.Escape(string)`.

- [ ] **Step 1:** Test `Result_Hidden_Before_Release`, `Detail_And_Explanation_Need_Separate_Permission`, `Candidate_Cannot_Read_Other_Result_By_Id`, `Export_Escapes_Formula_Prefixes` (`=`, `+`, `-`, `@`, tab), `Export_Writes_AuditRow`, `Report_Distinguishes_People_From_Attempts`, `Regrade_Keeps_Original_And_Reason`.
- [ ] **Step 2:** FAIL → implement → PASS → commit `feat(exam): results and reports`.

### Task 17: Frontend nền tảng và trang chủ công khai

**Files:** Create `web/` (Next.js), `web/src/lib/api.ts`, `web/src/styles/tokens.css`, `web/src/app/(public)/layout.tsx`, `web/src/app/(public)/page.tsx`, `web/src/components/blocks/*`, `web/tests/api.test.ts`.

**Interfaces:** Consumes `GET /api/public/homepage`, `/menus`, `/site`. Produces `api<T>(path, init?) -> Promise<T>` bóc envelope, ném `ApiClientError` khi `success=false`; một component render cho mỗi loại khối.

- [ ] **Step 1:** Test `api_unwraps_envelope_to_data`, `api_throws_on_error_envelope`, `block_renderer_handles_all_10_types`, `empty_block_renders_nothing`.
- [ ] **Step 2:** FAIL → dựng tokens, layout (thanh tiện ích, header, menu, footer), trang chủ theo khối, cache theo thẻ `homepage` → PASS (`npx vitest run`) → commit `feat(web): public shell and homepage`.

### Task 18: Các trang công khai

**Files:** Create `web/src/app/(public)/{tin-tuc,van-ban,thu-vien,tim-kiem,lien-he,[...slug]}/*`, `web/src/app/not-found.tsx`, `web/src/app/error.tsx`, `web/tests/e2e/public.spec.ts`.

- [ ] **Step 1:** Playwright: `article_page_shows_breadcrumb_date_attachments_print`, `category_filters_and_paginates`, `search_empty_state`, `contact_shows_success_only_after_api_ok`, `no_horizontal_scroll_at_360_390_768_1024_1440`, `direct_refresh_of_dynamic_url_works`.
- [ ] **Step 2:** FAIL → implement → PASS → commit `feat(web): public pages`.

### Task 19: Admin — khung, CMS và trình dựng khối

**Files:** Create `web/src/app/admin/**`, `web/src/components/admin/{BlockBuilder.tsx,RichTextEditor.tsx,MediaPicker.tsx}`, `web/tests/e2e/admin-cms.spec.ts`.

- [ ] **Step 1:** Playwright: `insert_image_from_computer_into_article_body_and_shows_publicly`, `paste_and_drag_drop_image`, `insert_uploaded_video_and_youtube_embed`, `rename_menu_item_changes_url_and_old_url_redirects`, `login_required_redirects`, `editor_cannot_see_publish_button_and_api_403`, `block_reorder_by_drag_and_by_buttons`, `preview_three_viewports_does_not_change_public`, `publish_then_public_updates_without_deploy`, `rollback_restores`, `unsaved_changes_warning`, `stale_version_shows_conflict_dialog`.
- [ ] **Step 2:** FAIL → implement (tiếng Việt, sidebar, bộ lọc, phân trang, thùng rác, autosave nháp) → PASS → commit `feat(web): admin cms`.

### Task 20: Admin — thi, tài khoản, báo cáo

**Files:** Create `web/src/app/admin/{cau-hoi,de-thi,ky-thi,tai-khoan,bao-cao,nhat-ky}/**`, `web/tests/e2e/admin-exam.spec.ts`.

- [ ] **Step 1:** Playwright: `import_preview_shows_row_errors_and_blocks_commit`, `exam_publish_blocked_when_bank_short`, `report_export_downloads_csv`, `dashboard_numbers_match_database_seed`.
- [ ] **Step 2:** FAIL → implement → PASS → commit `feat(web): admin exam and reports`.

### Task 21: Cổng thi `/thi`

**Files:** Create `web/src/app/thi/**`, `web/src/components/exam/{AttemptRunner.tsx,SaveStatus.tsx}`, `web/tests/e2e/exam.spec.ts`.

- [ ] **Step 1:** Playwright: `unassigned_exam_url_blocked`, `autosave_shows_saving_saved_error`, `reload_resumes_same_order_and_answers`, `offline_then_online_marks_unsynced_then_syncs`, `timer_uses_server_deadline_not_device_clock`, `submit_confirm_warns_blank_count`, `after_deadline_ui_locked`, `mobile_timer_does_not_cover_question`.
- [ ] **Step 2:** FAIL → implement (hàng đợi cục bộ chỉ lưu id câu/lựa chọn, xóa khi nộp, không lưu đáp án chuẩn) → PASS → commit `feat(web): exam portal`.

### Task 22: Import dữ liệu cũ và redirect

**Files:** Create `tools/Importer/*`, `src/Modules/Cms/Redirects/*`, `tests/Cms.Tests/ImporterTests.cs`, `docs/migration.md`.

**Interfaces:** Produces `Importer run --source <dir|url-list> --dry-run`, khóa nguồn chống trùng, báo cáo đối soát CSV; bảng `Redirect(OldPath, NewPath, 301)`.

- [ ] **Step 1:** Test `Rerun_Does_Not_Duplicate`, `Broken_Image_Logged_Not_Fatal`, `Old_Url_Redirects_301_Not_Home`, `Reconciliation_Counts_Match_Fixture`.
- [ ] **Step 2:** FAIL → implement (chạy trên dữ liệu mẫu có nhãn; chưa chạy dữ liệu thật vì chưa có nguồn) → PASS → commit `feat: legacy importer and redirects`.

### Task 23: Triển khai, nghiệm thu, đo tải

**Files:** Create `deploy/{web.config,install-service.ps1,backup.ps1,restore.ps1}`, `docs/{deploy-iis.md,editor-guide.md,exam-admin-guide.md,rollback.md,acceptance-matrix.md}`, `tests/Load/exam-load.js`.

- [ ] **Step 1:** Script tải (k6) mô phỏng N thí sinh bắt đầu, autosave và nộp; N là tham số, mặc định ghi rõ "kịch bản giả định 3000, chưa đo".
- [ ] **Step 2:** Chạy toàn bộ `dotnet test`, `npx vitest run`, Playwright; điền `acceptance-matrix.md` bằng kết quả thật, ô chưa chạy ghi "chưa kiểm chứng".
- [ ] **Step 3:** Viết tài liệu IIS (reverse proxy, TLS, quyền thư mục media, dịch vụ tự khởi động, migration, backup/restore, rollback tương thích schema, không phát hành trong giờ thi). Commit `docs: deployment, acceptance and load test`.
