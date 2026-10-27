import type { Metadata } from 'next'
import { notFound } from 'next/navigation'
import { Breadcrumb, PageTitle, Pagination, PostList, pageParam } from '@/components/site/Common'
import { getCategories, listPosts } from '@/lib/public'

type Props = { params: Promise<{ slug: string }>; searchParams: Promise<Record<string, string | string[] | undefined>> }

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params
  const cat = (await getCategories()).find((c) => c.slug === slug)
  return cat ? { title: cat.name, description: cat.description ?? undefined } : {}
}

export default async function CategoryPage({ params, searchParams }: Props) {
  const { slug } = await params
  const page = pageParam((await searchParams).trang)
  const cat = (await getCategories()).find((c) => c.slug === slug)
  if (!cat) notFound()
  const posts = await listPosts({ category: slug, page, pageSize: 12 })
  return (
    <>
      <Breadcrumb items={[{ label: cat.name }]} />
      <PageTitle>{cat.name}</PageTitle>
      {cat.description && <p className="-mt-3 mb-5 text-slate-700">{cat.description}</p>}
      <PostList posts={posts.items} empty="Chuyên mục chưa có bài viết." />
      <Pagination page={page} pageSize={12} total={posts.total} basePath={`/chuyen-muc/${slug}`} />
    </>
  )
}
