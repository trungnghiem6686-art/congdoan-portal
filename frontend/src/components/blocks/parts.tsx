import Link from 'next/link'
import type { ReactNode } from 'react'
import type { PostSummary } from '@/lib/types'
import { formatDate } from '@/lib/format'

export function SectionShell({ type, title, more, children }: { type: string; title?: string | null; more?: { href: string; label?: string }; children: ReactNode }) {
  return (
    <section data-block-type={type} className="mb-8" aria-label={title ?? undefined}>
      {title && (
        <div className="mb-4 flex items-end justify-between border-b-2 border-brand-navy">
          <h2 className="-mb-0.5 inline-block border-b-2 border-brand-red pb-1 text-lg font-bold uppercase text-brand-navy">{title}</h2>
          {more && (
            <Link href={more.href} className="mb-1 text-sm font-medium text-brand-blue hover:underline">
              {more.label ?? 'Xem thêm'} →
            </Link>
          )}
        </div>
      )}
      {children}
    </section>
  )
}

export function Cover({ src, alt, className = '' }: { src?: string | null; alt: string; className?: string }) {
  if (!src) return <div aria-hidden className={`flex items-center justify-center bg-brand-navy/10 text-brand-navy/40 ${className}`}>CĐ</div>
  // eslint-disable-next-line @next/next/no-img-element -- media is served by our own backend with its own thumbnails
  return <img src={src} alt={alt} loading="lazy" className={`object-cover ${className}`} />
}

export function PostMeta({ post }: { post: PostSummary }) {
  return (
    <p className="mt-1 text-sm text-slate-600">
      {post.categoryName && <span className="font-medium text-brand-blue">{post.categoryName}</span>}
      {post.categoryName && ' · '}
      <time dateTime={post.publishedUtc}>{formatDate(post.publishedUtc)}</time>
    </p>
  )
}

export const postHref = (p: PostSummary) => `/tin-tuc/${p.slug}`
