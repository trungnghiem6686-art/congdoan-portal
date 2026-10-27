import type { Metadata } from 'next'
import { Breadcrumb, PageTitle, Pagination, PostList, pageParam } from '@/components/site/Common'
import { listPosts } from '@/lib/public'

export const metadata: Metadata = { title: 'Tin tức' }

export default async function NewsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const page = pageParam((await searchParams).trang)
  const posts = await listPosts({ page, pageSize: 12 })
  return (
    <>
      <Breadcrumb items={[{ label: 'Tin tức' }]} />
      <PageTitle>Tin tức</PageTitle>
      <PostList posts={posts.items} />
      <Pagination page={page} pageSize={12} total={posts.total} basePath="/tin-tuc" />
    </>
  )
}
