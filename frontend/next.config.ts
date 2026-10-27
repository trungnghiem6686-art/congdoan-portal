import type { NextConfig } from 'next'

const api = process.env.API_BASE_URL ?? 'http://localhost:5000'

const nextConfig: NextConfig = {
  // Self-contained server (node server.js) to run as a Windows service behind IIS.
  output: 'standalone',
  poweredByHeader: false,
  // In production IIS sends /api and /media straight to Kestrel; these rewrites cover development and direct access.
  async rewrites() {
    return [
      { source: '/api/:path*', destination: `${api}/api/:path*` },
      { source: '/media/:path*', destination: `${api}/media/:path*` },
    ]
  },
  async headers() {
    return [
      {
        source: '/:path*',
        headers: [
          { key: 'X-Content-Type-Options', value: 'nosniff' },
          { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
          { key: 'X-Frame-Options', value: 'SAMEORIGIN' },
        ],
      },
      { source: '/(admin|thi)/:path*', headers: [{ key: 'Cache-Control', value: 'no-store' }] },
    ]
  },
}

export default nextConfig
