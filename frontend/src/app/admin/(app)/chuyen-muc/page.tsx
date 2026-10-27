'use client'

import { ResourcePage } from '@/components/admin/ResourcePage'

export default function CategoriesPage() {
  return (
    <ResourcePage
      title="Chuyên mục"
      endpoint="/api/admin/categories"
      unpaged
      defaults={{ isVisible: true, sortOrder: 0 }}
      fields={[
        { key: 'name', label: 'Tên chuyên mục', type: 'text', required: true },
        { key: 'slug', label: 'Đường dẫn', type: 'text', hint: 'Để trống để tự tạo từ tên; đổi tên sẽ đổi đường dẫn và chuyển hướng địa chỉ cũ.' },
        { key: 'description', label: 'Mô tả', type: 'textarea' },
        { key: 'sortOrder', label: 'Thứ tự', type: 'number' },
        { key: 'isVisible', label: 'Hiển thị', type: 'checkbox' },
      ]}
      columns={[
        { label: 'Tên', render: (i) => i.name },
        { label: 'Đường dẫn', render: (i) => <code>/chuyen-muc/{i.slug}</code> },
      ]}
      hideable={false}
    />
  )
}
