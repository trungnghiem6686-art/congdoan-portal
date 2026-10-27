# Quyết định trong quá trình thực hiện

Các quyết định (ruling) đưa ra khi kế hoạch hoặc spec chưa nói rõ, theo thứ tự thực hiện. Mỗi dòng gồm: quyết định, lý do, và cái giá nếu quyết định sai. Cuối tệp là các điểm nhỏ đã thấy nhưng chưa sửa.

## Quyết định

- Ruling: Layout follows plan's Path mapping (backend/CongDoan.{Domain,Application,Infrastructure,Api}, backend/tests/CongDoan.<M>.Tests, frontend/) — spec §3 is binding over the plan's src/ file tree — cost if wrong: directory moves.
- Ruling: Commit trailers use the current session's attribution, not the plan's old session lines — harness attribution supersedes a stale repo doc — cost if wrong: trailer text only.
- Ruling: Integration tests needing SQL Server read connection string from env CONGDOAN_TEST_SQL (default localhost docker) — secrets out of source — cost if wrong: env var rename.
- Task 2: Ruling: built-in ASP.NET Core RateLimiter instead of AspNetCoreRateLimit — same behavior, no extra dependency, supported in .NET 8 — cost if wrong: swap package.
- Task 2: Ruling: Application layer references EF Core abstractions (DbSet via I<Module>DbContext interfaces) instead of hand-written repositories — keeps 4-layer split with far less code — cost if wrong: introduce repositories later.
- Task 2: Ruling: one DbContext per module (own schema + own migrations history table), same database — enforces "no cross-module tables" and lets new modules plug in — cost if wrong: cross-module transactions need explicit coordination.
- Task 2: Ruling: server-side Session table; cookie carries session id and is re-validated each request — required for lock/revoke to end live sessions — cost if wrong: one indexed lookup per request.
- Task 2: Ruling: CSRF = ASP.NET antiforgery cookie + X-XSRF-TOKEN header on every unsafe /api request (login included); GET /api/antiforgery issues the token — cost if wrong: frontend must fetch the token.
- Task 3: Ruling: no Permission table — permission catalog is code (core list + IPermissionProvider per module), RolePermission stores codes — lets modules add permissions without migrations in core — cost if wrong: add a table later.
- Task 3: Ruling: Editor_Cannot_Publish tested against a test-only probe controller (added via ApplicationPart in the test host) because post endpoints arrive in Task 6; Task 6 re-tests on real endpoints — cost if wrong: none.
- Task 3: Ruling: scoped access to another org unit's resource returns 403 (not 404) — clearer for admins; resource existence is not secret for admins with the permission — cost if wrong: status code change.
- Task 4: Ruling: audit rows saved by a separate AuditDbContext right after the business save (not one shared transaction) — module isolation; a crash between the two saves can lose one audit row — cost if wrong: share a DbTransaction across contexts.
- Task 4: Ruling: Publish_Writes_AuditRow_With_Actor asserted on account lock until publish exists; Task 6 adds the real publish assertion — cost if wrong: none.
- Task 5: Ruling: SkiaSharp (MIT) for decode/thumbnail instead of ImageSharp (split commercial licence) — avoids licence risk for the organisation — cost if wrong: swap IImageProcessor implementation.
- Task 5: Ruling: SVG uploads not accepted (script-capable); restricted files need cms.media.manage until exam/member modules define their own file permissions — cost if wrong: add a per-owner permission check.
- Task 6: Ruling: sanitizer interface named IContentSanitizer (plan: IHtmlSanitizer) to avoid clashing with Ganss.Xss.IHtmlSanitizer; HtmlSanitizer 9.2.1039 (8.1.870 has advisory GHSA-j92c-7v7g-gj3f) — cost if wrong: rename.
- Task 6: Ruling: publishers may publish directly from Draft/Pending/Approved; editors without review rights only edit their own drafts and can only submit for review — matches spec roles while keeping the publisher path short — cost if wrong: tighten PostWorkflow map.
- Task 6: Ruling: added permission cms.category.manage for categories/tags — none of the core codes fit — cost if wrong: none.
- Task 7: Ruling: URL scheme — menu items own root paths "/{slug}" (render their target: category, page, post, url or a section of children); posts /tin-tuc/{slug}, categories /chuyen-muc/{slug}, pages /trang/{slug}, documents /van-ban/{slug}, albums /thu-vien/{slug}; reserved root segments cannot be menu slugs — makes "rename menu → URL changes, old URL 301s" literal — cost if wrong: path prefixes change in PublicPaths.
- Task 7: Ruling: Slug/UrlValidator tests passed on first run because the code was written in Tasks 5/6 ahead of its test (TDD deviation, recorded) — tests now cover them — cost if wrong: none.
- Task 7: Ruling: site settings are typed JSON per key (site, contact, footer, social, utility, seo) with "MẪU" defaults; unknown keys 404 — spec forbids inventing contact data — cost if wrong: add keys.
- Task 8: Ruling: block configs validated by typed C# records per block type (parse → range check → re-serialise) instead of JSON Schema files — same guarantees (unknown fields dropped, script impossible) without a schema library — cost if wrong: add schema files for the admin UI.
- Task 8: Ruling: cache invalidation is emitted from CmsDbContext.SaveChangesAsync by entity type (tags posts/menus/banners/documents/albums/pages/site/homepage) and sent to Frontend:RevalidateUrl; inside transactions tags flush after commit — cost if wrong: stale page until frontend timer.
- Task 8: Ruling: rollback publishes a copy of the old version as a new version number (history never rewritten) — cost if wrong: none.
- Task 9: Ruling: accent-insensitive search uses stored SearchText (Slug.SearchText of title/summary/body) with AND over terms, not SQL collation (Vietnamese "đ" is not folded by _AI collations) — cost if wrong: switch to full-text index later for large volumes.
- Task 9: Ruling: honeypot-filled contact returns 400 spam_detected (not fake success) so success always means a stored row — cost if wrong: bots learn the field.
- Task 11: Ruling: application/domain namespaces are "Exams" (plan: Exam) so the entity can be called Exam without namespace/type clashes — cost if wrong: rename.
- Task 11: Ruling: import preview is persisted (QuestionImport row with validated payload, 2h lifetime, creator-only) and commit replays it in one transaction; a preview with any error can never be committed — cost if wrong: none.
- Task 11: Ruling: editing a question creates a new version and resets it to Draft (needs re-approval) — prevents unreviewed changes reaching exams — cost if wrong: approvers re-approve small edits.
- Task 12: Ruling: no ExamVersion table — attempts snapshot every question, order and key at start, and exam settings that matter (questions, matrix, duration) are locked once any attempt exists — cost if wrong: add versioned exam settings.
- Task 12: Ruling: Exam_With_Results test seeds a finished Attempt row directly (attempt API is Task 13) — cost if wrong: none.
- Task 12: Ruling: random draw processes rules smallest-pool first so overlapping rules ("Easy x4" + "any x6") never draw duplicates or fail when a valid draw exists in nested cases — cost if wrong: general overlaps may need a matching algorithm.
- Task 12: Ruling: score scale is 10 points (pass mark 0–10), rounding decimals configurable per exam — cost if wrong: add a scale setting.
- Task 13: Ruling: start races settled by the two unique indexes (one InProgress per exam+candidate, unique attemptNo) at ReadCommitted, catching the violation and returning the winner — avoids Serializable lock contention at 3000 users — cost if wrong: a loser gets 409 attempt_conflict and retries.
- Task 13: Ruling: expired in-progress attempts are finished through IAttemptFinisher; until Task 14 a placeholder returns 409 previous_attempt_finishing — replaced in Task 14.
- Task 14: Ruling: results live on the Attempt row (Points, Score, Passed, CorrectCount) plus per-answer AwardedPoints, instead of a separate Result table — one row per attempt makes "one result" structural — cost if wrong: add a view/table for reporting.
- Task 14: Ruling: multi-tab = writer lease (45 s, heartbeat) with explicit takeover; other tabs read-only ("another_tab_active"/"read_only"), the taken-over tab gets "writer_superseded" — cost if wrong: UX tweak only.
- Task 14: Ruling: Application references EF Core Relational (provider-agnostic) for ExecuteUpdate conditional writes — cost if wrong: none.
- Task 16: Ruling: regrade is a manual score override on the 10-point scale with mandatory reason; original kept in GradeRevision — re-keying questions inside a frozen snapshot would rewrite history — cost if wrong: add a "re-key and recompute" action.
- Task 16: Ruling: pass rate denominator = people with at least one attempt (stated in the payload as passRateDenominator); assigned/attempted/not-attempted people and attempt counts reported separately — cost if wrong: change denominator label.
- Task 16: Ruling: XLSX cells are written as typed text/numbers (never formulas), so no apostrophe prefix there; CSV cells starting with = + - @ TAB CR get an apostrophe — cost if wrong: none.
- Task 17: Ruling: Next.js 16.4.3 (not 16.4.0: GHSA-2464-fhr4-w9xw RCE + 2 others), zod 4.6.7 (GHSA-mrhx-c7xc-m3w5), postcss-selector-parser overridden to 6.1.5; npm audit = 0 — cost if wrong: none.
- Task 17: Ruling: npm >= 11 required (npm 10.9 crashes on vitest's optional peer graph); Vitest 5 without @vitejs/plugin-react (needs Vite 8), JSX via esbuild — cost if wrong: dev tooling only.
- Task 17: Ruling: Be Vietnam Pro self-hosted via @fontsource (no runtime call to Google) — cost if wrong: none.
- Task 17: Ruling: public pages render per request (force-dynamic) while data uses fetch cache with tags; backend calls /next-api/revalidate (secret) → revalidateTag(tag,{expire:0}) so a publish is visible on the next request with no rebuild — cost if wrong: switch to 'max' for stale-while-revalidate.
- Task 18: Ruling: old-URL redirects use Next permanentRedirect (HTTP 308, permanent like 301, method-preserving) from the catch-all page; test accepts 301 or 308 — a literal 301 would need a per-request proxy lookup — cost if wrong: add proxy-level 301 for redirect paths.
- Task 19: Ruling: rich-text editor is a small contentEditable component (toolbar, image/video upload from computer, paste and drag-drop, captions, YouTube/Vimeo embed) instead of adding an editor library outside the agreed stack; the backend sanitiser is the authority — cost if wrong: swap in TipTap behind the same props.
- Task 19: Ruling: added GET /api/admin/auth/permissions so the UI hides actions the user lacks (API still enforces) — test Me_Permissions_Lists_Effective_Codes RED→GREEN.
- Task 20: Ruling: added GET /api/admin/dashboard (DashboardService reads each module through its own context, no cross-module joins) — test Dashboard_Counts_Match_Database RED→GREEN; E2E compares the UI to the API and the API to list totals.
- Task 20: Ruling: report export is a POST (audited) fetched as a blob and saved client-side; CSV is UTF-8 with BOM for Excel — cost if wrong: none.
- Task 21: Ruling: API DateTimes lacked 'Z' (Unspecified kind from SQL Server) so UTC+7 browsers saw exams expired — added global UtcDateTimeConverter with backend test Timestamps_Read_From_Database_Are_Serialized_As_Utc — cost if wrong: one converter to remove
- Task 22: Ruling: no legacy export exists, so the importer reads a documented neutral format (manifest.json + files; docs/migration.md) — a converter from the real export to it is the remaining step — cost if wrong: converter work once the real source is known.
- Task 22: Ruling: importer core lives in Application (CongDoan.Application.Cms.Import.LegacyImporter, tested through ApiFactory in Cms.Tests); backend/tools/CongDoan.Importer is a thin host — paths differ from the brief (tools/Importer, src/Modules/Cms/Redirects) to match the repo layout; the Redirect table/service already existed from Task 13 and was reused.
- Task 22: Ruling: re-run skips posts whose SourceKey exists (no update-in-place); categories matched by slug (Category has no SourceKey) — cost if wrong: corrected source data needs the post deleted before re-import.
- Task 22: Ruling: old URLs with query strings cannot be stored (Normalize strips the query) — reported with kind=redirect and left to an IIS rewrite rule — cost if wrong: query-style legacy links 404 until rules are added.
- Task 23: Ruling: behind IIS every request reached Kestrel as http from 127.0.0.1 (cookies not Secure, login rate limit shared by all users) — added ForwardedHeaders (XFF + XFP, ForwardLimit 1, loopback proxies by default, ReverseProxy:KnownProxies configurable); test Behind_Https_Proxy_Session_Cookie_Is_Secure RED→GREEN — cost if wrong: misconfigured remote proxy needs KnownProxies.
- Task 23: Ruling: added UseWindowsService and optional Serilog rolling file sink (Logging:File:Path, daily, 30 files, 50 MB) — spec §3 names Serilog and Windows Service; no test (hosting glue), verified log file written and free of "password=".
- Task 23: Ruling: Next.js runs as a startup Scheduled Task with restart-on-failure instead of a third-party service wrapper (NSSM/WinSW) — no extra binary to vet — cost if wrong: switch to a wrapper if the org forbids scheduled tasks.
- Task 23: Ruling: plan never scheduled sitemap/robots/canonical but requirements §11 SEO row demands them — added /api/public/sitemap (SitemapTests.Sitemap_Lists_Public_Content_Only RED→GREEN), sitemap.ts/robots.ts/metadataBase/canonical (E2E sitemap_robots_and_canonical RED→GREEN).
- Task 23: Ruling: redirects had an API but no admin page while migration.md relied on one — added /admin/chuyen-huong (E2E manage_redirects_from_admin RED→GREEN).
- Task 23: Ruling: k6 binary download was denied by the environment's permission policy — script written and syntax-checked, load figures recorded as "chưa đo" — cost: load capacity unknown until run on staging.
- Final: Ruling: install-service.ps1 granted folder rights only to the API account, so the web task (LOCAL SERVICE) could not read its release or write logs\web.log and would fail to start — fixed by granting RX on WebDir and M on logs; no RED test possible (no PowerShell/Windows here) — cost if wrong: web task fails on first install, visible in Task Scheduler history.

## Điểm nhỏ phát hiện khi rà soát (đã sửa sau đó, xem mục cuối)

- Importer catches only AppException per post; an unreadable image file (IOException/access denied) aborts the run instead of being reported.
- first-save insert path in AttemptRunner.SaveAsync does not recheck Status=InProgress in SQL; a save racing with finish can leave an ungraded answer row (score and result unaffected).
- /admin/xem-truoc builds the preview API path from the query string without checking it is a GUID (GET only, uses the admin's own session).
- logs\web.log (Next.js stdout) is not rotated; documented in deploy-iis.md.

## Kết quả từng task

- Task 1: complete (commits a09611d..f66e6fe, tests: dotnet test backend/tests/CongDoan.Shared.Tests → Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3, Duration: 34 ms - CongDoan.Shared.Tests.dll (net8.0))
- Task 2: complete (commits f66e6fe..de5f99b, tests: dotnet test backend/CongDoan.sln → Passed!  - Failed:     0, Passed:     7, Skipped:     0, Total:     7, Duration: 1 s - CongDoan.Identity.Tests.dll (net8.0))
- Task 3: complete (commits de5f99b..7296d8c, tests: dotnet test backend/CongDoan.sln → Passed!  - Failed:     0, Passed:    13, Skipped:     0, Total:    13, Duration: 2 s - CongDoan.Identity.Tests.dll (net8.0))
- Task 4: complete (commits 7296d8c..564da26, tests: dotnet test backend/CongDoan.sln → Passed!  - Failed:     0, Passed:    13, Skipped:     0, Total:    13, Duration: 2 s - CongDoan.Identity.Tests.dll (net8.0))
- Task 5: complete (commits 564da26..e5ad6f7, tests: dotnet test backend/CongDoan.sln → Passed!  - Failed:     0, Passed:     7, Skipped:     0, Total:     7, Duration: 2 s - CongDoan.Cms.Tests.dll (net8.0))
- Task 6: complete (commits e5ad6f7..f0629d0, tests: dotnet test backend/CongDoan.sln → Passed!  - Failed:     0, Passed:    13, Skipped:     0, Total:    13, Duration: 2 s - CongDoan.Identity.Tests.dll (net8.0))
- Task 7: complete (commits f0629d0..44a8d27, tests: dotnet test backend/CongDoan.sln → Passed!  - Failed:     0, Passed:    43, Skipped:     0, Total:    43, Duration: 19 s - CongDoan.Cms.Tests.dll (net8.0))
- Task 8: complete (commits 44a8d27..5ed03c1, tests: dotnet test backend/CongDoan.sln → Passed!  - Failed:     0, Passed:    53, Skipped:     0, Total:    53, Duration: 21 s - CongDoan.Cms.Tests.dll (net8.0))
- Task 9: complete (commits 5ed03c1..660a512, tests: dotnet test backend/CongDoan.sln → Passed!  - Failed:     0, Passed:    59, Skipped:     0, Total:    59, Duration: 38 s - CongDoan.Cms.Tests.dll (net8.0))
- Task 10: complete (commits 660a512..7f9bf75, tests: dotnet test backend/CongDoan.sln → Passed!  - Failed:     0, Passed:    61, Skipped:     0, Total:    61, Duration: 23 s - CongDoan.Cms.Tests.dll (net8.0))
- Task 11: complete (commits 7f9bf75..328e1e2, tests: dotnet test backend/CongDoan.sln → Passed!  - Failed:     0, Passed:    61, Skipped:     0, Total:    61, Duration: 25 s - CongDoan.Cms.Tests.dll (net8.0))
- Task 12: complete (commits 328e1e2..ecf6aa9, tests: dotnet test backend/CongDoan.sln → Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14, Duration: 4 s - CongDoan.Exam.Tests.dll (net8.0))
- Task 13: complete (commits ecf6aa9..8b27f9e, tests: dotnet test backend/CongDoan.sln → Passed!  - Failed:     0, Passed:    61, Skipped:     0, Total:    61, Duration: 40 s - CongDoan.Cms.Tests.dll (net8.0))
- Task 14: complete (commits 8b27f9e..4262a35, tests: dotnet test backend/CongDoan.sln → Passed!  - Failed:     0, Passed:    40, Skipped:     0, Total:    40, Duration: 24 s - CongDoan.Exam.Tests.dll (net8.0))
- Task 15: complete (commits 4262a35..c46f10b, tests: dotnet test backend/CongDoan.sln → Passed!  - Failed:     0, Passed:    43, Skipped:     0, Total:    43, Duration: 24 s - CongDoan.Exam.Tests.dll (net8.0))
- Task 16: complete (commits c46f10b..9be3dd2, tests: dotnet test backend/CongDoan.sln → Passed!  - Failed:     0, Passed:    58, Skipped:     0, Total:    58, Duration: 35 s - CongDoan.Exam.Tests.dll (net8.0))
- Task 17: complete (commits 9be3dd2..cac5939, tests: bash -c 'cd frontend && npx vitest run && npx tsc --noEmit && npx eslint .' →    Duration  1.37s (environment 63%, transform 13%, setup 10%, tests 8%, import 7%))
- Task 18: complete (commits cac5939..07d5fef, tests: bash -c 'cd frontend && npx vitest run && npx playwright test' →   11 passed (8.9s))
- Task 19: complete (commits 07d5fef..9541385, tests: bash -c 'cd backend && dotnet test CongDoan.sln >/dev/null && cd ../frontend && npx vitest run && npx playwright test' →   23 passed (27.6s))
- Task 20: complete (commits 9541385..a693086, tests: bash -c 'cd backend && dotnet test CongDoan.sln >/dev/null && cd ../frontend && npx vitest run && npx playwright test' →   27 passed (33.9s))
- Task 21: complete (commits a693086..11d4b83, tests: bash -c 'cd backend && dotnet test CongDoan.sln >/dev/null && cd ../frontend && npx vitest run && npx playwright test' →   35 passed (50.9s))
- Task 22: complete (commits 11d4b83..68739bb, tests: bash -c 'cd backend && dotnet test CongDoan.sln' → Passed!  - Failed:     0, Passed:    59, Skipped:     0, Total:    59, Duration: 49 s - CongDoan.Exam.Tests.dll (net8.0))
- Task 23: complete (commits 68739bb..1d8fb27, tests: bash -c 'cd backend && dotnet test CongDoan.sln >/dev/null && cd ../frontend && npx vitest run >/dev/null && npx playwright test' →   37 passed (57.7s))

## Sửa sau khi gộp vào main

- Importer: lỗi đọc tệp ảnh (IOException, thiếu quyền) giờ được ghi vào báo cáo, bài vẫn được nhập — test `ImporterTests.Unreadable_Image_File_Is_Reported_Not_Fatal` RED→GREEN.
- Lưu bài chạy đua với nộp bài: nặng hơn đánh giá ban đầu. Test `SaveSubmitRaceTests.First_Saves_Racing_Submit_Leave_No_Ungraded_Answers` (12 bài × 8 câu lưu cùng lúc với nộp) cho thấy 40–65 câu đã báo "đã lưu" nhưng không được chấm, và 4/12 bài đã nộp không bao giờ được chấm (cập nhật WriterSeenUtc sau khi nộp làm đổi RowVersion, bước chấm tưởng đã có người chấm nên bỏ qua). Sửa: mỗi lần lưu khóa dòng bài làm trong một giao dịch trước khi ghi; các cập nhật phụ (WriterSeenUtc, đếm chuyển tab) chỉ chạy khi bài còn đang làm; bước chấm gặp xung đột thì đọc lại và chấm lại. Test đạt 3/3 lần chạy, 60/60 test phần thi đạt.
- Trang xem trước: chỉ chấp nhận `homepage` hoặc `page:<GUID>` — test `tests/preview.test.ts` RED→GREEN.
- Log web: tệp khởi chạy Next.js chuyển sang PowerShell, ghi `web-YYYYMMDD.log` và xóa tệp cũ hơn 30 ngày. Không có test (môi trường này không có PowerShell/Windows) — chưa kiểm chứng.
