import Link from 'next/link'

export default function NotFound() {
  return (
    <main className="container-site py-16 text-center">
      <p className="text-6xl font-bold text-brand-navy">404</p>
      <h1 className="mt-2 text-2xl font-bold">Không tìm thấy trang</h1>
      <p className="mt-2 text-slate-700">Trang bạn tìm có thể đã được chuyển hoặc không còn tồn tại.</p>
      <div className="mt-6 flex justify-center gap-3">
        <Link href="/" className="rounded bg-brand-navy px-4 py-2 font-semibold text-white">Về trang chủ</Link>
        <Link href="/tim-kiem" className="rounded border border-slate-300 bg-white px-4 py-2">Tìm kiếm</Link>
      </div>
    </main>
  )
}
