import { ExamEditor } from '@/components/admin/ExamEditor'

export default async function EditExam({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params
  return <ExamEditor key={id} id={id} />
}
