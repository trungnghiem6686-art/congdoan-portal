'use client'

import Link from 'next/link'

export default function ErrorPage({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <main className="container-site py-16 text-center">
      <h1 className="text-2xl font-bold text-brand-navy">Đã có lỗi xảy ra</h1>
      <p className="mt-2 text-slate-700">Vui lòng thử lại. Nếu lỗi lặp lại, hãy báo cho quản trị viên mã: {error.digest ?? 'không có'}.</p>
      <div className="mt-6 flex justify-center gap-3">
        <button onClick={reset} className="rounded bg-brand-navy px-4 py-2 font-semibold text-white">Thử lại</button>
        <Link href="/" className="rounded border border-slate-300 bg-white px-4 py-2">Về trang chủ</Link>
      </div>
    </main>
  )
}
