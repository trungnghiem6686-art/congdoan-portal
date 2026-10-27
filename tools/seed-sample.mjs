// Seeds clearly labelled SAMPLE ("MẪU") content through the public admin API.
// Usage: node tools/seed-sample.mjs [baseUrl] [adminUser] [adminPassword] [newPasswordIfChangeRequired]
// Never point this at production.
const base = process.argv[2] ?? 'http://localhost:5000'
const user = process.argv[3] ?? 'admin'
let pass = process.argv[4] ?? 'Admin12345'
const newPass = process.argv[5] ?? 'Admin123456'

const jar = new Map()
let csrf = ''

async function call(method, path, body) {
  const headers = { Accept: 'application/json', Cookie: [...jar].map(([k, v]) => `${k}=${v}`).join('; ') }
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (method !== 'GET') headers['X-XSRF-TOKEN'] = csrf
  const res = await fetch(base + path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
  for (const c of res.headers.getSetCookie?.() ?? []) {
    const [pair] = c.split(';')
    const i = pair.indexOf('=')
    jar.set(pair.slice(0, i), pair.slice(i + 1))
  }
  const json = await res.json().catch(() => ({}))
  if (!json.success) throw new Error(`${method} ${path} → ${res.status} ${JSON.stringify(json.error)}`)
  return json.data
}

const refreshCsrf = async () => (csrf = (await call('GET', '/api/antiforgery')).token)

await refreshCsrf()
let me = await call('POST', '/api/admin/auth/login', { userName: user, password: pass }).catch(async () => {
  pass = newPass
  return call('POST', '/api/admin/auth/login', { userName: user, password: pass })
})
await refreshCsrf()
if (me.mustChangePassword) {
  await call('POST', '/api/admin/auth/change-password', { currentPassword: pass, newPassword: newPass })
  console.log(`First-login password changed; admin password is now: ${newPass}`)
}

const cats = {}
for (const name of ['Tin tức – Sự kiện', 'Hoạt động công đoàn', 'Chăm lo đời sống', 'Văn hóa – Thể thao']) {
  const existing = (await call('GET', '/api/admin/categories')).find((c) => c.name === name)
  cats[name] = existing ?? (await call('POST', '/api/admin/categories', { name }))
}

const posts = [
  ['Hội nghị Ban Chấp hành Công đoàn Đường sắt Việt Nam (MẪU)', 'Tin tức – Sự kiện', true],
  ['Tổng kết phong trào thi đua lao động giỏi năm (MẪU)', 'Hoạt động công đoàn', true],
  ['Thăm hỏi, tặng quà đoàn viên có hoàn cảnh khó khăn (MẪU)', 'Chăm lo đời sống', false],
  ['Giải bóng đá công nhân ngành đường sắt (MẪU)', 'Văn hóa – Thể thao', false],
  ['Tập huấn nghiệp vụ cho cán bộ công đoàn cơ sở (MẪU)', 'Hoạt động công đoàn', false],
  ['Phát động Tháng Công nhân (MẪU)', 'Tin tức – Sự kiện', false],
]
const existingPosts = (await call('GET', '/api/admin/posts?pageSize=200')).items.map((p) => p.title)
for (const [title, cat, featured] of posts) {
  if (existingPosts.includes(title)) continue
  const p = await call('POST', '/api/admin/posts', {
    title,
    summary: 'Nội dung mẫu để kiểm tra giao diện. Sẽ được thay bằng tin chính thức.',
    body: '<p>Đây là <strong>nội dung mẫu</strong> (MẪU) dùng để kiểm tra giao diện cổng thông tin.</p><p>Nội dung chính thức sẽ do ban biên tập cung cấp.</p>',
    categoryId: cats[cat].id,
    isFeatured: featured,
    tags: ['mẫu'],
  })
  await call('POST', `/api/admin/posts/${p.id}/transition`, { to: 'Published' })
}

const menus = await call('GET', '/api/admin/menus')
const main = menus.find((m) => m.code === 'main')
const tree = await call('GET', `/api/admin/menus/${main.id}/items`)
if (tree.length === 0) {
  const intro = await call('POST', `/api/admin/menus/${main.id}/items`, { label: 'Giới thiệu', linkType: 'None' })
  const page = await call('POST', '/api/admin/pages', { title: 'Lịch sử hình thành (MẪU)', body: '<p>Nội dung giới thiệu mẫu (MẪU).</p>' })
  await call('POST', `/api/admin/menus/${main.id}/items`, { label: 'Lịch sử hình thành', linkType: 'Page', targetId: page.id, parentId: intro.id })
  for (const name of Object.keys(cats)) await call('POST', `/api/admin/menus/${main.id}/items`, { label: name, linkType: 'Category', targetId: cats[name].id })
  await call('POST', `/api/admin/menus/${main.id}/items`, { label: 'Văn bản', linkType: 'Url', url: '/van-ban' })
  await call('POST', `/api/admin/menus/${main.id}/items`, { label: 'Liên hệ', linkType: 'Url', url: '/lien-he' })
}

const docs = (await call('GET', '/api/admin/documents?pageSize=5')).items
if (docs.length === 0) {
  await call('POST', '/api/admin/documents', { number: '01/NQ-MẪU', title: 'Nghị quyết mẫu về công tác năm (MẪU)', docType: 'Nghị quyết', issuer: 'Ban Thường vụ (MẪU)', issuedDate: '2026-01-15' })
  await call('POST', '/api/admin/documents', { number: '12/HD-MẪU', title: 'Hướng dẫn mẫu tổ chức hội nghị (MẪU)', docType: 'Hướng dẫn', issuer: 'Văn phòng (MẪU)', issuedDate: '2026-02-20' })
}

const draft = await call('PUT', '/api/admin/homepage/draft', {
  sections: [
    { type: 'featured_news', title: 'Tin nổi bật', config: { limit: 5, layout: 'hero-list' } },
    { type: 'quick_links', title: 'Tiện ích', config: { layout: 'icons', items: [
      { label: 'Thi trực tuyến', url: '/thi' }, { label: 'Văn bản', url: '/van-ban' }, { label: 'Thư viện', url: '/thu-vien' },
      { label: 'Tìm kiếm', url: '/tim-kiem' }, { label: 'Liên hệ', url: '/lien-he' }, { label: 'Giới thiệu', url: '/gioi-thieu' } ] } },
    { type: 'news_grid', title: 'Hoạt động công đoàn', config: { limit: 4, layout: 'grid', columns: 4, categorySlug: cats['Hoạt động công đoàn'].slug } },
    { type: 'documents', title: 'Văn bản mới', config: { limit: 5, layout: 'list' } },
    { type: 'exam_notice', title: 'Thông báo kỳ thi', config: { limit: 3, layout: 'list' } },
    { type: 'news_list', title: 'Tin mới cập nhật', config: { limit: 6, layout: 'list' } },
    { type: 'intro_text', title: 'Giới thiệu', config: { html: '<p>Công đoàn Đường sắt Việt Nam (nội dung MẪU) – đại diện, chăm lo và bảo vệ quyền, lợi ích hợp pháp, chính đáng của đoàn viên, người lao động.</p>', layout: 'boxed' } },
  ],
})
await call('POST', '/api/admin/homepage/publish', { revisionId: draft.id })
console.log('Sample content ready (labelled MẪU).')
