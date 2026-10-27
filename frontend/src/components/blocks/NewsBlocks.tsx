import Link from 'next/link'
import type { PostSummary } from '@/lib/types'
import { Cover, PostMeta, SectionShell, postHref } from './parts'

type Props = { type: string; title?: string | null; config: { layout?: string; columns?: number; categorySlug?: string | null }; posts: PostSummary[] }

const moreFor = (config: Props['config']) => ({ href: config.categorySlug ? `/chuyen-muc/${config.categorySlug}` : '/tin-tuc' })

export function FeaturedNews({ type, title, config, posts }: Props) {
  if (posts.length === 0) return null
  const [main, ...rest] = posts
  return (
    <SectionShell type={type} title={title} more={moreFor(config)}>
      <div className="grid gap-6 lg:grid-cols-3">
        <article className="lg:col-span-2">
          <Link href={postHref(main)} className="group block">
            <Cover src={main.coverUrl} alt={main.title} className="aspect-[16/9] w-full rounded" />
            <h3 className="mt-3 text-2xl font-bold leading-snug text-brand-navy group-hover:text-brand-blue">{main.title}</h3>
          </Link>
          <PostMeta post={main} />
          {main.summary && <p className="mt-2 text-base text-slate-700">{main.summary}</p>}
        </article>
        {config.layout !== 'hero' || rest.length > 0 ? (
          <ul className="divide-y divide-slate-200">
            {rest.map((p) => (
              <li key={p.id} className="py-3 first:pt-0">
                <Link href={postHref(p)} className="flex gap-3 hover:text-brand-blue">
                  <Cover src={p.coverUrl} alt="" className="h-16 w-24 shrink-0 rounded" />
                  <span className="font-semibold leading-snug">{p.title}</span>
                </Link>
              </li>
            ))}
          </ul>
        ) : null}
      </div>
    </SectionShell>
  )
}

export function NewsList({ type, title, config, posts }: Props) {
  if (posts.length === 0) return null
  const compact = config.layout === 'compact'
  return (
    <SectionShell type={type} title={title} more={moreFor(config)}>
      <ul className="divide-y divide-slate-200">
        {posts.map((p) => (
          <li key={p.id} className="py-3">
            <Link href={postHref(p)} className="flex gap-4 hover:text-brand-blue">
              {!compact && <Cover src={p.coverUrl} alt="" className="h-20 w-32 shrink-0 rounded" />}
              <div>
                <h3 className="font-semibold leading-snug">{p.title}</h3>
                <PostMeta post={p} />
                {!compact && p.summary && <p className="mt-1 line-clamp-2 text-slate-700">{p.summary}</p>}
              </div>
            </Link>
          </li>
        ))}
      </ul>
    </SectionShell>
  )
}

export function NewsGrid({ type, title, config, posts }: Props) {
  if (posts.length === 0) return null
  const cols = { 2: 'sm:grid-cols-2', 3: 'sm:grid-cols-2 lg:grid-cols-3', 4: 'sm:grid-cols-2 lg:grid-cols-4' }[config.columns ?? 3] ?? 'sm:grid-cols-3'
  return (
    <SectionShell type={type} title={title} more={moreFor(config)}>
      <div className={`grid gap-5 ${cols}`}>
        {posts.map((p) => (
          <article key={p.id} className={config.layout === 'cards' ? 'rounded bg-white p-3 shadow-sm' : ''}>
            <Link href={postHref(p)} className="group block">
              <Cover src={p.coverUrl} alt="" className="aspect-[4/3] w-full rounded" />
              <h3 className="mt-2 font-semibold leading-snug group-hover:text-brand-blue">{p.title}</h3>
            </Link>
            <PostMeta post={p} />
          </article>
        ))}
      </div>
    </SectionShell>
  )
}
