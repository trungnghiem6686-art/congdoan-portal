import type { Metadata } from 'next'
import Link from 'next/link'
import { Breadcrumb, PageTitle, Pagination, pageParam, str } from '@/components/site/Common'
import { search } from '@/lib/public'
import { formatDate } from '@/lib/format'

export const metadata: Metadata = { title: 'Tìm kiếm' }

const typeLabel: Record<string, string> = { post: 'Tin bài', page: 'Trang', document: 'Văn bản', album: 'Thư viện' }

export default async function SearchPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const sp = await searchParams
  const q = (str(sp.q) ?? '').trim()
  const type = str(sp.loai)
  const page = pageParam(sp.trang)
  const results = q ? await search(q, type, page) : null
  return (
    <>
      <Breadcrumb items={[{ label: 'Tìm kiếm' }]} />
      <PageTitle>Tìm kiếm</PageTitle>
      <form role="search" className="mb-6 flex flex-wrap gap-2">
        <label htmlFor="search-q" className="sr-only">Từ khóa</label>
        <input id="search-q" name="q" defaultValue={q} placeholder="Nhập từ khóa (có dấu hoặc không dấu)" className="min-w-0 flex-1 rounded border border-slate-300 bg-white px-3 py-2" />
        <select name="loai" defaultValue={type ?? ''} aria-label="Loại nội dung" className="rounded border border-slate-300 bg-white px-3 py-2">
          <option value="">Tất cả</option>
          {Object.entries(typeLabel).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
        </select>
        <button className="rounded bg-brand-navy px-4 py-2 font-semibold text-white hover:bg-brand-blue">Tìm</button>
      </form>
      {!q ? (
        <p className="rounded bg-white p-6">Nhập từ khóa để tìm tin bài, văn bản, trang và thư viện.</p>
      ) : results!.items.length === 0 ? (
        <div className="rounded bg-white p-6">
          <p className="font-semibold">Không tìm thấy kết quả phù hợp với “{q}”.</p>
          <p className="mt-1 text-slate-700">Thử từ khóa ngắn hơn, bỏ bớt từ, hoặc tìm theo số hiệu văn bản.</p>
        </div>
      ) : (
        <>
          <p className="mb-3 text-slate-700">Tìm thấy {results!.total} kết quả.</p>
          <ul className="divide-y divide-slate-200 rounded bg-white">
            {results!.items.map((r) => (
              <li key={r.url} className="p-4">
                <span className="text-xs font-semibold uppercase text-brand-red">{typeLabel[r.type] ?? r.type}</span>
                <Link href={r.url} className="block text-lg font-semibold hover:text-brand-blue">{r.title}</Link>
                {r.snippet && <p className="text-slate-700">{r.snippet}</p>}
                {r.date && <p className="text-sm text-slate-500">{formatDate(r.date)}</p>}
              </li>
            ))}
          </ul>
          <Pagination page={page} pageSize={10} total={results!.total} basePath="/tim-kiem" query={{ q, loai: type }} />
        </>
      )}
    </>
  )
}
