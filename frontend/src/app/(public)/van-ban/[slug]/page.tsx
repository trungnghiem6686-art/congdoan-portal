import type { Metadata } from 'next'
import { notFound } from 'next/navigation'
import { Download } from 'lucide-react'
import { Breadcrumb } from '@/components/site/Common'
import { getDocument } from '@/lib/public'
import { formatDay } from '@/lib/format'

type Props = { params: Promise<{ slug: string }> }

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const d = await getDocument((await params).slug)
  return d ? { title: `${d.number} – ${d.title}`, alternates: { canonical: `/van-ban/${d.slug}` } } : {}
}

export default async function DocumentPage({ params }: Props) {
  const d = await getDocument((await params).slug)
  if (!d) notFound()
  const rows: [string, string | null | undefined][] = [
    ['Số hiệu', d.number], ['Loại văn bản', d.docType], ['Cơ quan ban hành', d.issuer],
    ['Ngày ban hành', formatDay(d.issuedDate)], ['Ngày hiệu lực', d.effectiveDate ? formatDay(d.effectiveDate) : null],
  ]
  return (
    <>
      <Breadcrumb items={[{ label: 'Văn bản', href: '/van-ban' }, { label: d.number }]} />
      <h1 className="mb-4 text-2xl font-bold text-brand-navy">{d.title}</h1>
      <dl className="grid gap-x-6 gap-y-2 rounded bg-white p-4 sm:grid-cols-[180px_1fr]">
        {rows.filter(([, v]) => v).map(([k, v]) => (
          <div key={k} className="contents"><dt className="font-medium text-slate-600">{k}</dt><dd>{v}</dd></div>
        ))}
      </dl>
      {d.summary && <p className="mt-4 whitespace-pre-line">{d.summary}</p>}
      {d.fileUrl && (
        <a href={d.fileUrl} className="mt-4 inline-flex items-center gap-2 rounded bg-brand-navy px-4 py-2 font-semibold text-white hover:bg-brand-blue">
          <Download aria-hidden className="h-4 w-4" /> Tải văn bản
        </a>
      )}
    </>
  )
}
