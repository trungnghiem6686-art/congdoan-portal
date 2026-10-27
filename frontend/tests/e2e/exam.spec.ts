import { expect, test, type Page } from '@playwright/test'
import { AdminApi, examForCandidate, loginCandidateUi } from './helpers'

let admin: AdminApi
test.beforeAll(async () => {
  admin = await AdminApi.create()
})
test.afterAll(async () => admin.dispose())

async function startExam(page: Page, examId: string) {
  await page.goto(`/thi/ky-thi/${examId}`)
  await page.getByRole('button', { name: /Bắt đầu làm bài|Tiếp tục làm bài/ }).click()
  await page.waitForURL(/\/thi\/bai-lam\//)
  await expect(page.getByRole('heading', { name: /Câu 1/ })).toBeVisible()
}

const status = (page: Page) => page.getByTestId('save-status')
const choice = (page: Page, text: string) => page.getByRole('radio', { name: text })
const goTo = (page: Page, n: number) => page.getByRole('navigation', { name: 'Danh sách câu hỏi' }).getByRole('button', { name: new RegExp(`^Câu ${n}\\b`) }).click()

test('unassigned_exam_url_blocked', async ({ page }) => {
  const mine = await examForCandidate(admin)
  const other = await examForCandidate(admin)
  await loginCandidateUi(page, mine.code, mine.password)
  await expect(page.getByText(mine.title)).toBeVisible()
  await expect(page.getByText(other.title)).toHaveCount(0)
  await page.goto(`/thi/ky-thi/${other.examId}`)
  await expect(page.getByText('Bạn không được giao kỳ thi này')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Bắt đầu làm bài' })).toHaveCount(0)
})

test('autosave_shows_saving_saved_error', async ({ page }) => {
  const e = await examForCandidate(admin)
  await loginCandidateUi(page, e.code, e.password)
  await startExam(page, e.examId)

  let release: () => void = () => {}
  await page.route('**/answers/**', async (route) => {
    await new Promise<void>((r) => (release = r))
    await route.continue()
  })
  await choice(page, 'Đúng 1').check()
  await expect(status(page)).toContainText('Đang lưu')
  release()
  await expect(status(page)).toContainText('Đã lưu')

  await page.unroute('**/answers/**')
  await page.route('**/answers/**', (route) => route.fulfill({ status: 500, contentType: 'application/json', body: JSON.stringify({ success: false, data: null, error: { code: 'internal_error', message: 'Lỗi', requestId: 'x' } }) }))
  await goTo(page, 2)
  await choice(page, 'Sai 2A').check()
  await expect(status(page)).toContainText('Chưa lưu được')
  await page.unroute('**/answers/**')
  await expect(status(page)).toContainText('Đã lưu', { timeout: 15_000 })
})

test('reload_resumes_same_order_and_answers', async ({ page }) => {
  const e = await examForCandidate(admin, { questions: 4, exam: { shuffleQuestions: true, shuffleChoices: true } })
  await loginCandidateUi(page, e.code, e.password)
  await startExam(page, e.examId)
  const order = async () => (await page.getByTestId('question-text').allTextContents()).join('|')
  await page.getByRole('button', { name: 'Xem tất cả câu' }).click()
  const before = await order()
  await page.getByRole('radio', { name: /^Đúng / }).first().check()
  await expect(status(page)).toContainText('Đã lưu')

  await page.reload()
  await expect(page.getByRole('heading', { name: /Câu 1/ })).toBeVisible()
  await page.getByRole('button', { name: 'Xem tất cả câu' }).click()
  expect(await order()).toBe(before)
  await expect(page.getByRole('radio', { name: /^Đúng / }).first()).toBeChecked()
})

test('offline_then_online_marks_unsynced_then_syncs', async ({ page, context }) => {
  const e = await examForCandidate(admin)
  await loginCandidateUi(page, e.code, e.password)
  await startExam(page, e.examId)
  await context.setOffline(true)
  await choice(page, 'Đúng 1').check()
  await expect(status(page)).toContainText('Mất kết nối')
  await expect(status(page)).toContainText('1 câu chưa đồng bộ')
  await context.setOffline(false)
  await expect(status(page)).toContainText('Đã lưu', { timeout: 15_000 })

  await page.reload()
  await expect(choice(page, 'Đúng 1')).toBeChecked()
})

test('timer_uses_server_deadline_not_device_clock', async ({ page }) => {
  const e = await examForCandidate(admin, { exam: { durationMinutes: 30 } })
  await page.clock.install({ time: new Date(Date.now() + 3 * 3600_000) })
  await loginCandidateUi(page, e.code, e.password)
  await startExam(page, e.examId)
  const text = await page.getByRole('timer').textContent()
  const [mm] = (text ?? '').replace(/[^\d:]/g, '').split(':').map(Number)
  expect(mm).toBeGreaterThanOrEqual(28)
  expect(mm).toBeLessThanOrEqual(30)
})

test('submit_confirm_warns_blank_count', async ({ page }) => {
  const e = await examForCandidate(admin, { questions: 3 })
  await loginCandidateUi(page, e.code, e.password)
  await startExam(page, e.examId)
  await choice(page, 'Đúng 1').check()
  await expect(status(page)).toContainText('Đã lưu')
  await page.getByRole('button', { name: 'Nộp bài' }).click()
  const dialog = page.getByRole('dialog')
  await expect(dialog).toContainText('còn 2 câu chưa trả lời')
  await dialog.getByRole('button', { name: 'Nộp bài' }).click()
  await page.waitForURL(/\/thi\/ket-qua\//)
  await expect(page.getByText('3.33')).toBeVisible()
})

test('after_deadline_ui_locked', async ({ page }) => {
  const e = await examForCandidate(admin, { exam: { durationMinutes: 5 } })
  await page.clock.install()
  await loginCandidateUi(page, e.code, e.password)
  await startExam(page, e.examId)
  await page.clock.fastForward('06:00')
  await expect(page.getByText('Đã hết giờ làm bài')).toBeVisible()
  await expect(choice(page, 'Đúng 1')).toBeDisabled()
})

test('mobile_timer_does_not_cover_question', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 740 })
  const e = await examForCandidate(admin, { questions: 4 })
  await loginCandidateUi(page, e.code, e.password)
  await startExam(page, e.examId)
  await page.getByRole('button', { name: 'Xem tất cả câu' }).click()
  // Every option must be reachable and not hidden under the timer bar (trial click checks it receives the pointer).
  for (const radio of await page.getByRole('radio').all()) await radio.click({ trial: true })
  // The timer stays on screen while scrolling through the questions.
  await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight))
  await expect(page.getByRole('timer')).toBeInViewport()
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)
  expect(overflow).toBeLessThanOrEqual(0)
})
