import type { Config } from 'tailwindcss'
import typography from '@tailwindcss/typography'

export default {
  content: ['./src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        brand: { navy: '#12365B', blue: '#176BB0', red: '#C62828', bg: '#F4F6F8' },
      },
      fontFamily: { sans: ['"Be Vietnam Pro"', 'system-ui', 'Segoe UI', 'Arial', 'sans-serif'] },
      maxWidth: { site: '1280px' },
    },
  },
  plugins: [typography],
} satisfies Config
