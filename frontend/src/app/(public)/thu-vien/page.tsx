import type { Metadata } from 'next'
import Link from 'next/link'
import { Breadcrumb, PageTitle, Pagination, pageParam, str } from '@/components/site/Common'
import { Cover } from '@/components/blocks/parts'
import { listAlbums } from '@/lib/public'

export const metadata: Metadata = { title: 'Thư viện ảnh, video' }

export default async function GalleryPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const sp = await searchParams
  const page = pageParam(sp.trang)
  const kind = str(sp.loai) === 'video' ? 'Video' : str(sp.loai) === 'anh' ? 'Photo' : undefined
  const albums = await listAlbums(page, kind)
  const tab = (label: string, value?: string) => (
    <Link href={value ? `/thu-vien?loai=${value}` : '/thu-vien'} aria-current={str(sp.loai) === value ? 'page' : undefined}
      className={`rounded px-3 py-1.5 ${str(sp.loai) === value ? 'bg-brand-navy text-white' : 'bg-white'}`}>{label}</Link>
  )
  return (
    <>
      <Breadcrumb items={[{ label: 'Thư viện' }]} />
      <PageTitle>Thư viện ảnh, video</PageTitle>
      <div className="mb-5 flex gap-2">{tab('Tất cả')}{tab('Ảnh', 'anh')}{tab('Video', 'video')}</div>
      {albums.items.length === 0 ? <p className="rounded bg-white p-6">Chưa có album.</p> : (
        <ul className="grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-4">
          {albums.items.map((a) => (
            <li key={a.id}>
              <Link href={`/thu-vien/${a.slug}`} className="group block">
                <Cover src={a.coverUrl} alt="" className="aspect-[4/3] w-full rounded" />
                <span className="mt-2 block font-semibold group-hover:text-brand-blue">{a.title}</span>
              </Link>
            </li>
          ))}
        </ul>
      )}
      <Pagination page={page} pageSize={12} total={albums.total} basePath="/thu-vien" query={{ loai: str(sp.loai) }} />
    </>
  )
}
