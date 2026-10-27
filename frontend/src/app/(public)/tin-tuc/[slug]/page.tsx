import type { Metadata } from 'next'
import Link from 'next/link'
import { notFound } from 'next/navigation'
import { Paperclip } from 'lucide-react'
import { Breadcrumb } from '@/components/site/Common'
import { PrintButton } from '@/components/site/PrintButton'
import { PostList } from '@/components/site/Common'
import { getPost } from '@/lib/public'
import { formatDateTime, formatSize } from '@/lib/format'
import { safeHtml } from '@/lib/sanitize'

type Props = { params: Promise<{ slug: string }> }

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const post = await getPost((await params).slug)
  return post ? { title: post.title, description: post.summary ?? undefined, openGraph: { title: post.title, images: post.coverUrl ? [post.coverUrl] : [] } } : {}
}

export default async function ArticlePage({ params }: Props) {
  const post = await getPost((await params).slug)
  if (!post) notFound()
  return (
    <div className="grid gap-8 lg:grid-cols-3">
      <article className="lg:col-span-2">
        <Breadcrumb items={[...(post.categoryName ? [{ label: post.categoryName, href: `/chuyen-muc/${post.categorySlug}` }] : [{ label: 'Tin tức', href: '/tin-tuc' }]), { label: post.title }]} />
        <h1 className="text-2xl font-bold leading-snug text-brand-navy md:text-3xl">{post.title}</h1>
        <div className="mt-2 flex flex-wrap items-center justify-between gap-2 text-sm text-slate-600">
          <time dateTime={post.publishedUtc}>{formatDateTime(post.publishedUtc)}</time>
          <PrintButton />
        </div>
        {post.summary && <p className="mt-4 text-lg font-semibold text-slate-800">{post.summary}</p>}
        <div className="prose mt-4 max-w-none prose-img:rounded prose-figcaption:text-center" dangerouslySetInnerHTML={{ __html: safeHtml(post.body) }} />
        {post.attachments.length > 0 && (
          <section className="mt-6 rounded bg-white p-4">
            <h2 className="mb-2 font-semibold text-brand-navy">Tệp đính kèm</h2>
            <ul className="space-y-1">
              {post.attachments.map((a) => (
                <li key={a.id}>
                  <a href={a.url} className="inline-flex items-center gap-1 text-brand-blue hover:underline">
                    <Paperclip aria-hidden className="h-4 w-4" /> {a.name} ({formatSize(a.size)})
                  </a>
                </li>
              ))}
            </ul>
          </section>
        )}
        {post.tags.length > 0 && (
          <p className="mt-4 text-sm text-slate-600">Từ khóa: {post.tags.join(', ')}</p>
        )}
      </article>
      <aside className="no-print">
        <h2 className="mb-3 border-b-2 border-brand-navy pb-1 font-bold uppercase text-brand-navy">Tin cùng chuyên mục</h2>
        <PostList posts={post.related} empty="Chưa có tin liên quan." />
        <Link href="/tin-tuc" className="mt-3 inline-block text-brand-blue hover:underline">Tất cả tin tức →</Link>
      </aside>
    </div>
  )
}
