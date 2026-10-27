import Link from 'next/link'
import { ArrowRight, CalendarClock, FileText, Images, Link2 } from 'lucide-react'
import type { AlbumItem, BannerItem, DocumentItem, ExamNotice, LinkItem } from '@/lib/types'
import { formatDateTime, formatDay } from '@/lib/format'
import { safeHtml } from '@/lib/sanitize'
import { Cover, SectionShell } from './parts'

const external = (url: string) => /^https?:\/\//.test(url)

function SmartLink({ href, className, children }: { href: string; className?: string; children: React.ReactNode }) {
  return external(href) ? (
    <a href={href} className={className} target="_blank" rel="noopener noreferrer">
      {children}
    </a>
  ) : (
    <Link href={href} className={className}>
      {children}
    </Link>
  )
}

export function BannerBlock({ type, title, config, banners }: { type: string; title?: string | null; config: { layout?: string }; banners: BannerItem[] }) {
  if (banners.length === 0) return null
  return (
    <SectionShell type={type} title={title}>
      {/* Static stack: no auto-rotating carousel (WCAG 2.2.2). */}
      <div className={config.layout === 'strip' ? 'grid gap-3 sm:grid-cols-2' : 'space-y-3'}>
        {banners.map((b) => {
          const img = <Cover src={b.imageUrl} alt={b.title} className={config.layout === 'strip' ? 'h-24 w-full rounded' : 'max-h-64 w-full rounded'} />
          return b.linkUrl ? (
            <SmartLink key={b.id} href={b.linkUrl} className="block">
              {img}
            </SmartLink>
          ) : (
            <div key={b.id}>{img}</div>
          )
        })}
      </div>
    </SectionShell>
  )
}

export function LinksBlock({ type, title, config }: { type: string; title?: string | null; config: { items?: LinkItem[]; layout?: string } }) {
  const items = config.items ?? []
  if (items.length === 0) return null
  const tiles = type === 'quick_links'
  return (
    <SectionShell type={type} title={title}>
      <ul className={tiles ? 'grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6' : 'grid gap-2 sm:grid-cols-2'}>
        {items.map((l) => (
          <li key={l.label + l.url}>
            <SmartLink
              href={l.url}
              className={
                tiles
                  ? 'flex min-h-[64px] items-center gap-2 rounded bg-brand-navy px-3 py-3 font-semibold text-white hover:bg-brand-blue'
                  : 'flex items-center gap-2 py-1 text-brand-blue hover:underline'
              }
            >
              <Link2 aria-hidden className="h-4 w-4 shrink-0" />
              {l.label}
            </SmartLink>
          </li>
        ))}
      </ul>
    </SectionShell>
  )
}

export function DocumentsBlock({ type, title, config, documents }: { type: string; title?: string | null; config: { layout?: string }; documents: DocumentItem[] }) {
  if (documents.length === 0) return null
  return (
    <SectionShell type={type} title={title} more={{ href: '/van-ban', label: 'Tất cả văn bản' }}>
      <ul className="divide-y divide-slate-200 rounded bg-white">
        {documents.map((d) => (
          <li key={d.id} className="flex gap-3 p-3">
            <FileText aria-hidden className="mt-0.5 h-5 w-5 shrink-0 text-brand-red" />
            <div>
              <Link href={`/van-ban/${d.slug}`} className="font-semibold hover:text-brand-blue">
                {d.number} – {d.title}
              </Link>
              <p className="text-sm text-slate-600">
                {d.docType} · Ban hành {formatDay(d.issuedDate)}
              </p>
            </div>
          </li>
        ))}
      </ul>
    </SectionShell>
  )
}

export function GalleryBlock({ type, title, albums }: { type: string; title?: string | null; albums: AlbumItem[] }) {
  if (albums.length === 0) return null
  return (
    <SectionShell type={type} title={title} more={{ href: '/thu-vien', label: 'Thư viện' }}>
      <ul className="grid grid-cols-2 gap-4 md:grid-cols-3">
        {albums.map((a) => (
          <li key={a.id}>
            <Link href={`/thu-vien/${a.slug}`} className="group block">
              <Cover src={a.coverUrl} alt="" className="aspect-[4/3] w-full rounded" />
              <span className="mt-2 flex items-center gap-1 font-semibold group-hover:text-brand-blue">
                <Images aria-hidden className="h-4 w-4" /> {a.title}
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </SectionShell>
  )
}

const phaseLabel: Record<string, string> = { open: 'Đang mở', upcoming: 'Sắp mở', closed: 'Đã đóng' }

export function ExamNoticeBlock({ type, title, exams }: { type: string; title?: string | null; exams: ExamNotice[] }) {
  if (exams.length === 0) return null
  return (
    <SectionShell type={type} title={title} more={{ href: '/thi', label: 'Vào cổng thi' }}>
      <ul className="space-y-3">
        {exams.map((e) => (
          <li key={e.id} className="flex flex-wrap items-center justify-between gap-3 rounded border-l-4 border-brand-red bg-white p-4">
            <div>
              <p className="font-semibold text-brand-navy">{e.title}</p>
              <p className="flex items-center gap-1 text-sm text-slate-600">
                <CalendarClock aria-hidden className="h-4 w-4" />
                {formatDateTime(e.opensAtUtc)} – {formatDateTime(e.closesAtUtc)} · {e.durationMinutes} phút
              </p>
            </div>
            <Link href="/thi" className="inline-flex items-center gap-1 rounded bg-brand-red px-4 py-2 text-sm font-semibold text-white hover:bg-red-800">
              {phaseLabel[e.phase] ?? 'Chi tiết'} <ArrowRight aria-hidden className="h-4 w-4" />
            </Link>
          </li>
        ))}
      </ul>
    </SectionShell>
  )
}

export function IntroBlock({ type, title, config }: { type: string; title?: string | null; config: { html?: string; layout?: string } }) {
  const html = safeHtml(config.html)
  if (!html) return null
  return (
    <SectionShell type={type} title={title}>
      <div className={`prose max-w-none ${config.layout === 'boxed' ? 'rounded bg-white p-5 shadow-sm' : ''}`} dangerouslySetInnerHTML={{ __html: html }} />
    </SectionShell>
  )
}
