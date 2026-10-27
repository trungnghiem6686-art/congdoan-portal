import { expect, test } from '@playwright/test'
import { AdminApi, PDF, unique } from './helpers'

let admin: AdminApi
test.beforeAll(async () => {
  admin = await AdminApi.create()
})
test.afterAll(async () => admin.dispose())

test('article_page_shows_breadcrumb_date_attachments_print', async ({ page }) => {
  const cat = await admin.call('POST', '/api/admin/categories', { name: unique('Chuyên mục E2E') })
  const file = await admin.upload('bien-ban.pdf', PDF, 'application/pdf')
  const post = await admin.publishedPost(unique('Bài kiểm thử chi tiết'), { categoryId: cat.id, attachmentIds: [file.id] })

  await page.goto(`/tin-tuc/${post.slug}`)
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(post.title)
  const crumbs = page.getByRole('navigation', { name: 'Đường dẫn' })
  await expect(crumbs.getByRole('link', { name: 'Trang chủ' })).toBeVisible()
  await expect(crumbs.getByRole('link', { name: cat.name })).toHaveAttribute('href', `/chuyen-muc/${cat.slug}`)
  await expect(page.locator('article time')).toBeVisible()
  await expect(page.getByRole('link', { name: /bien-ban\.pdf/ })).toBeVisible()
  await expect(page.getByRole('button', { name: 'In bài' })).toBeVisible()
})

test('category_filters_and_paginates', async ({ page }) => {
  const cat = await admin.call('POST', '/api/admin/categories', { name: unique('Phân trang') })
  for (let i = 1; i <= 14; i++) await admin.publishedPost(`${cat.name} – tin ${String(i).padStart(2, '0')}`, { categoryId: cat.id })
  const other = await admin.publishedPost(unique('Không thuộc chuyên mục'))

  await page.goto(`/chuyen-muc/${cat.slug}`)
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(cat.name)
  await expect(page.locator('[data-post-item]')).toHaveCount(12)
  await expect(page.getByText(other.title)).toHaveCount(0)
  await page.getByRole('link', { name: 'Trang sau' }).click()
  await expect(page).toHaveURL(/trang=2/)
  await expect(page.locator('[data-post-item]')).toHaveCount(2)
})

test('search_empty_state', async ({ page }) => {
  await page.goto('/tim-kiem?q=' + encodeURIComponent('khongcogitrongnay' + Date.now()))
  await expect(page.getByText('Không tìm thấy kết quả phù hợp')).toBeVisible()

  const post = await admin.publishedPost(unique('Hội thao Công đoàn'))
  await page.goto('/tim-kiem?q=' + encodeURIComponent(post.title.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D')))
  await expect(page.getByRole('link', { name: post.title })).toBeVisible()
})

test('contact_shows_success_only_after_api_ok', async ({ page }) => {
  await page.goto('/lien-he')
  await page.getByRole('button', { name: 'Gửi liên hệ' }).click()
  await expect(page.getByText('Vui lòng nhập họ tên')).toBeVisible()
  await expect(page.locator('form').getByRole('status')).toHaveCount(0)

  await page.route('**/api/public/contact', (route) => route.fulfill({
    status: 500, contentType: 'application/json',
    body: JSON.stringify({ success: false, data: null, error: { code: 'internal_error', message: 'Lỗi máy chủ', requestId: 'x' } }),
  }))
  await page.getByLabel('Họ và tên').fill('Nguyễn Văn Kiểm Thử')
  await page.getByLabel('Email').fill('kiemthu@example.com')
  await page.getByLabel('Nội dung').fill('Nội dung liên hệ kiểm thử')
  await page.getByRole('button', { name: 'Gửi liên hệ' }).click()
  await expect(page.locator('form').getByRole('alert')).toContainText('Lỗi máy chủ')
  await expect(page.getByText('Đã gửi liên hệ')).toHaveCount(0)

  await page.unroute('**/api/public/contact')
  await page.getByRole('button', { name: 'Gửi liên hệ' }).click()
  await expect(page.locator('form').getByRole('status')).toContainText('Đã gửi liên hệ')
})

for (const width of [360, 390, 768, 1024, 1440]) {
  test(`no_horizontal_scroll_at_${width}`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 })
    for (const path of ['/', '/tin-tuc', '/van-ban', '/thu-vien', '/lien-he', '/tim-kiem?q=cong+doan']) {
      await page.goto(path)
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)
      expect(overflow, `${path} @${width}`).toBeLessThanOrEqual(0)
    }
  })
}

test('direct_refresh_of_dynamic_url_works', async ({ page }) => {
  const post = await admin.publishedPost(unique('Tải lại trực tiếp'))
  const res = await page.goto(`/tin-tuc/${post.slug}`)
  expect(res?.status()).toBe(200)
  await page.reload()
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(post.title)

  const missing = await page.goto('/tin-tuc/khong-ton-tai-' + Date.now())
  expect(missing?.status()).toBe(404)
  await expect(page.getByText('Không tìm thấy trang')).toBeVisible()
})

test('renamed_menu_url_redirects_permanently', async ({ page, request }) => {
  const menus = await admin.call('GET', '/api/admin/menus')
  const main = menus.find((m: { code: string }) => m.code === 'main')
  const label = unique('Phong trào')
  const item = await admin.call('POST', `/api/admin/menus/${main.id}/items`, { label, linkType: 'None' })
  await admin.call('PUT', `/api/admin/menu-items/${item.id}`, { label: label + ' mới', linkType: 'None', rowVersion: item.rowVersion })

  const res = await request.get(`/${item.slug}`, { maxRedirects: 0 })
  expect([301, 308]).toContain(res.status())
  expect(res.headers()['location']).toContain(`${item.slug}-moi`)
  await page.goto(`/${item.slug}`)
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(label + ' mới')
})

test('sitemap_robots_and_canonical', async ({ page, request }) => {
  const post = await admin.publishedPost(unique('Bài sitemap'))
  const sitemap = await request.get('/sitemap.xml')
  expect(sitemap.status()).toBe(200)
  const xml = await sitemap.text()
  expect(xml).toContain(`/tin-tuc/${post.slug}</loc>`)
  expect(xml).not.toContain('/admin')

  const robots = await (await request.get('/robots.txt')).text()
  expect(robots).toMatch(/Disallow: \/admin/)
  expect(robots).toMatch(/Disallow: \/thi/)
  expect(robots).toMatch(/Sitemap: .*\/sitemap\.xml/)

  await page.goto(`/tin-tuc/${post.slug}`)
  await expect(page.locator('link[rel="canonical"]')).toHaveAttribute('href', new RegExp(`/tin-tuc/${post.slug}$`))
})
