import type { MetadataRoute } from 'next'
import { getSitemap } from '@/lib/public'

// Rendered per request (fetches stay tag-cached), so the build does not need a running API.
export const dynamic = 'force-dynamic'

const site = (process.env.NEXT_PUBLIC_SITE_URL ?? 'http://localhost:3000').replace(/\/$/, '')

/** Public listing pages plus every visible post, category, page, document and album (canonical paths). */
export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const fixed = ['/', '/tin-tuc', '/van-ban', '/thu-vien', '/lien-he'].map((path) => ({ url: site + path }))
  const content = (await getSitemap()).map((e) => ({
    url: site + e.path,
    ...(e.lastModifiedUtc ? { lastModified: new Date(e.lastModifiedUtc) } : {}),
  }))
  return [...fixed, ...content]
}
