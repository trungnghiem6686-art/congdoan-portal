import type { Metadata, Viewport } from 'next'
import '@/styles/tokens.css'

export const metadata: Metadata = {
  metadataBase: new URL(process.env.NEXT_PUBLIC_SITE_URL ?? 'http://localhost:3000'),
  title: { default: 'Công đoàn Đường sắt Việt Nam', template: '%s | Công đoàn Đường sắt Việt Nam' },
  description: 'Cổng thông tin điện tử Công đoàn Đường sắt Việt Nam',
}

export const viewport: Viewport = { width: 'device-width', initialScale: 1, themeColor: '#12365B' }

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="vi">
      <body className="min-h-screen font-sans antialiased">{children}</body>
    </html>
  )
}
