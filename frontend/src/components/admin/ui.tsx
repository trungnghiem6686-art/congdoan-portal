'use client'

import { useEffect, useRef } from 'react'
import type { ReactNode } from 'react'

export const input = 'w-full rounded border border-slate-300 bg-white px-3 py-2'
export const btn = 'inline-flex items-center gap-1 rounded px-3 py-2 font-semibold disabled:opacity-50'
export const btnPrimary = `${btn} bg-brand-navy text-white hover:bg-brand-blue`
export const btnDanger = `${btn} bg-brand-red text-white hover:bg-red-800`
export const btnGhost = `${btn} border border-slate-300 bg-white hover:text-brand-blue`

export function Title({ children, actions }: { children: ReactNode; actions?: ReactNode }) {
  return (
    <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
      <h1 className="text-2xl font-bold text-brand-navy">{children}</h1>
      {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
    </div>
  )
}

export function Field({ label, children, hint }: { label: string; children: ReactNode; hint?: string }) {
  return (
    <div>
      <label className="block">
        <span className="mb-1 block text-sm font-medium">{label}</span>
        {children}
      </label>
      {hint && <p className="mt-1 text-xs text-slate-500">{hint}</p>}
    </div>
  )
}

export function Notice({ kind, children }: { kind: 'ok' | 'error' | 'info'; children: ReactNode }) {
  const cls = kind === 'ok' ? 'bg-green-50 text-green-800' : kind === 'error' ? 'bg-red-50 text-brand-red' : 'bg-blue-50 text-brand-navy'
  return <p role={kind === 'error' ? 'alert' : 'status'} className={`rounded p-3 ${cls}`}>{children}</p>
}

/** Accessible modal: focus moves in, Escape closes. */
export function Dialog({ title, children, onClose }: { title: string; children: ReactNode; onClose: () => void }) {
  const ref = useRef<HTMLDivElement>(null)
  useEffect(() => {
    ref.current?.focus()
    const esc = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    document.addEventListener('keydown', esc)
    return () => document.removeEventListener('keydown', esc)
  }, [onClose])
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div ref={ref} tabIndex={-1} role="dialog" aria-modal="true" aria-labelledby="dlg-title" className="w-full max-w-lg rounded bg-white p-5 shadow-xl outline-none">
        <h2 id="dlg-title" className="mb-3 text-lg font-bold text-brand-navy">{title}</h2>
        {children}
      </div>
    </div>
  )
}

export function Table({ head, children }: { head: string[]; children: ReactNode }) {
  return (
    <div className="overflow-x-auto rounded bg-white shadow-sm">
      <table className="w-full min-w-[640px] text-left text-sm">
        <thead className="bg-slate-100">
          <tr>{head.map((h) => <th key={h} className="p-3 font-semibold">{h}</th>)}</tr>
        </thead>
        <tbody>{children}</tbody>
      </table>
    </div>
  )
}

export function Pager({ page, pageSize, total, onPage }: { page: number; pageSize: number; total: number; onPage: (p: number) => void }) {
  const pages = Math.max(1, Math.ceil(total / pageSize))
  return (
    <div className="mt-3 flex items-center justify-between text-sm">
      <span>Tổng {total} mục</span>
      <div className="flex items-center gap-2">
        <button className={btnGhost} disabled={page <= 1} onClick={() => onPage(page - 1)}>Trước</button>
        <span>{page}/{pages}</span>
        <button className={btnGhost} disabled={page >= pages} onClick={() => onPage(page + 1)}>Sau</button>
      </div>
    </div>
  )
}

export function errorText(e: unknown) {
  return e instanceof Error ? e.message : 'Đã có lỗi xảy ra.'
}
