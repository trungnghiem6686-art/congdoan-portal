export type PostSummary = {
  id: string
  title: string
  slug: string
  summary?: string | null
  coverUrl?: string | null
  categoryName?: string | null
  categorySlug?: string | null
  publishedUtc: string
  isFeatured?: boolean
}

export type PostDetail = PostSummary & {
  body: string
  tags: string[]
  attachments: { id: string; name: string; contentType: string; size: number; url: string }[]
  updatedUtc: string
  related: PostSummary[]
}

export type BannerItem = { id: string; title: string; imageUrl?: string | null; linkUrl?: string | null; description?: string | null }
export type DocumentItem = {
  id: string
  number: string
  title: string
  slug: string
  docType: string
  issuer?: string | null
  issuedDate: string
  effectiveDate?: string | null
  summary?: string | null
  fileUrl?: string | null
}
export type AlbumItem = { id: string; title: string; slug: string; kind: string; coverUrl?: string | null; description?: string | null }
export type ExamNotice = { id: string; title: string; opensAtUtc: string; closesAtUtc: string; durationMinutes: number; phase: string }
export type LinkItem = { label: string; url: string; icon?: string | null }

export type MenuItem = { id: string; label: string; href: string; openInNewTab: boolean; children: MenuItem[] }
export type Menu = { code: string; name: string; items: MenuItem[] }

export type SiteSettings = {
  site: { name: string; shortName?: string | null; tagline?: string | null; logoUrl?: string | null }
  contact: { organization?: string | null; address?: string | null; phone?: string | null; email?: string | null; workingHours?: string | null }
  footer: { copyright?: string | null; columns: { title: string; links: LinkItem[] }[]; note?: string | null }
  social: { links: { platform: string; url: string }[] }
  utility: { links: LinkItem[] }
  seo: { description?: string | null; keywords?: string | null }
}
