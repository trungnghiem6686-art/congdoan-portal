import { SiteFooter } from '@/components/site/SiteFooter'
import { SiteHeader } from '@/components/site/SiteHeader'
import { getMenus, getSite, menu } from '@/lib/public'

// Rendered per request; the data itself comes from the tag-invalidated cache, so publishing needs no rebuild.
export const dynamic = 'force-dynamic'

export default async function PublicLayout({ children }: { children: React.ReactNode }) {
  const [site, menus] = await Promise.all([getSite(), getMenus()])
  return (
    <>
      <SiteHeader site={site} menu={menu(menus, 'main')} />
      <main id="noi-dung" className="container-site py-6">
        {children}
      </main>
      <SiteFooter site={site} menu={menu(menus, 'footer')} />
    </>
  )
}
