export const metadata = { title: 'Bảo trì' }

export default function Maintenance() {
  return (
    <div className="rounded bg-white p-8 text-center">
      <h1 className="text-2xl font-bold text-brand-navy">Hệ thống đang bảo trì</h1>
      <p className="mt-2 text-slate-700">Chúng tôi đang nâng cấp hệ thống. Vui lòng quay lại sau ít phút.</p>
    </div>
  )
}
