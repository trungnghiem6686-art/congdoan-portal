import { BlockBuilder } from '@/components/admin/BlockBuilder'

export default async function PageLayout({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params
  return <BlockBuilder base={`/api/admin/pages/${id}/layout`} previewSource={`page:${id}`} title="Khối trang" />
}
