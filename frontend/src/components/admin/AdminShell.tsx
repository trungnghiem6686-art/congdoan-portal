'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { useState } from 'react'
import { LogOut, Menu as MenuIcon } from 'lucide-react'
import { useAdmin } from './AdminContext'

type NavItem = { href: string; label: string; perm?: string }
type NavGroup = { title: string; items: NavItem[] }

export const NAV: NavGroup[] = [
  { title: 'Tổng quan', items: [{ href: '/admin', label: 'Bảng điều khiển' }] },
  {
    title: 'Nội dung',
    items: [
      { href: '/admin/bai-viet', label: 'Bài viết', perm: 'cms.post.edit' },
      { href: '/admin/chuyen-muc', label: 'Chuyên mục', perm: 'cms.category.manage' },
      { href: '/admin/trang-chu', label: 'Trang chủ', perm: 'cms.homepage.manage' },
      { href: '/admin/trang', label: 'Trang tĩnh', perm: 'cms.page.manage' },
      { href: '/admin/menu', label: 'Menu', perm: 'cms.menu.manage' },
      { href: '/admin/banner', label: 'Banner', perm: 'cms.banner.manage' },
      { href: '/admin/van-ban', label: 'Văn bản', perm: 'cms.document.manage' },
      { href: '/admin/thu-vien', label: 'Thư viện ảnh/video', perm: 'cms.album.manage' },
      { href: '/admin/tep', label: 'Tệp đã tải lên', perm: 'cms.media.upload' },
      { href: '/admin/lien-he', label: 'Liên hệ', perm: 'cms.contact.view' },
      { href: '/admin/cau-hinh', label: 'Cấu hình website', perm: 'cms.settings.manage' },
    ],
  },
  {
    title: 'Thi trực tuyến',
    items: [
      { href: '/admin/cau-hoi', label: 'Ngân hàng câu hỏi', perm: 'exam.question.manage' },
      { href: '/admin/ky-thi', label: 'Kỳ thi', perm: 'exam.exam.manage' },
      { href: '/admin/bao-cao', label: 'Báo cáo', perm: 'report.view' },
    ],
  },
  {
    title: 'Hệ thống',
    items: [
      { href: '/admin/tai-khoan', label: 'Tài khoản quản trị', perm: 'identity.user.manage' },
      { href: '/admin/thi-sinh', label: 'Thí sinh', perm: 'identity.candidate.view' },
      { href: '/admin/don-vi', label: 'Đơn vị, nhóm', perm: 'identity.orgunit.manage' },
      { href: '/admin/vai-tro', label: 'Vai trò, quyền', perm: 'identity.role.manage' },
      { href: '/admin/nhat-ky', label: 'Nhật ký', perm: 'audit.view' },
    ],
  },
]

export function AdminShell({ children }: { children: React.ReactNode }) {
  const { me, can, logout } = useAdmin()
  const pathname = usePathname()
  const [open, setOpen] = useState(false)
  const active = (href: string) => pathname === href || (href !== '/admin' && pathname.startsWith(href + '/'))

  return (
    <div className="min-h-screen bg-brand-bg lg:flex">
      <aside className={`${open ? 'block' : 'hidden'} w-full shrink-0 bg-brand-navy text-white lg:block lg:min-h-screen lg:w-64`}>
        <div className="px-4 py-4 text-lg font-bold">Quản trị</div>
        <nav aria-label="Quản trị" className="pb-6">
          {NAV.map((g) => {
            const items = g.items.filter((i) => !i.perm || can(i.perm))
            if (items.length === 0) return null
            return (
              <div key={g.title} className="mt-3">
                <p className="px-4 text-xs font-semibold uppercase tracking-wide text-white/60">{g.title}</p>
                <ul className="mt-1">
                  {items.map((i) => (
                    <li key={i.href}>
                      <Link href={i.href} onClick={() => setOpen(false)} aria-current={active(i.href) ? 'page' : undefined}
                        className={`block px-4 py-2 hover:bg-brand-blue ${active(i.href) ? 'bg-brand-blue font-semibold' : ''}`}>
                        {i.label}
                      </Link>
                    </li>
                  ))}
                </ul>
              </div>
            )
          })}
        </nav>
      </aside>
      <div className="min-w-0 flex-1">
        <header className="flex items-center justify-between gap-3 border-b border-slate-200 bg-white px-4 py-3">
          <button type="button" className="lg:hidden" aria-expanded={open} aria-label="Mở menu quản trị" onClick={() => setOpen((o) => !o)}>
            <MenuIcon className="h-6 w-6" />
          </button>
          <Link href="/" className="text-sm text-brand-blue hover:underline" target="_blank">Xem trang công khai ↗</Link>
          <div className="flex items-center gap-3 text-sm">
            <span>{me.displayName}</span>
            <button type="button" onClick={logout} className="inline-flex items-center gap-1 rounded border border-slate-300 px-2 py-1 hover:text-brand-red">
              <LogOut aria-hidden className="h-4 w-4" /> Đăng xuất
            </button>
          </div>
        </header>
        <main className="p-4 md:p-6">{children}</main>
      </div>
    </div>
  )
}
