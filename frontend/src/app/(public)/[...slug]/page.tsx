import type { Metadata } from 'next'
import Link from 'next/link'
import { notFound, permanentRedirect, redirect } from 'next/navigation'
import { Breadcrumb, PageTitle, Pagination, PostList, pageParam } from '@/components/site/Common'
import { BlockRenderer } from '@/components/blocks/BlockRenderer'
import { getPage, listPosts, resolvePath } from '@/lib/public'
import { safeHtml } from '@/lib/sanitize'

type Props = { params: Promise<{ slug: string[] }>; searchParams: Promise<Record<string, string | string[] | undefined>> }

/** Menu item paths ("/{slug}") and old URLs (permanent redirects) resolved by the backend. */
export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const path = '/' + (await params).slug.join('/')
  const r = await resolvePath(path)
  return r.type === 'menu' && r.title ? { title: r.title, alternates: { canonical: path } } : {}
}

export default async function ResolvedPage({ params, searchParams }: Props) {
  const path = '/' + (await params).slug.map(encodeURIComponent).join('/')
  const r = await resolvePath(decodeURIComponent(path))
  if (r.type === 'redirect' && r.location) permanentRedirect(r.location)
  if (r.type !== 'menu') notFound()

  const target = r.target
  if (target?.type === 'url' && target.url) redirect(target.url)
  if (target?.type === 'post' && target.slug) redirect(`/tin-tuc/${target.slug}`)

  const title = r.title ?? ''
  if (target?.type === 'category' && target.slug) {
    const page = pageParam((await searchParams).trang)
    const posts = await listPosts({ category: target.slug, page, pageSize: 12 })
    return (
      <>
        <Breadcrumb items={[{ label: title }]} />
        <PageTitle>{title}</PageTitle>
        <PostList posts={posts.items} empty="Mục này chưa có bài viết." />
        <Pagination page={page} pageSize={12} total={posts.total} basePath={path} />
      </>
    )
  }
  if (target?.type === 'page' && target.slug) {
    const page = await getPage(target.slug)
    if (!page) notFound()
    return (
      <>
        <Breadcrumb items={[{ label: title }]} />
        <PageTitle>{page.title}</PageTitle>
        {page.body && <div className="prose mb-8 max-w-none" dangerouslySetInnerHTML={{ __html: safeHtml(page.body) }} />}
        <BlockRenderer blocks={page.sections} />
      </>
    )
  }
  return (
    <>
      <Breadcrumb items={[{ label: title }]} />
      <PageTitle>{title}</PageTitle>
      {r.children && r.children.length > 0 ? (
        <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {r.children.map((c) => (
            <li key={c.id}><Link href={c.href} className="block rounded bg-white p-4 font-semibold hover:text-brand-blue">{c.label}</Link></li>
          ))}
        </ul>
      ) : (
        <p className="rounded bg-white p-6">Mục này chưa có nội dung.</p>
      )}
    </>
  )
}
