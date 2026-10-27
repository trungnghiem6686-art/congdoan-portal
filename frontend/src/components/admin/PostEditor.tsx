'use client'

import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useRouter } from 'next/navigation'
import { api, ApiClientError } from '@/lib/api'
import { uploadFile } from '@/lib/upload'
import { formatDateTime } from '@/lib/format'
import { useAdmin } from './AdminContext'
import { RichTextEditor } from './RichTextEditor'
import { Dialog, Field, Notice, Title, btnDanger, btnGhost, btnPrimary, input } from './ui'

type Post = {
  id: string; title: string; slug: string; slugIsManual: boolean; summary?: string | null; body: string; coverMediaId?: string | null
  categoryId?: string | null; tags: string[]; attachmentIds: string[]; isFeatured: boolean; status: string; reviewNote?: string | null
  publishedUtc?: string | null; scheduledUtc?: string | null; rowVersion: string
}
type Category = { id: string; name: string }
type Form = { title: string; slug: string; summary: string; body: string; categoryId: string; tags: string; coverMediaId: string | null; attachmentIds: string[]; isFeatured: boolean }

const statusLabel: Record<string, string> = {
  Draft: 'Nháp', PendingReview: 'Chờ duyệt', Approved: 'Đã duyệt', Scheduled: 'Đã lên lịch', Published: 'Đã xuất bản', Archived: 'Lưu trữ',
}

const toForm = (p?: Post): Form => ({
  title: p?.title ?? '', slug: p?.slug ?? '', summary: p?.summary ?? '', body: p?.body ?? '', categoryId: p?.categoryId ?? '',
  tags: p?.tags.join(', ') ?? '', coverMediaId: p?.coverMediaId ?? null, attachmentIds: p?.attachmentIds ?? [], isFeatured: p?.isFeatured ?? false,
})

