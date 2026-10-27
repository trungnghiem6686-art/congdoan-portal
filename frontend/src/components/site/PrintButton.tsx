'use client'

import { Printer } from 'lucide-react'

export function PrintButton() {
  return (
    <button type="button" onClick={() => window.print()} className="no-print inline-flex items-center gap-1 rounded border border-slate-300 bg-white px-3 py-1.5 text-sm hover:text-brand-blue">
      <Printer aria-hidden className="h-4 w-4" /> In bài
    </button>
  )
}
