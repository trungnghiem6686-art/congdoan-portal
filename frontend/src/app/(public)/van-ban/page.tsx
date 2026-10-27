import type { Metadata } from 'next'
import Link from 'next/link'
import { Breadcrumb, PageTitle, Pagination, pageParam, str } from '@/components/site/Common'
import { getDocumentTypes, listDocuments } from '@/lib/public'
import { formatDay } from '@/lib/format'

export const metadata: Metadata = { title: 'Văn bản' }

const input = 'w-full rounded border border-slate-300 bg-white px-3 py-2'

export default async function DocumentsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const sp = await searchParams
  const page = pageParam(sp.trang)
  const filter = { q: str(sp.q), number: str(sp.number), docType: str(sp.docType), from: str(sp.from), to: str(sp.to) }
  const [docs, types] = await Promise.all([listDocuments({ ...filter, page: String(page), pageSize: '20' }), getDocumentTypes()])
  return (
    <>
      <Breadcrumb items={[{ label: 'Văn bản' }]} />
      <PageTitle>Văn bản</PageTitle>
      <form className="mb-6 grid gap-3 rounded bg-white p-4 sm:grid-cols-2 lg:grid-cols-6" role="search">
        <label className="lg:col-span-2"><span className="mb-1 block text-sm font-medium">Từ khóa</span><input className={input} name="q" defaultValue={filter.q} /></label>
        <label><span className="mb-1 block text-sm font-medium">Số hiệu</span><input className={input} name="number" defaultValue={filter.number} /></label>
        <label>
          <span className="mb-1 block text-sm font-medium">Loại văn bản</span>
          <select className={input} name="docType" defaultValue={filter.docType ?? ''}>
            <option value="">Tất cả</option>
            {types.map((t) => <option key={t}>{t}</option>)}
          </select>
        </label>
        <label><span className="mb-1 block text-sm font-medium">Từ ngày</span><input className={input} type="date" name="from" defaultValue={filter.from} /></label>
        <label><span className="mb-1 block text-sm font-medium">Đến ngày</span><input className={input} type="date" name="to" defaultValue={filter.to} /></label>
        <div className="flex gap-2 lg:col-span-6">
          <button className="rounded bg-brand-navy px-4 py-2 font-semibold text-white hover:bg-brand-blue">Tìm văn bản</button>
          <Link href="/van-ban" className="rounded border border-slate-300 px-4 py-2">Xóa lọc</Link>
        </div>
      </form>
      {docs.items.length === 0 ? (
        <p className="rounded bg-white p-6">Không có văn bản phù hợp.</p>
      ) : (
        <div className="overflow-x-auto rounded bg-white">
          <table className="w-full min-w-[640px] text-left">
            <thead className="bg-brand-navy text-white">
              <tr><th className="p-3">Số hiệu</th><th className="p-3">Trích yếu</th><th className="p-3">Loại</th><th className="p-3">Ngày ban hành</th></tr>
            </thead>
            <tbody>
              {docs.items.map((d) => (
                <tr key={d.id} className="border-b border-slate-200 align-top">
                  <td className="p-3 font-medium">{d.number}</td>
                  <td className="p-3"><Link href={`/van-ban/${d.slug}`} className="text-brand-blue hover:underline">{d.title}</Link></td>
                  <td className="p-3">{d.docType}</td>
                  <td className="p-3 whitespace-nowrap">{formatDay(d.issuedDate)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <Pagination page={page} pageSize={20} total={docs.total} basePath="/van-ban" query={filter} />
    </>
  )
}
