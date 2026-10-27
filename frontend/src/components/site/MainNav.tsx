'use client'

import Link from 'next/link'
import { useState } from 'react'
import { ChevronDown, Menu as MenuIcon, X } from 'lucide-react'
import type { MenuItem } from '@/lib/types'

function Item({ item, onNavigate }: { item: MenuItem; onNavigate: () => void }) {
  const [open, setOpen] = useState(false)
  const link = (
    <Link
      href={item.href}
      target={item.openInNewTab ? '_blank' : undefined}
      rel={item.openInNewTab ? 'noopener noreferrer' : undefined}
      onClick={onNavigate}
      className="block px-4 py-3 font-semibold text-white hover:bg-brand-blue"
    >
      {item.label}
    </Link>
  )
  if (item.children.length === 0) return <li>{link}</li>
  return (
    <li className="group relative" onMouseLeave={() => setOpen(false)}>
      <div className="flex items-center">
        {link}
        <button
          type="button"
          aria-expanded={open}
          aria-label={`Mở menu con ${item.label}`}
          onClick={() => setOpen((o) => !o)}
          className="px-2 py-3 text-white hover:bg-brand-blue"
        >
          <ChevronDown aria-hidden className="h-4 w-4" />
        </button>
      </div>
      <ul className={`${open ? 'block' : 'hidden'} z-30 bg-brand-navy lg:absolute lg:left-0 lg:top-full lg:min-w-[220px] lg:shadow-lg lg:group-hover:block`}>
        {item.children.map((c) => (
          <li key={c.id}>
            <Link href={c.href} onClick={onNavigate} className="block px-6 py-2.5 text-white hover:bg-brand-blue lg:px-4">
              {c.label}
            </Link>
          </li>
        ))}
      </ul>
    </li>
  )
}

export function MainNav({ items }: { items: MenuItem[] }) {
  const [open, setOpen] = useState(false)
  return (
    <nav aria-label="Menu chính" className="bg-brand-navy">
      <div className="container-site">
        <button
          type="button"
          className="flex items-center gap-2 py-3 font-semibold text-white lg:hidden"
          aria-expanded={open}
          onClick={() => setOpen((o) => !o)}
        >
          {open ? <X aria-hidden className="h-5 w-5" /> : <MenuIcon aria-hidden className="h-5 w-5" />} Danh mục
        </button>
        <ul className={`${open ? 'block' : 'hidden'} lg:flex lg:flex-wrap`}>
          <li>
            <Link href="/" className="block px-4 py-3 font-semibold text-white hover:bg-brand-blue" onClick={() => setOpen(false)}>
              Trang chủ
            </Link>
          </li>
          {items.map((i) => (
            <Item key={i.id} item={i} onNavigate={() => setOpen(false)} />
          ))}
        </ul>
      </div>
    </nav>
  )
}
