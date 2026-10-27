'use client'

import { ResourcePage } from '@/components/admin/ResourcePage'
import { formatDay } from '@/lib/format'

export default function DocumentsAdminPage() {
  return (
    <ResourcePage
      title="Văn bản"
      endpoint="/api/admin/documents"
      defaults={{ isVisible: true, sortOrder: 0 }}
      fields={[
        { key: 'number', label: 'Số hiệu', type: 'text', required: true },
        { key: 'title', label: 'Trích yếu', type: 'textarea', required: true },
        { key: 'docType', label: 'Loại văn bản', type: 'text', required: true, hint: 'Ví dụ: Nghị quyết, Quyết định, Hướng dẫn, Công văn' },
        { key: 'issuer', label: 'Cơ quan ban hành', type: 'text' },
        { key: 'issuedDate', label: 'Ngày ban hành', type: 'date', required: true },
        { key: 'effectiveDate', label: 'Ngày hiệu lực', type: 'date' },
        { key: 'summary', label: 'Tóm tắt', type: 'textarea' },
        { key: 'fileMediaId', label: 'Tệp văn bản (PDF/Word)', type: 'media', accept: '.pdf,.doc,.docx,.xls,.xlsx' },
        { key: 'isVisible', label: 'Hiển thị', type: 'checkbox' },
      ]}
      columns={[
        { label: 'Số hiệu', render: (i) => i.number },
        { label: 'Trích yếu', render: (i) => i.title },
        { label: 'Ngày ban hành', render: (i) => formatDay(i.issuedDate) },
      ]}
    />
  )
}
