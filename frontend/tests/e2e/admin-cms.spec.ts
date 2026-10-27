import { expect, test, type Page } from '@playwright/test'
import { AdminApi, PNG, createAdminUser, loginAdminUi, removeHomepageTestBlocks, unique } from './helpers'

let admin: AdminApi
test.beforeAll(async () => {
  admin = await AdminApi.create()
})
test.afterAll(async () => admin.dispose())

async function newPost(page: Page, title: string) {
  await page.goto('/admin/bai-viet/moi')
  await page.getByLabel('Tiêu đề').fill(title)
  await page.getByRole('button', { name: 'Lưu nháp' }).click()
  await page.waitForURL(/\/admin\/bai-viet\/[0-9a-f-]{36}$/)
}

const editor = (page: Page) => page.getByRole('textbox', { name: 'Nội dung bài viết' })

test('login_required_redirects', async ({ page }) => {
  await page.goto('/admin/bai-viet')
  await page.waitForURL(/\/admin\/dang-nhap\?next=%2Fadmin%2Fbai-viet/)
  await page.getByLabel('Tên đăng nhập').fill(process.env.E2E_ADMIN_USER ?? 'admin')
  await page.getByLabel('Mật khẩu').fill(process.env.E2E_ADMIN_PASSWORD ?? 'Admin123456')
  await page.getByRole('button', { name: 'Đăng nhập' }).click()
  await page.waitForURL(/\/admin\/bai-viet$/)
  await expect(page.getByRole('heading', { name: 'Bài viết' })).toBeVisible()
})

