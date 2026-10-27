'use client'

import Link from 'next/link'
import { ResourcePage } from '@/components/admin/ResourcePage'

export default function PagesAdminPage() {
  return (
    <ResourcePage
      title="Trang tĩnh"
      endpoint="/api/admin/pages"
      defaults={{ isVisible: true, body: '' }}
      fields={[
        { key: 'title', label: 'Tiêu đề', type: 'text', required: true },
        { key: 'slug', label: 'Đường dẫn', type: 'text', hint: 'Để trống để tự tạo từ tiêu đề.' },
        { key: 'seoDescription', label: 'Mô tả ngắn (SEO)', type: 'textarea' },
        { key: 'body', label: 'Nội dung trang', type: 'richtext' },
        { key: 'isVisible', label: 'Hiển thị', type: 'checkbox' },
      ]}
      columns={[
        { label: 'Tiêu đề', render: (i) => i.title },
        { label: 'Đường dẫn', render: (i) => <a className="text-brand-blue underline" href={i.path} target="_blank" rel="noreferrer">{i.path}</a> },
      ]}
      extra={(i) => <Link className="inline-flex items-center rounded border border-slate-300 bg-white px-3 py-2 font-semibold hover:text-brand-blue" href={`/admin/trang/${i.id}/bo-cuc`}>Khối</Link>}
    />
  )
}
