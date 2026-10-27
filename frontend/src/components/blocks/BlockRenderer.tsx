import type { AlbumItem, BannerItem, DocumentItem, ExamNotice, PostSummary } from '@/lib/types'
import { FeaturedNews, NewsGrid, NewsList } from './NewsBlocks'
import { BannerBlock, DocumentsBlock, ExamNoticeBlock, GalleryBlock, IntroBlock, LinksBlock } from './OtherBlocks'

export const BLOCK_TYPES = [
  'featured_news', 'news_list', 'news_grid', 'banner', 'quick_links', 'documents', 'gallery', 'exam_notice', 'intro_text', 'links',
] as const

export type Block = {
  id: string
  type: string
  title?: string | null
  /** Validated by the backend per block type. */
  config: Record<string, any>
  data: Record<string, any> | null
}

function One({ block }: { block: Block }) {
  const { type, title, config, data } = block
  switch (type) {
    case 'featured_news':
      return <FeaturedNews type={type} title={title} config={config} posts={(data?.posts as PostSummary[]) ?? []} />
    case 'news_list':
      return <NewsList type={type} title={title} config={config} posts={(data?.posts as PostSummary[]) ?? []} />
    case 'news_grid':
      return <NewsGrid type={type} title={title} config={config} posts={(data?.posts as PostSummary[]) ?? []} />
    case 'banner':
      return <BannerBlock type={type} title={title} config={config} banners={(data?.banners as BannerItem[]) ?? []} />
    case 'quick_links':
    case 'links':
      return <LinksBlock type={type} title={title} config={config} />
    case 'documents':
      return <DocumentsBlock type={type} title={title} config={config} documents={(data?.documents as DocumentItem[]) ?? []} />
    case 'gallery':
      return <GalleryBlock type={type} title={title} albums={(data?.albums as AlbumItem[]) ?? []} />
    case 'exam_notice':
      return <ExamNoticeBlock type={type} title={title} exams={(data?.exams as ExamNotice[]) ?? []} />
    case 'intro_text':
      return <IntroBlock type={type} title={title} config={config} />
    default:
      // Unknown types (e.g. from a newer backend) are skipped rather than breaking the page.
      return null
  }
}

/** Renders a resolved layout (homepage or static page). Empty blocks render nothing. */
export function BlockRenderer({ blocks }: { blocks: Block[] }) {
  return (
    <>
      {blocks.map((b) => (
        <One key={b.id} block={b} />
      ))}
    </>
  )
}
