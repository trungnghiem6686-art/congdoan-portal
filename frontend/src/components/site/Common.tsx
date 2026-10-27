import Link from 'next/link'
import type { PostSummary } from '@/lib/types'
import { Cover, PostMeta, postHref } from '@/components/blocks/parts'

export type Crumb = { label: string; href?: string }

export function Breadcrumb({ items }: { items: Crumb[] }) {
  return (
    <nav aria-label="Đường dẫn" className="mb-4 text-sm text-slate-600">
      <ol className="flex flex-wrap items-center gap-1">
        <li><Link href="/" className="hover:text-brand-blue">Trang chủ</Link></li>
        {items.map((c) => (
          <li key={c.label} className="flex items-center gap-1">
            <span aria-hidden>›</span>
            {c.href ? <Link href={c.href} className="hover:text-brand-blue">{c.label}</Link> : <span aria-current="page">{c.label}</span>}
          </li>
        ))}
      </ol>
    </nav>
  )
}

export function PageTitle({ children }: { children: React.ReactNode }) {
  return <h1 className="mb-6 border-b-2 border-brand-navy pb-2 text-2xl font-bold text-brand-navy md:text-3xl">{children}</h1>
}

export function PostList({ posts, empty = 'Chưa có bài viết.' }: { posts: PostSummary[]; empty?: string }) {
  if (posts.length === 0) return <p className="rounded bg-white p-6 text-slate-700">{empty}</p>
  return (
    <ul className="divide-y divide-slate-200 rounded bg-white">
      {posts.map((p) => (
        <li key={p.id} data-post-item className="p-4">
          <Link href={postHref(p)} className="flex gap-4 hover:text-brand-blue">
            <Cover src={p.coverUrl} alt="" className="hidden h-24 w-36 shrink-0 rounded sm:block" />
            <div className="min-w-0">
              <h2 className="text-lg font-semibold leading-snug">{p.title}</h2>
              <PostMeta post={p} />
              {p.summary && <p className="mt-1 line-clamp-2 text-slate-700">{p.summary}</p>}
            </div>
          </Link>
        </li>
      ))}
    </ul>
  )
}

/** Page links keep every other query parameter; page 1 has no parameter. */
export function Pagination({ page, pageSize, total, basePath, query = {} }: { page: number; pageSize: number; total: number; basePath: string; query?: Record<string, string | undefined> }) {
  const pages = Math.max(1, Math.ceil(total / pageSize))
  if (pages <= 1) return null
  const href = (n: number) => {
    const s = new URLSearchParams(Object.entries(query).filter(([, v]) => v) as [string, string][])
    if (n > 1) s.set('trang', String(n))
    const qs = s.toString()
    return qs ? `${basePath}?${qs}` : basePath
  }
  return (
    <nav aria-label="Phân trang" className="mt-6 flex flex-wrap items-center justify-center gap-2">
      {page > 1 && <Link className="rounded bg-white px-3 py-2 shadow-sm hover:text-brand-blue" href={href(page - 1)}>Trang trước</Link>}
      <span className="px-3 py-2 text-slate-700">Trang {page}/{pages}</span>
      {page < pages && <Link className="rounded bg-white px-3 py-2 shadow-sm hover:text-brand-blue" href={href(page + 1)}>Trang sau</Link>}
    </nav>
  )
}

export const pageParam = (v?: string | string[]) => Math.max(1, Number(Array.isArray(v) ? v[0] : v) || 1)
export const str = (v?: string | string[]) => (Array.isArray(v) ? v[0] : v) || undefined
