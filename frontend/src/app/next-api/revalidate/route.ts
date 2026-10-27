import { revalidateTag } from 'next/cache'
import type { NextRequest } from 'next/server'

const ALLOWED = new Set(['site', 'menus', 'homepage', 'posts', 'banners', 'documents', 'albums', 'pages', 'redirects'])

/** Called by the backend after CMS changes (Frontend:RevalidateUrl). Requires the shared secret. */
export async function POST(request: NextRequest) {
  const secret = process.env.REVALIDATE_SECRET
  if (!secret || request.headers.get('x-revalidate-secret') !== secret) return Response.json({ ok: false }, { status: 401 })
  const tag = request.nextUrl.searchParams.get('tag') ?? ''
  if (!ALLOWED.has(tag)) return Response.json({ ok: false, error: 'unknown tag' }, { status: 400 })
  // expire: 0 — the next visitor gets fresh content, never the version that was just replaced.
  revalidateTag(tag, { expire: 0 })
  return Response.json({ ok: true, tag })
}
