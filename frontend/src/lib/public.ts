import { api, ApiClientError, type Paged } from './api'
import type { Menu, PostDetail, PostSummary, SiteSettings } from './types'
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
