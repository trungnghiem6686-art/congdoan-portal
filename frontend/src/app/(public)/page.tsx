import { BlockRenderer } from '@/components/blocks/BlockRenderer'
import { getHomepage } from '@/lib/public'

export default async function HomePage() {
  const home = await getHomepage()
  if (home.sections.length === 0)
    return <p className="rounded bg-white p-6 text-slate-700">Trang chủ chưa được thiết lập. Quản trị viên có thể dựng trang chủ trong mục Quản trị → Trang chủ.</p>
  return <BlockRenderer blocks={home.sections} />
}
