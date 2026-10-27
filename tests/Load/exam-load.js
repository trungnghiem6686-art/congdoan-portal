// Kiểm thử tải cổng thi bằng k6 (https://k6.io).
//
// KỊCH BẢN GIẢ ĐỊNH: mặc định 3000 thí sinh đồng thời. CON SỐ NÀY CHƯA ĐƯỢC XÁC NHẬN VÀ CHƯA ĐO.
// Chỉ ghi kết quả vào docs/acceptance-matrix.md kèm cấu hình máy chủ, dữ liệu và số thí sinh thực tế đã chạy.
//
// Mỗi thí sinh ảo (VU): đăng nhập lần đầu → đổi mật khẩu → bắt đầu bài → nhận quyền ghi → trả lời từng câu
// (autosave, có thời gian suy nghĩ) → nộp bài. Dữ liệu (câu hỏi MẪU, kỳ thi, thí sinh, nhóm) do setup() tạo qua API quản trị.
//
// CHỈ chạy trên staging, không chạy trên trang đang hoạt động:
//   k6 run -e BASE_URL=https://staging.example -e CONFIRM_STAGING=1 \
//          -e ADMIN_USER=... -e ADMIN_PASSWORD=... -e N=200 -e QUESTIONS=30 tests/Load/exam-load.js
//
// Lưu ý: mọi VU chạy từ cùng một máy nên chung một IP. Giới hạn đăng nhập theo IP (mặc định 10 lần/phút)
// sẽ chặn phần lớn thí sinh ảo. Trên staging, tạm đặt RateLimit__LoginPermitPerMinute đủ lớn trong lúc đo,
// rồi trả lại giá trị cũ. Khi ghi kết quả, nêu rõ là đã thay đổi giới hạn này.
//
// Tham số (biến môi trường):
//   N               số thí sinh (mặc định 3000, giả định)
//   QUESTIONS       số câu mỗi đề (mặc định 30)
//   START_WINDOW_S  các thí sinh bắt đầu rải đều trong khoảng này (mặc định 60 giây)
//   THINK_MIN_S / THINK_MAX_S   thời gian suy nghĩ mỗi câu (mặc định 3–10 giây)
import http from 'k6/http'
import { check, fail, sleep } from 'k6'
import { Counter, Trend } from 'k6/metrics'
import exec from 'k6/execution'

const BASE = (__ENV.BASE_URL || '').replace(/\/$/, '')
const N = Number(__ENV.N || 3000)
const QUESTIONS = Number(__ENV.QUESTIONS || 30)
const START_WINDOW_S = Number(__ENV.START_WINDOW_S || 60)
const THINK_MIN_S = Number(__ENV.THINK_MIN_S || 3)
const THINK_MAX_S = Number(__ENV.THINK_MAX_S || 10)
const RUN = `load-${Date.now().toString(36)}`
const START_PASSWORD = 'Start12345a'
const PASSWORD = 'Thi12345a'

const saveMs = new Trend('exam_autosave_ms', true)
const startMs = new Trend('exam_start_ms', true)
const submitMs = new Trend('exam_submit_ms', true)
const saveFailures = new Counter('exam_autosave_failed')

export const options = {
  setupTimeout: '30m',
  scenarios: {
    candidates: { executor: 'per-vu-iterations', vus: N, iterations: 1, maxDuration: '2h' },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    exam_autosave_failed: ['count<1'],
    exam_autosave_ms: ['p(95)<1000'],
    exam_start_ms: ['p(95)<2000'],
  },
}

// ---- helpers -------------------------------------------------------------------------------

function csrf() {
  const r = http.get(`${BASE}/api/antiforgery`)
  if (r.status !== 200) fail(`antiforgery ${r.status}`)
  return r.json('data.token')
}

function call(method, path, body, token, extraHeaders = {}) {
  const headers = { 'Content-Type': 'application/json', ...extraHeaders }
  if (method !== 'GET') headers['X-XSRF-TOKEN'] = token
  return http.request(method, `${BASE}${path}`, body === undefined ? null : JSON.stringify(body), { headers })
}

function ok(r, what) {
  if (!r.json('success')) fail(`${what}: ${r.status} ${r.body && r.body.slice(0, 300)}`)
  return r.json('data')
}

const rand = (min, max) => min + Math.random() * (max - min)

// ---- setup: sample exam + candidates (runs once) -------------------------------------------

