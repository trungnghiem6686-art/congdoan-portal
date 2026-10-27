const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

/** Admin preview endpoint for the builder's `?nguon=` value: the homepage or `page:<id>`; null for anything else. */
export function previewApiUrl(source: string | null): string | null {
  if (source === null || source === 'homepage') return '/api/admin/homepage/preview'
  if (source.startsWith('page:') && GUID.test(source.slice(5))) return `/api/admin/pages/${source.slice(5)}/layout/preview`
  return null
}
