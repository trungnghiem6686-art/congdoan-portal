import Link from 'next/link'
import { Search } from 'lucide-react'
import type { MenuItem, SiteSettings } from '@/lib/types'
import { MainNav } from './MainNav'

function today() {
  return new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date())
}

export function SiteHeader({ site, menu }: { site: SiteSettings; menu: MenuItem[] }) {
  return (
    <header>
      <a href="#noi-dung" className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-50 focus:bg-white focus:p-2">
        Bỏ qua đến nội dung chính
      </a>
      <div className="bg-brand-navy text-sm text-white">
        <div className="container-site flex flex-wrap items-center justify-between gap-2 py-1.5">
          <span className="capitalize">{today()}</span>
          <nav aria-label="Tiện ích" className="flex flex-wrap gap-4">
            {site.utility.links.map((l) => (
              <Link key={l.url} href={l.url} className="hover:underline">
                {l.label}
              </Link>
            ))}
          </nav>
        </div>
      </div>
      <div className="bg-white">
        <div className="container-site flex flex-wrap items-center justify-between gap-4 py-4">
          <Link href="/" className="flex items-center gap-3">
            {site.site.logoUrl ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img src={site.site.logoUrl} alt="" className="h-14 w-14 object-contain" />
            ) : (
              <span aria-hidden className="flex h-14 w-14 items-center justify-center rounded-full bg-brand-red text-lg font-bold text-white">CĐ</span>
            )}
            <span>
              <span className="block text-xl font-bold uppercase leading-tight text-brand-navy md:text-2xl">{site.site.name}</span>
              {site.site.tagline && <span className="block text-sm text-slate-600">{site.site.tagline}</span>}
            </span>
          </Link>
          <form action="/tim-kiem" role="search" className="flex w-full max-w-xs items-center rounded border border-slate-300 bg-white sm:w-auto">
            <label htmlFor="q" className="sr-only">Tìm kiếm</label>
            <input id="q" name="q" type="search" placeholder="Tìm kiếm..." className="w-full min-w-0 px-3 py-2 text-base outline-none" />
            <button type="submit" className="p-2 text-brand-navy" aria-label="Tìm">
              <Search className="h-5 w-5" />
            </button>
          </form>
        </div>
      </div>
      <MainNav items={menu} />
    </header>
  )
}