export function PostEditor({ id }: { id?: string }) {
  const router = useRouter()
  const { can, setDirty } = useAdmin()
  const [post, setPost] = useState<Post | undefined>()
  const [form, setForm] = useState<Form>(toForm())
  const [saved, setSaved] = useState<Form>(toForm())
  const [initialBody, setInitialBody] = useState('')
  const [cats, setCats] = useState<Category[]>([])
  const [msg, setMsg] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null)
  const [conflict, setConflict] = useState(false)
  const [schedule, setSchedule] = useState('')
  const [loading, setLoading] = useState(!!id)
  const saving = useRef(false)

  const apply = useCallback((p: Post) => {
    setPost(p)
    setForm(toForm(p))
    setSaved(toForm(p))
    setInitialBody(p.body)
    setLoading(false)
  }, [])

  const load = useCallback(() => {
    if (id) api<Post>(`/api/admin/posts/${id}`).then(apply)
  }, [id, apply])

  useEffect(() => {
    if (id) api<Post>(`/api/admin/posts/${id}`).then(apply)
    api<Category[]>('/api/admin/categories').then(setCats).catch(() => setCats([]))
  }, [id, apply])

  const dirty = useMemo(() => JSON.stringify(form) !== JSON.stringify(saved), [form, saved])
  useEffect(() => {
    setDirty(dirty)
    return () => setDirty(false)
  }, [dirty, setDirty])

  const set = <K extends keyof Form>(k: K, v: Form[K]) => setForm((f) => ({ ...f, [k]: v }))

  const save = useCallback(async (silent = false) => {
    if (saving.current) return post
    if (!form.title.trim()) {
      setMsg({ kind: 'error', text: 'Vui lòng nhập tiêu đề.' })
      return
    }
    saving.current = true
    const body = {
      title: form.title, slug: form.slug || null, summary: form.summary, body: form.body, categoryId: form.categoryId || null,
      tags: form.tags.split(',').map((t) => t.trim()).filter(Boolean), coverMediaId: form.coverMediaId, attachmentIds: form.attachmentIds,
      isFeatured: form.isFeatured, rowVersion: post?.rowVersion,
    }
    try {
      const p = post
        ? await api<Post>(`/api/admin/posts/${post.id}`, { method: 'PUT', body })
        : await api<Post>('/api/admin/posts', { method: 'POST', body })
      setPost(p)
      const f = { ...form, slug: p.slug, body: p.body }
      setForm(f)
      setSaved(f)
      setMsg({ kind: 'ok', text: silent ? `Đã tự lưu lúc ${formatDateTime(new Date().toISOString())}` : 'Đã lưu' })
      if (!post) {
        setDirty(false)
        router.replace(`/admin/bai-viet/${p.id}`)
      }
      return p
    } catch (e) {
      if (e instanceof ApiClientError && e.code === 'concurrency_conflict') setConflict(true)
      else setMsg({ kind: 'error', text: e instanceof Error ? e.message : 'Không lưu được.' })
    } finally {
      saving.current = false
    }
  }, [form, post, router, setDirty])

  // Autosave drafts every 30 seconds while there are unsaved changes.
  useEffect(() => {
    if (!post || post.status !== 'Draft' || !dirty) return
    const t = setTimeout(() => void save(true), 30_000)
    return () => clearTimeout(t)
  }, [post, dirty, save])

  async function transition(to: string, extra: Record<string, unknown> = {}) {
    const current = dirty ? await save() : post
    if (!current) return
    try {
      const p = await api<Post>(`/api/admin/posts/${current.id}/transition`, { method: 'POST', body: { to, ...extra } })
      setPost(p)
      setMsg({ kind: 'ok', text: to === 'Published' ? 'Đã xuất bản' : `Đã chuyển sang: ${statusLabel[to]}` })
    } catch (e) {
      setMsg({ kind: 'error', text: e instanceof Error ? e.message : 'Không thực hiện được.' })
    }
  }

  async function uploadCover(file: File) {
    try {
      const m = await uploadFile(file)
      set('coverMediaId', m.id)
    } catch (e) {
      setMsg({ kind: 'error', text: e instanceof Error ? e.message : 'Không tải được ảnh.' })
    }
  }

  async function uploadAttachment(file: File) {
    try {
      const m = await uploadFile(file)
      set('attachmentIds', [...form.attachmentIds, m.id])
    } catch (e) {
      setMsg({ kind: 'error', text: e instanceof Error ? e.message : 'Không tải được tệp.' })
    }
  }

  if (loading) return <p role="status">Đang tải bài viết…</p>
  const status = post?.status ?? 'Draft'
  const canReview = can('cms.post.review')
  const canPublish = can('cms.post.publish')

  return (
    <>
      <Title
        actions={
          <>
            <button type="button" className={btnPrimary} onClick={() => void save()}>{status === 'Published' ? 'Lưu thay đổi' : 'Lưu nháp'}</button>
            {post && status === 'Draft' && <button type="button" className={btnGhost} onClick={() => transition('PendingReview')}>Gửi duyệt</button>}
            {post && status === 'PendingReview' && canReview && (
              <>
                <button type="button" className={btnGhost} onClick={() => transition('Approved')}>Duyệt</button>
                <button type="button" className={btnGhost} onClick={() => transition('Draft', { note: window.prompt('Lý do trả lại (không bắt buộc):') ?? undefined })}>Trả lại</button>
              </>
            )}
            {post && canPublish && status !== 'Published' && status !== 'Archived' && (
              <button type="button" className={btnDanger} onClick={() => transition('Published')}>Xuất bản</button>
            )}
            {post && canPublish && status === 'Published' && (
              <>
                <button type="button" className={btnGhost} onClick={() => transition('Draft')}>Gỡ xuống</button>
                <button type="button" className={btnGhost} onClick={() => transition('Archived')}>Lưu trữ</button>
              </>
            )}
          </>
        }
      >
        {post ? 'Sửa bài viết' : 'Bài viết mới'}
      </Title>
      <div className="mb-4 flex flex-wrap items-center gap-3 text-sm">
        <span className="rounded bg-slate-200 px-2 py-1">Trạng thái: {statusLabel[status]}</span>
        {post?.publishedUtc && <span>Xuất bản: {formatDateTime(post.publishedUtc)}</span>}
        {dirty && <span className="text-brand-red">Có thay đổi chưa lưu</span>}
        {post?.status === 'Published' && <a className="text-brand-blue underline" href={`/tin-tuc/${post.slug}`} target="_blank" rel="noreferrer">Xem trên trang</a>}
      </div>
      {post?.reviewNote && <Notice kind="info">Ghi chú duyệt: {post.reviewNote}</Notice>}
      {msg && <div className="mb-4"><Notice kind={msg.kind}>{msg.text}</Notice></div>}

      <div className="grid gap-6 xl:grid-cols-[1fr_320px]">
        <div className="space-y-4">
          <Field label="Tiêu đề"><input className={`${input} text-lg font-semibold`} value={form.title} onChange={(e) => set('title', e.target.value)} /></Field>
          <Field label="Đường dẫn" hint="Để trống để tự tạo từ tiêu đề (bỏ dấu). Đổi đường dẫn bài đã đăng sẽ tự chuyển hướng từ địa chỉ cũ.">
            <input className={input} value={form.slug} onChange={(e) => set('slug', e.target.value)} />
          </Field>
          <Field label="Tóm tắt"><textarea className={input} rows={3} value={form.summary} onChange={(e) => set('summary', e.target.value)} /></Field>
          <div>
            <span className="mb-1 block text-sm font-medium">Nội dung</span>
            <RichTextEditor label="Nội dung bài viết" initialHtml={initialBody} onChange={(html) => set('body', html)} />
          </div>
        </div>
        <div className="space-y-4">
          <Field label="Chuyên mục">
            <select className={input} value={form.categoryId} onChange={(e) => set('categoryId', e.target.value)}>
              <option value="">— Không chọn —</option>
              {cats.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
          </Field>
          <Field label="Thẻ" hint="Phân cách bằng dấu phẩy"><input className={input} value={form.tags} onChange={(e) => set('tags', e.target.value)} /></Field>
          <label className="flex items-center gap-2"><input type="checkbox" checked={form.isFeatured} onChange={(e) => set('isFeatured', e.target.checked)} /> Tin nổi bật</label>
          <Field label="Ảnh đại diện">
            {form.coverMediaId && (
              // eslint-disable-next-line @next/next/no-img-element
              <img src={`/media/${form.coverMediaId}/thumb`} alt="" className="mb-2 w-full rounded" />
            )}
            <input type="file" accept="image/*" onChange={(e) => e.target.files?.[0] && void uploadCover(e.target.files[0])} />
          </Field>
          <Field label="Tệp đính kèm">
            <ul className="mb-2 text-sm">
              {form.attachmentIds.map((a) => (
                <li key={a} className="flex items-center justify-between">
                  <a href={`/media/${a}`} target="_blank" rel="noreferrer" className="truncate text-brand-blue underline">{a.slice(0, 8)}…</a>
                  <button type="button" className="text-brand-red" onClick={() => set('attachmentIds', form.attachmentIds.filter((x) => x !== a))}>Gỡ</button>
                </li>
              ))}
            </ul>
            <input type="file" onChange={(e) => e.target.files?.[0] && void uploadAttachment(e.target.files[0])} />
          </Field>
          {post && canPublish && status !== 'Published' && (
            <Field label="Lên lịch xuất bản">
              <div className="flex gap-2">
                <input type="datetime-local" className={input} value={schedule} onChange={(e) => setSchedule(e.target.value)} />
                <button type="button" className={btnGhost} disabled={!schedule} onClick={() => transition('Scheduled', { scheduledUtc: new Date(schedule).toISOString() })}>Lên lịch</button>
              </div>
            </Field>
          )}
        </div>
      </div>

      {conflict && (
        <Dialog title="Xung đột chỉnh sửa" onClose={() => setConflict(false)}>
          <p>Bài viết đã được người khác sửa sau khi bạn mở. Để tránh ghi đè, thay đổi của bạn chưa được lưu.</p>
          <p className="mt-2 text-sm text-slate-600">Hãy sao chép phần cần giữ, sau đó tải lại bản mới nhất.</p>
          <div className="mt-4 flex justify-end gap-2">
            <button type="button" className={btnGhost} onClick={() => setConflict(false)}>Đóng</button>
            <button type="button" className={btnPrimary} onClick={() => { setConflict(false); load() }}>Tải lại bản mới nhất</button>
          </div>
        </Dialog>
      )}
    </>
  )
}
