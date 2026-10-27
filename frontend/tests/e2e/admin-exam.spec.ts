import { expect, test } from '@playwright/test'
import { AdminApi, loginAdminUi, unique } from './helpers'

let admin: AdminApi
test.beforeAll(async () => {
  admin = await AdminApi.create()
})
test.afterAll(async () => admin.dispose())

test('import_preview_shows_row_errors_and_blocks_commit', async ({ page }) => {
  await loginAdminUi(page)
  const topic = unique('NhapCSV').replace(' ', '-')
  const csv = 'chu_de,muc_do,loai,noi_dung,dap_an_a,dap_an_b,dap_an_c,dap_an_d,dap_an_dung,giai_thich,diem\n'
    + `${topic},de,mot_dap_an,Câu đúng?,A,B,C,D,A,,1\n`
    + `${topic},de,mot_dap_an,Câu thiếu đáp án đúng?,A,B,,,,,1\n`
  await page.goto('/admin/cau-hoi/nhap')
  await page.getByLabel('Tệp câu hỏi (CSV hoặc XLSX)').setInputFiles({ name: 'cau-hoi.csv', mimeType: 'text/csv', buffer: Buffer.from(csv) })
  await page.getByRole('button', { name: 'Xem trước' }).click()

  const errors = page.getByRole('region', { name: 'Lỗi theo dòng' })
  await expect(errors).toContainText('Dòng 3')
  await expect(page.getByRole('button', { name: 'Ghi vào ngân hàng' })).toBeDisabled()
  const list = await admin.call('GET', `/api/admin/questions?topic=${encodeURIComponent(topic)}`)
  expect(list.total).toBe(0)
})

test('exam_publish_blocked_when_bank_short', async ({ page }) => {
  await loginAdminUi(page)
  const topic = unique('Thieu').replace(' ', '-')
  const q = await admin.call('POST', '/api/admin/questions', {
    type: 'Single', topic, difficulty: 'Easy', content: 'Câu duy nhất', points: 1,
    choices: [{ content: 'Đúng', isCorrect: true }, { content: 'Sai', isCorrect: false }],
  })
  await admin.call('POST', `/api/admin/questions/${q.id}/approve`)
  const now = Date.now()
  const exam = await admin.call('POST', '/api/admin/exams', {
    title: unique('Kỳ thi thiếu câu'), opensAtUtc: new Date(now).toISOString(), closesAtUtc: new Date(now + 86400000).toISOString(),
    durationMinutes: 20, maxAttempts: 1, scoreRule: 'Highest', passScore: 5, resultRelease: 'Immediately', questionSource: 'Random',
  })
  await admin.call('PUT', `/api/admin/exams/${exam.id}/matrix`, { rules: [{ topic, difficulty: 'Easy', count: 5 }] })

  await page.goto(`/admin/ky-thi/${exam.id}`)
  await page.getByRole('button', { name: 'Công bố' }).click()
  await expect(page.getByRole('alert').filter({ hasText: 'chỉ có 1' })).toBeVisible()
  await expect(page.getByText('Trạng thái: Nháp')).toBeVisible()
})

test('report_export_downloads_csv', async ({ page }) => {
  await loginAdminUi(page)
  const topic = unique('BaoCao').replace(' ', '-')
  const q = await admin.call('POST', '/api/admin/questions', {
    type: 'Single', topic, difficulty: 'Easy', content: 'Câu báo cáo', points: 1,
    choices: [{ content: 'Đúng', isCorrect: true }, { content: 'Sai', isCorrect: false }],
  })
  await admin.call('POST', `/api/admin/questions/${q.id}/approve`)
  const code = unique('bc').replace(' ', '-')
  const cand = await admin.call('POST', '/api/admin/candidates', { code, displayName: 'Thí sinh báo cáo', password: 'Start12345a' })
  const now = Date.now()
  const exam = await admin.call('POST', '/api/admin/exams', {
    title: unique('Kỳ thi báo cáo'), opensAtUtc: new Date(now - 60000).toISOString(), closesAtUtc: new Date(now + 86400000).toISOString(),
    durationMinutes: 20, maxAttempts: 1, scoreRule: 'Highest', passScore: 5, resultRelease: 'Immediately', questionSource: 'Manual',
  })
  await admin.call('PUT', `/api/admin/exams/${exam.id}/questions`, { questionIds: [q.id] })
  await admin.call('PUT', `/api/admin/exams/${exam.id}/assignments`, { add: [{ targetType: 'Candidate', targetId: cand.candidate.id }] })
  await admin.call('POST', `/api/admin/exams/${exam.id}/publish`)

  await page.goto(`/admin/bao-cao/${exam.id}`)
  await expect(page.getByText('Được giao')).toBeVisible()
  const download = page.waitForEvent('download')
  await page.getByRole('button', { name: 'Xuất CSV' }).click()
  const file = await download
  expect(file.suggestedFilename()).toMatch(/\.csv$/)
  const text = (await (await file.createReadStream()).toArray()).map(String).join('')
  expect(text).toContain('Mã thí sinh')
  expect(text).toContain(code)
})

test('dashboard_numbers_match_database_seed', async ({ page }) => {
  await loginAdminUi(page)
  const d = await admin.call('GET', '/api/admin/dashboard')
  const pending = await admin.call('GET', '/api/admin/posts?status=PendingReview&pageSize=1')
  expect(d.pendingReviewPosts).toBe(pending.total)
  await page.goto('/admin')
  await expect(page.getByTestId('stat-publishedPosts')).toHaveText(String(d.publishedPosts))
  await expect(page.getByTestId('stat-pendingReviewPosts')).toHaveText(String(d.pendingReviewPosts))
  await expect(page.getByTestId('stat-candidates')).toHaveText(String(d.candidates))
  await expect(page.getByTestId('stat-approvedQuestions')).toHaveText(String(d.approvedQuestions))
})
