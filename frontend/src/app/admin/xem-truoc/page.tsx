'use client'

import { Suspense, useEffect, useState } from 'react'
import { useSearchParams } from 'next/navigation'
import { api } from '@/lib/api'
import { BlockRenderer, type Block } from '@/components/blocks/BlockRenderer'
import { previewApiUrl } from '@/components/admin/previewSource'

/** Draft preview rendered inside the builder's iframe. Admin-only API, never cached, never public. */
function Preview() {
  const url = previewApiUrl(useSearchParams().get('nguon'))
  const [blocks, setBlocks] = useState<Block[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!url) return
    api<{ sections: Block[] }>(url, { cache: 'no-store' }).then((r) => setBlocks(r.sections)).catch((e) => setError(e.message))
  }, [url])

  if (!url) return <p className="p-4 text-brand-red">Đường dẫn xem trước không hợp lệ.</p>
  if (error) return <p className="p-4 text-brand-red">{error}</p>
  if (!blocks) return <p className="p-4">Đang tải bản xem trước…</p>
  return (
    <main className="container-site py-6">
      <p className="mb-4 rounded bg-yellow-50 p-2 text-sm text-yellow-900">Bản xem trước – chưa hiển thị cho người xem.</p>
      <BlockRenderer blocks={blocks} />
    </main>
  )
}

export default function PreviewPage() {
  return (
    <Suspense>
      <Preview />
    </Suspense>
  )
}