test('insert_image_from_computer_into_article_body_and_shows_publicly', async ({ page }) => {
  await loginAdminUi(page)
  const title = unique('Bài có ảnh')
  await newPost(page, title)
  await editor(page).click()
  await editor(page).pressSequentially('Đoạn mở đầu. ')
  await page.locator('input[data-upload="image"]').setInputFiles({ name: 'anh-hoi-nghi.png', mimeType: 'image/png', buffer: PNG })
  await expect(editor(page).locator('figure img')).toHaveCount(1)
  await editor(page).locator('figcaption').first().fill('Ảnh hội nghị')
  await page.getByRole('button', { name: 'Lưu nháp' }).click()
  await expect(page.getByText('Đã lưu')).toBeVisible()
  await page.getByRole('button', { name: 'Xuất bản' }).click()
  await expect(page.getByRole('status').filter({ hasText: 'Đã xuất bản' })).toBeVisible()

  const slug = await page.getByLabel('Đường dẫn').inputValue()
  await page.goto(`/tin-tuc/${slug}`)
  await expect(page.locator('article figure img')).toHaveAttribute('src', /\/media\//)
  await expect(page.locator('article figcaption')).toHaveText('Ảnh hội nghị')
})

test('paste_and_drag_drop_image', async ({ page }) => {
  await loginAdminUi(page)
  await newPost(page, unique('Dán ảnh'))
  await editor(page).click()
  await page.evaluate(async (b64) => {
    const bytes = Uint8Array.from(atob(b64), (c) => c.charCodeAt(0))
    const file = new File([bytes], 'dan.png', { type: 'image/png' })
    const el = document.querySelector('[aria-label="Nội dung bài viết"]')!
    const paste = new DataTransfer()
    paste.items.add(file)
    el.dispatchEvent(new ClipboardEvent('paste', { clipboardData: paste, bubbles: true, cancelable: true }))
    const drop = new DataTransfer()
    drop.items.add(new File([bytes], 'keo.png', { type: 'image/png' }))
    el.dispatchEvent(new DragEvent('drop', { dataTransfer: drop, bubbles: true, cancelable: true }))
  }, PNG.toString('base64'))
  await expect(editor(page).locator('figure img')).toHaveCount(2)
})

test('insert_uploaded_video_and_youtube_embed', async ({ page }) => {
  await loginAdminUi(page)
  await newPost(page, unique('Bài có video'))
  await editor(page).click()
  const mp4 = Buffer.concat([Buffer.from([0, 0, 0, 0x18, 0x66, 0x74, 0x79, 0x70, 0x6d, 0x70, 0x34, 0x32, 0, 0, 0, 0]), Buffer.alloc(4000, 1)])
  await page.locator('input[data-upload="video"]').setInputFiles({ name: 'clip.mp4', mimeType: 'video/mp4', buffer: mp4 })
  await expect(editor(page).locator('video source[src*="/media/"], video[src*="/media/"]')).toHaveCount(1)

  page.once('dialog', (d) => d.accept('https://www.youtube.com/watch?v=dQw4w9WgXcQ'))
  await page.getByRole('button', { name: 'Nhúng YouTube' }).click()
  await expect(editor(page).locator('iframe[src="https://www.youtube.com/embed/dQw4w9WgXcQ"]')).toHaveCount(1)

  page.once('dialog', (d) => d.accept('https://evil.example/video'))
  await page.getByRole('button', { name: 'Nhúng YouTube' }).click()
  await expect(page.getByRole('alert').filter({ hasText: 'không được hỗ trợ' })).toBeVisible()
  await page.getByRole('button', { name: 'Lưu nháp' }).click()
  await expect(page.getByText('Đã lưu')).toBeVisible()
})

test('editor_cannot_see_publish_button_and_api_403', async ({ page, request }) => {
  const ed = await createAdminUser(admin, 'editor')
  await loginAdminUi(page, ed.userName, ed.password)
  await newPost(page, unique('Bài của biên tập viên'))
  await expect(page.getByRole('button', { name: 'Gửi duyệt' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Xuất bản' })).toHaveCount(0)

  const id = page.url().split('/').pop()
  const status = await page.evaluate(async (postId) => {
    const t = await (await fetch('/api/antiforgery')).json()
    const r = await fetch(`/api/admin/posts/${postId}/transition`, {
      method: 'POST', headers: { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': t.data.token }, body: JSON.stringify({ to: 'Published' }),
    })
    return r.status
  }, id)
  expect(status).toBe(403)
})

test('unsaved_changes_warning', async ({ page }) => {
  await loginAdminUi(page)
  await newPost(page, unique('Chưa lưu'))
  await page.getByLabel('Tiêu đề').fill('Đã sửa nhưng chưa lưu')
  let message = ''
  page.once('dialog', async (d) => {
    message = d.message()
    await d.dismiss()
  })
  await page.getByRole('navigation', { name: 'Quản trị' }).getByRole('link', { name: 'Menu' }).click()
  await expect.poll(() => message).toContain('chưa lưu')
  await expect(page).toHaveURL(/\/admin\/bai-viet\//)
})

test('stale_version_shows_conflict_dialog', async ({ browser }) => {
  const ctx = await browser.newContext()
  const a = await ctx.newPage()
  await loginAdminUi(a)
  await newPost(a, unique('Xung đột'))
  const url = a.url()
  const b = await ctx.newPage()
  await b.goto(url)
  await expect(b.getByLabel('Tiêu đề')).not.toHaveValue('')

  await a.getByLabel('Tiêu đề').fill('Bản của tab A')
  await a.getByRole('button', { name: 'Lưu nháp' }).click()
  await expect(a.getByText('Đã lưu')).toBeVisible()

  await b.getByLabel('Tiêu đề').fill('Bản của tab B')
  await b.getByRole('button', { name: 'Lưu nháp' }).click()
  const dialog = b.getByRole('dialog')
  await expect(dialog).toContainText('người khác')
  await expect(dialog.getByRole('button', { name: 'Tải lại bản mới nhất' })).toBeVisible()
  await ctx.close()
})

test('rename_menu_item_changes_url_and_old_url_redirects', async ({ page }) => {
  await loginAdminUi(page)
  const label = unique('Mục kiểm thử')
  const menus = await admin.call('GET', '/api/admin/menus')
  const main = menus.find((m: { code: string }) => m.code === 'main')
  const item = await admin.call('POST', `/api/admin/menus/${main.id}/items`, { label, linkType: 'None' })

  await page.goto('/admin/menu')
  await page.getByRole('button', { name: `Sửa ${label}` }).click()
  await page.getByLabel('Tên mục').fill(label + ' đổi')
  await page.getByRole('button', { name: 'Lưu mục' }).click()
  await expect(page.getByText(`/${item.slug}-doi`).first()).toBeVisible()

  await page.goto(`/${item.slug}`)
  await expect(page).toHaveURL(new RegExp(`/${item.slug}-doi$`))
})

test('block_reorder_by_drag_and_by_buttons', async ({ page }) => {
  await loginAdminUi(page)
  await page.goto('/admin/trang-chu')
  const titles = () => page.locator('[data-block-item] [data-block-title]').allTextContents()
  await expect(page.locator('[data-block-item]').nth(2)).toBeVisible()
  const before = await titles()
  expect(before.length).toBeGreaterThan(2)

  await page.getByRole('button', { name: `Chuyển xuống: ${before[0]}` }).click()
  expect((await titles()).slice(0, 2)).toEqual([before[1], before[0]])

  const items = page.locator('[data-block-item]')
  await items.nth(2).dragTo(items.nth(0))
  const after = await titles()
  expect(after[0]).toBe(before[2])
  await page.getByRole('button', { name: 'Lưu bản nháp' }).click()
  await expect(page.getByText('Đã lưu bản nháp')).toBeVisible()
  await page.reload()
  await expect(page.locator('[data-block-item]').nth(2)).toBeVisible()
  expect(await titles()).toEqual(after)
})

test('preview_three_viewports_does_not_change_public', async ({ page, context }) => {
  await removeHomepageTestBlocks(admin)
  await loginAdminUi(page)
  await page.goto('/admin/trang-chu')
  const marker = unique('Nội dung xem trước')
  await page.getByRole('button', { name: 'Thêm khối' }).click()
  await page.getByRole('menuitem', { name: 'Văn bản giới thiệu' }).click()
  await page.getByLabel('Nội dung HTML').last().fill(`<p>${marker}</p>`)
  await page.getByRole('button', { name: 'Lưu bản nháp' }).click()
  await expect(page.getByText('Đã lưu bản nháp')).toBeVisible()

  for (const [label, width] of [['Máy tính', 1280], ['Máy tính bảng', 768], ['Điện thoại', 390]] as const) {
    await page.getByRole('button', { name: label, exact: true }).click()
    const frame = page.frameLocator('iframe[title="Xem trước trang chủ"]')
    await expect(frame.getByText(marker)).toBeVisible()
    await expect(page.locator('iframe[title="Xem trước trang chủ"]')).toHaveAttribute('width', String(width))
  }
  const pub = await context.newPage()
  await pub.goto('/')
  await expect(pub.getByText(marker)).toHaveCount(0)
})

test('publish_then_public_updates_without_deploy', async ({ page, context }) => {
  await removeHomepageTestBlocks(admin)
  await loginAdminUi(page)
  await page.goto('/admin/trang-chu')
  const marker = unique('Đã xuất bản lên trang chủ')
  await page.getByRole('button', { name: 'Thêm khối' }).click()
  await page.getByRole('menuitem', { name: 'Văn bản giới thiệu' }).click()
  await page.getByLabel('Nội dung HTML').last().fill(`<p>${marker}</p>`)
  await page.getByRole('button', { name: 'Lưu bản nháp' }).click()
  await expect(page.getByText('Đã lưu bản nháp')).toBeVisible()
  await page.getByRole('button', { name: 'Xuất bản trang chủ' }).click()
  await expect(page.getByText(/Đã xuất bản phiên bản \d+/)).toBeVisible()

  const pub = await context.newPage()
  await pub.goto('/')
  await expect(pub.getByText(marker)).toBeVisible()
})

test('rollback_restores', async ({ page, context }) => {
  await removeHomepageTestBlocks(admin)
  await loginAdminUi(page)
  await page.goto('/admin/trang-chu')
  const marker = unique('Bản sẽ bị khôi phục')
  const history = page.getByRole('region', { name: 'Lịch sử xuất bản' })
  const liveVersion = await history.locator('[data-version][data-status="Published"]').getAttribute('data-version')

  await page.getByRole('button', { name: 'Thêm khối' }).click()
  await page.getByRole('menuitem', { name: 'Văn bản giới thiệu' }).click()
  await page.getByLabel('Nội dung HTML').last().fill(`<p>${marker}</p>`)
  await page.getByRole('button', { name: 'Lưu bản nháp' }).click()
  await expect(page.getByText('Đã lưu bản nháp')).toBeVisible()
  await page.getByRole('button', { name: 'Xuất bản trang chủ' }).click()
  await expect(page.getByText(/Đã xuất bản phiên bản \d+/)).toBeVisible()
  const pub = await context.newPage()
  await pub.goto('/')
  await expect(pub.getByText(marker)).toBeVisible()

  page.once('dialog', (d) => d.accept())
  await history.getByRole('button', { name: `Khôi phục phiên bản ${liveVersion}` }).click()
  await expect(page.getByText(/Đã khôi phục/)).toBeVisible()
  await pub.reload()
  await expect(pub.getByText(marker)).toHaveCount(0)
})

test('manage_redirects_from_admin', async ({ page, request }) => {
  const post = await admin.publishedPost(unique('Bài chuyển hướng'))
  const old = `/trang-cu-${Math.random().toString(36).slice(2, 8)}/bai.html`
  await loginAdminUi(page)
  await page.goto('/admin/chuyen-huong')
  await page.getByRole('button', { name: 'Thêm chuyển hướng' }).click()
  await page.getByLabel('Đường dẫn cũ').fill(old)
  await page.getByLabel('Đường dẫn mới').fill(`/tin-tuc/${post.slug}`)
  await page.getByRole('button', { name: 'Lưu' }).click()
  await expect(page.getByRole('cell', { name: old })).toBeVisible()

  const moved = await request.get(old, { maxRedirects: 0 })
  expect([301, 308]).toContain(moved.status())
  expect(moved.headers()['location']).toContain(`/tin-tuc/${post.slug}`)

  page.once('dialog', (d) => d.accept())
  await page.getByRole('row', { name: new RegExp(old.replace(/[.*+?^${}()|[\]\\/]/g, '\\$&')) }).getByRole('button', { name: 'Xóa' }).click()
  await expect(page.getByRole('cell', { name: old })).toHaveCount(0)
  expect((await request.get(old, { maxRedirects: 0 })).status()).toBe(404)
})
