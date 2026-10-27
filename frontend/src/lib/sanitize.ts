import DOMPurify from 'isomorphic-dompurify'

const EMBED_HOSTS = ['www.youtube.com', 'youtube.com', 'www.youtube-nocookie.com', 'player.vimeo.com', 'www.facebook.com']

/**
 * Second line of defence: the backend already sanitises rich text; this re-sanitises at render time
 * so a compromised or legacy row still cannot run script.
 */
export function safeHtml(html?: string | null): string {
  if (!html) return ''
  const clean = DOMPurify.sanitize(html, {
    ADD_TAGS: ['iframe', 'video', 'source', 'figure', 'figcaption'],
    ADD_ATTR: ['allowfullscreen', 'frameborder', 'allow', 'controls', 'poster', 'loading', 'target'],
    FORBID_ATTR: ['style'],
  })
  if (!clean.includes('<iframe')) return clean
  return clean.replace(/<iframe[^>]*src="([^"]*)"[^>]*>(?:<\/iframe>)?/gi, (tag, src: string) => {
    try {
      const u = new URL(src)
      return u.protocol === 'https:' && EMBED_HOSTS.includes(u.host) ? tag : ''
    } catch {
      return ''
    }
  })
}
