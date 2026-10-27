import { QuestionEditor } from '@/components/admin/QuestionEditor'

export default async function EditQuestion({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params
  return <QuestionEditor key={id} id={id} />
}
