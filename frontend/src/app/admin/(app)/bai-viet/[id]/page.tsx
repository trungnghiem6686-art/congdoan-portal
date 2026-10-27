import { PostEditor } from '@/components/admin/PostEditor'

export default async function EditPostPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params
  return <PostEditor key={id} id={id} />
}
