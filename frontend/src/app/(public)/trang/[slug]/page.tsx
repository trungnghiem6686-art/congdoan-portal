import type { Metadata } from 'next'
import { notFound } from 'next/navigation'
import { BlockRenderer } from '@/components/blocks/BlockRenderer'
import { Breadcrumb, PageTitle } from '@/components/site/Common'
import { getPage } from '@/lib/public'
import { safeHtml } from '@/lib/sanitize'

type Props = { params: Promise<{ slug: string }> }

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const p = await getPage((await params).slug)
  return p ? { title: p.title, description: p.seoDescription ?? undefined, alternates: { canonical: `/trang/${p.slug}` } } : {}
}

export default async function StaticPage({ params }: Props) {
  const page = await getPage((await params).slug)
  if (!page) notFound()
  return (
    <>
      <Breadcrumb items={[{ label: page.title }]} />
      <PageTitle>{page.title}</PageTitle>
      {page.body && <div className="prose mb-8 max-w-none" dangerouslySetInnerHTML={{ __html: safeHtml(page.body) }} />}
      <BlockRenderer blocks={page.sections} />
    </>
  )
}