export function setup() {
  if (!BASE) fail('Thiếu BASE_URL.')
  if (__ENV.CONFIRM_STAGING !== '1') fail('Đặt CONFIRM_STAGING=1 để xác nhận đây là môi trường staging, không phải trang đang hoạt động.')
  if (!__ENV.ADMIN_USER || !__ENV.ADMIN_PASSWORD) fail('Thiếu ADMIN_USER/ADMIN_PASSWORD (lấy từ biến môi trường, không ghi vào tệp).')

  let token = csrf()
  ok(call('POST', '/api/admin/auth/login', { userName: __ENV.ADMIN_USER, password: __ENV.ADMIN_PASSWORD }, token), 'admin login')
  token = csrf()

  const questionIds = []
  for (let i = 1; i <= QUESTIONS; i++) {
    const q = ok(call('POST', '/api/admin/questions', {
      type: 'Single', topic: `MẪU ${RUN}`, difficulty: 'Easy', points: 1, content: `[MẪU tải] Câu ${i}`,
      choices: [{ content: 'Phương án A', isCorrect: true }, { content: 'Phương án B', isCorrect: false },
        { content: 'Phương án C', isCorrect: false }, { content: 'Phương án D', isCorrect: false }],
    }, token), 'create question')
    ok(call('POST', `/api/admin/questions/${q.id}/approve`, undefined, token), 'approve question')
    questionIds.push(q.id)
  }

  // Candidates in batches of 500 through the CSV import.
  const codes = []
  for (let start = 0; start < N; start += 500) {
    const rows = ['ma,ho_ten,mat_khau']
    for (let i = start; i < Math.min(N, start + 500); i++) {
      const code = `${RUN}-${i}`
      codes.push(code)
      rows.push(`${code},Thí sinh MẪU ${i},${START_PASSWORD}`)
    }
    ok(call('POST', '/api/admin/candidates/import', { csv: rows.join('\n') }, token), 'import candidates')
  }

  const group = ok(call('POST', '/api/admin/candidate-groups', { name: `Nhóm MẪU ${RUN}`, description: 'Kiểm thử tải' }, token), 'create group')
  const ids = []
  for (let page = 1; ids.length < N; page++) {
    const res = ok(call('GET', `/api/admin/candidates?q=${encodeURIComponent(RUN)}&page=${page}&pageSize=100`), 'list candidates')
    if (!res.items.length) break
    for (const c of res.items) ids.push(c.id)
  }
  for (let i = 0; i < ids.length; i += 500)
    ok(call('PUT', `/api/admin/candidate-groups/${group.id}/members`, { add: ids.slice(i, i + 500) }, token), 'group members')

  const now = Date.now()
  const exam = ok(call('POST', '/api/admin/exams', {
    title: `[MẪU] Kiểm thử tải ${RUN}`, opensAtUtc: new Date(now - 60_000).toISOString(), closesAtUtc: new Date(now + 4 * 3600_000).toISOString(),
    durationMinutes: 90, maxAttempts: 1, scoreRule: 'Highest', passScore: 5, resultRelease: 'Immediately', questionSource: 'Manual', showAnswerDetail: false,
  }, token), 'create exam')
  ok(call('PUT', `/api/admin/exams/${exam.id}/questions`, { questionIds }, token), 'exam questions')
  ok(call('PUT', `/api/admin/exams/${exam.id}/assignments`, { add: [{ targetType: 'Group', targetId: group.id }] }, token), 'assign group')
  ok(call('POST', `/api/admin/exams/${exam.id}/publish`, undefined, token), 'publish exam')

  console.log(`Kỳ thi ${exam.id}: ${QUESTIONS} câu, ${codes.length} thí sinh (${RUN}). KỊCH BẢN GIẢ ĐỊNH — kết quả chỉ đúng cho cấu hình đang chạy.`)
  return { examId: exam.id, codes }
}

// ---- one candidate --------------------------------------------------------------------------

export default function (data) {
  const code = data.codes[exec.vu.idInTest - 1]
  if (!code) return
  sleep(rand(0, START_WINDOW_S))

  let token = csrf()
  ok(call('POST', '/api/thi/auth/login', { userName: code, password: START_PASSWORD }, token), 'candidate login')
  token = csrf()
  ok(call('POST', '/api/thi/auth/change-password', { currentPassword: START_PASSWORD, newPassword: PASSWORD }, token), 'change password')
  token = csrf()

  const started = call('POST', `/api/thi/exams/${data.examId}/start`, undefined, token)
  startMs.add(started.timings.duration)
  const attemptId = ok(started, 'start').attemptId
  const view = ok(call('GET', `/api/thi/attempts/${attemptId}`), 'attempt view')
  const writer = ok(call('POST', `/api/thi/attempts/${attemptId}/claim`, { force: false }, token), 'claim').writerToken

  let seq = view.lastSeq || 0
  let sinceHeartbeat = 0
  for (const q of view.questions) {
    const think = rand(THINK_MIN_S, THINK_MAX_S)
    sleep(think)
    sinceHeartbeat += think
    if (sinceHeartbeat >= 20) {
      call('POST', `/api/thi/attempts/${attemptId}/heartbeat`, undefined, token, { 'X-Attempt-Writer': writer })
      sinceHeartbeat = 0
    }
    const choice = q.choices[Math.floor(Math.random() * q.choices.length)]
    const r = call('PUT', `/api/thi/attempts/${attemptId}/answers/${q.id}`, { choiceIds: [choice.id], seq: ++seq, flagged: false }, token,
      { 'X-Attempt-Writer': writer })
    saveMs.add(r.timings.duration)
    if (!check(r, { 'autosave ok': (x) => x.status === 200 })) saveFailures.add(1)
  }

  const submitted = call('POST', `/api/thi/attempts/${attemptId}/submit`, undefined, token)
  submitMs.add(submitted.timings.duration)
  check(submitted, { 'submit ok': (x) => x.status === 200 })
}
