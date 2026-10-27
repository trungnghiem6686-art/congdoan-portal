'use client'

import { ResourcePage } from '@/components/admin/ResourcePage'
import { formatDateTime } from '@/lib/format'

export default function BannersPage() {
  return (
    <ResourcePage
      title="Banner"
      endpoint="/api/admin/banners"
      defaults={{ position: 'home_top', isVisible: true, sortOrder: 0 }}
      fields={[
        { key: 'title', label: 'Tiêu đề', type: 'text', required: true },
        { key: 'position', label: 'Vị trí', type: 'text', required: true, hint: 'Ví dụ: home_top, sidebar. Khối "Banner" trên trang chủ chọn theo vị trí.' },
        { key: 'imageMediaId', label: 'Ảnh', type: 'media', accept: 'image/*' },
        { key: 'linkUrl', label: 'Liên kết', type: 'text', hint: 'https://… hoặc /đường-dẫn' },
        { key: 'description', label: 'Mô tả (văn bản thay thế)', type: 'textarea' },
        { key: 'startsUtc', label: 'Hiển thị từ', type: 'datetime' },
        { key: 'endsUtc', label: 'Hiển thị đến', type: 'datetime' },
        { key: 'sortOrder', label: 'Thứ tự', type: 'number' },
        { key: 'isVisible', label: 'Hiển thị', type: 'checkbox' },
      ]}
      columns={[
        { label: 'Tiêu đề', render: (i) => i.title },
        { label: 'Vị trí', render: (i) => i.position },
        { label: 'Lịch', render: (i) => `${i.startsUtc ? formatDateTime(i.startsUtc) : '…'} → ${i.endsUtc ? formatDateTime(i.endsUtc) : '…'}` },
      ]}
    />
  )
}
