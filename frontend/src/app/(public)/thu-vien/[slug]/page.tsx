import type { Metadata } from 'next'
import { notFound } from 'next/navigation'
import { Breadcrumb, PageTitle } from '@/components/site/Common'
import { getAlbum } from '@/lib/public'

type Props = { params: Promise<{ slug: string }> }

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const a = await getAlbum((await params).slug)
  return a ? { title: a.title } : {}
}

const EMBED_HOSTS = ['www.youtube.com', 'youtube.com', 'www.youtube-nocookie.com', 'player.vimeo.com', 'www.facebook.com']
const safeEmbed = (url: string) => {
  try {
    const u = new URL(url)
    return u.protocol === 'https:' && EMBED_HOSTS.includes(u.host) ? url : null
  } catch {
    return null
  }
}

export default async function AlbumPage({ params }: Props) {
  const album = await getAlbum((await params).slug)
  if (!album) notFound()
  return (
    <>
      <Breadcrumb items={[{ label: 'Thư viện', href: '/thu-vien' }, { label: album.title }]} />
      <PageTitle>{album.title}</PageTitle>
      {album.description && <p className="-mt-3 mb-5">{album.description}</p>}
      <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {album.items.map((i) => {
          const embed = i.embedUrl ? safeEmbed(i.embedUrl) : null
          return (
            <li key={i.id} className="rounded bg-white p-2">
              {embed ? (
                <iframe src={embed} title={i.caption ?? album.title} className="aspect-video w-full" allowFullScreen loading="lazy" />
              ) : i.url && album.kind === 'Video' ? (
                <video src={i.url} controls preload="metadata" className="aspect-video w-full bg-black" />
              ) : i.url ? (
                <a href={i.url}>
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img src={i.thumbnailUrl ?? i.url} alt={i.caption ?? album.title} loading="lazy" className="aspect-[4/3] w-full rounded object-cover" />
                </a>
              ) : null}
              {i.caption && <p className="mt-2 text-sm text-slate-700">{i.caption}</p>}
            </li>
          )
        })}
      </ul>
    </>
  )
}
