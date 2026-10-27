import { api, ApiClientError, type Paged } from './api'
import type { AlbumItem, DocumentItem, Menu, PostDetail, PostSummary, SiteSettings } from './types'
import type { Block } from '@/components/blocks/BlockRenderer'

/** Server-side reads for public pages; each is cached under the tag the backend invalidates on publish. */
export const getSite = () => api<SiteSettings>('/api/public/site', { tags: ['site'] })
export const getMenus = () => api<Menu[]>('/api/public/menus', { tags: ['menus'] })
export const getHomepage = () =>
  api<{ version: number; sections: Block[] }>('/api/public/homepage', { tags: ['homepage', 'posts', 'banners', 'documents', 'albums'] })

export async function orNull<T>(p: Promise<T>): Promise<T | null> {
  try {
    return await p
  } catch (e) {
    if (e instanceof ApiClientError && e.status === 404) return null
    throw e
  }
}

export const getPost = (slug: string) => orNull(api<PostDetail>(`/api/public/posts/${encodeURIComponent(slug)}`, { tags: ['posts'] }))

export const listPosts = (q: { category?: string; tag?: string; page?: number; pageSize?: number }) => {
  const s = new URLSearchParams()
  if (q.category) s.set('category', q.category)
  if (q.tag) s.set('tag', q.tag)
  s.set('page', String(q.page ?? 1))
  s.set('pageSize', String(q.pageSize ?? 12))
  return api<Paged<PostSummary>>(`/api/public/posts?${s}`, { tags: ['posts'] })
}

export const menu = (menus: Menu[], code: string) => menus.find((m) => m.code === code)?.items ?? []

export type PublicCategory = { id: string; name: string; slug: string; description?: string | null; parentId?: string | null }
export const getCategories = () => api<PublicCategory[]>('/api/public/categories', { tags: ['posts'] })

export type PublicPage = { id: string; title: string; slug: string; body: string; seoDescription?: string | null; updatedUtc: string; sections: Block[] }
export const getPage = (slug: string) => orNull(api<PublicPage>(`/api/public/pages/${encodeURIComponent(slug)}`, { tags: ['pages', 'homepage', 'posts'] }))

export const listDocuments = (q: Record<string, string | undefined>) => {
  const s = new URLSearchParams(Object.entries(q).filter(([, v]) => v) as [string, string][])
  return api<Paged<DocumentItem>>(`/api/public/documents?${s}`, { tags: ['documents'] })
}
export const getDocument = (slug: string) => orNull(api<DocumentItem>(`/api/public/documents/${encodeURIComponent(slug)}`, { tags: ['documents'] }))
export const getDocumentTypes = () => api<string[]>('/api/public/document-types', { tags: ['documents'] })

export type AlbumDetail = AlbumItem & { eventDate?: string | null; items: { id: string; url?: string | null; thumbnailUrl?: string | null; embedUrl?: string | null; caption?: string | null }[] }
export const listAlbums = (page: number, kind?: string) =>
  api<Paged<AlbumItem>>(`/api/public/albums?page=${page}&pageSize=12${kind ? `&kind=${kind}` : ''}`, { tags: ['albums'] })
export const getAlbum = (slug: string) => orNull(api<AlbumDetail>(`/api/public/albums/${encodeURIComponent(slug)}`, { tags: ['albums'] }))

export type SearchResult = { type: string; title: string; url: string; snippet?: string | null; date?: string | null }
export const search = (q: string, type: string | undefined, page: number) =>
  api<Paged<SearchResult>>(`/api/public/search?q=${encodeURIComponent(q)}&page=${page}&pageSize=10${type ? `&type=${type}` : ''}`, { cache: 'no-store' })

export type Resolved = {
  type: string
  status: number
  location?: string | null
  slug?: string | null
  title?: string | null
  target?: { type: string; slug?: string | null; url?: string | null } | null
  children?: { id: string; label: string; href: string }[] | null
}
export const resolvePath = (path: string) => api<Resolved>(`/api/public/resolve?path=${encodeURIComponent(path)}`, { cache: 'no-store' })
