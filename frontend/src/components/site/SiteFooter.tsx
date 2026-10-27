import Link from 'next/link'
import type { MenuItem, SiteSettings } from '@/lib/types'

export function SiteFooter({ site, menu }: { site: SiteSettings; menu: MenuItem[] }) {
  const c = site.contact
  return (
    <footer className="mt-12 bg-brand-navy text-white">
      <div className="container-site grid gap-8 py-10 md:grid-cols-3">
        <div>
          <p className="text-lg font-bold uppercase">{c.organization ?? site.site.name}</p>
          <address className="mt-3 space-y-1 not-italic text-white/90">
            {c.address && <p>Địa chỉ: {c.address}</p>}
            {c.phone && <p>Điện thoại: {c.phone}</p>}
            {c.email && <p>Email: <a className="underline" href={`mailto:${c.email}`}>{c.email}</a></p>}
            {c.workingHours && <p>{c.workingHours}</p>}
          </address>
        </div>
        {site.footer.columns.map((col) => (
          <div key={col.title}>
            <p className="font-semibold uppercase">{col.title}</p>
            <ul className="mt-3 space-y-2">
              {col.links.map((l) => (
                <li key={l.url}>
                  <Link className="text-white/90 hover:underline" href={l.url}>{l.label}</Link>
                </li>
              ))}
            </ul>
          </div>
        ))}
        {menu.length > 0 && (
          <ul className="space-y-2 md:col-span-3 md:flex md:flex-wrap md:gap-6 md:space-y-0">
            {menu.map((m) => (
              <li key={m.id}><Link className="text-white/90 hover:underline" href={m.href}>{m.label}</Link></li>
            ))}
          </ul>
        )}
      </div>
      <div className="border-t border-white/20">
        <div className="container-site py-4 text-sm text-white/80">
          <p>{site.footer.copyright}</p>
          {site.footer.note && <p className="mt-1">{site.footer.note}</p>}
        </div>
      </div>
    </footer>
  )
}
