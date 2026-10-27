'use client'

import { useEffect, useRef, useState } from 'react'
import { Bold, Heading2, Heading3, ImagePlus, Italic, Link as LinkIcon, List, ListOrdered, Quote, Film, Video } from 'lucide-react'
import { toEmbedUrl, uploadFile } from '@/lib/upload'
import { ApiClientError } from '@/lib/api'

type Props = { label: string; initialHtml: string; onChange: (html: string) => void }

const IMAGE = /^image\/(png|jpe?g|gif|webp)$/

/**
 * Lightweight rich-text editor: formatting, images and videos uploaded from the computer
 * (button, paste, drag and drop) with captions, and YouTube/Vimeo embeds. The server sanitises the result.
 */
export function RichTextEditor({ label, initialHtml, onChange }: Props) {
  const ref = useRef<HTMLDivElement>(null)
  const range = useRef<Range | null>(null)
  const [busy, setBusy] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (ref.current) ref.current.innerHTML = initialHtml
  }, [initialHtml])

  const emit = () => ref.current && onChange(ref.current.innerHTML)

  const remember = () => {
    const sel = window.getSelection()
    if (sel && sel.rangeCount > 0 && ref.current?.contains(sel.getRangeAt(0).commonAncestorContainer)) range.current = sel.getRangeAt(0).cloneRange()
  }

  /** Inserts a block at the caret (or at the end) followed by an empty paragraph to keep typing. */
  const insertBlock = (node: HTMLElement) => {
    const root = ref.current!
    let anchor: Node | null = range.current?.endContainer ?? null
    while (anchor && anchor.parentNode !== root) anchor = anchor.parentNode
    const after = document.createElement('p')
    after.appendChild(document.createElement('br'))
    if (anchor && anchor !== root) {
      root.insertBefore(node, anchor.nextSibling)
    } else root.appendChild(node)
    root.insertBefore(after, node.nextSibling)
    const r = document.createRange()
    r.setStart(after, 0)
    range.current = r
    emit()
  }

  const figure = (media: HTMLElement, caption = '') => {
    const fig = document.createElement('figure')
    fig.appendChild(media)
    const cap = document.createElement('figcaption')
    cap.textContent = caption
    cap.setAttribute('data-placeholder', 'Chú thích (không bắt buộc)')
    fig.appendChild(cap)
    return fig
  }

  async function addFiles(files: File[]) {
    setError(null)
    for (const file of files) {
      try {
        setBusy(`Đang tải ${file.name}…`)
        const m = await uploadFile(file, (p) => setBusy(`Đang tải ${file.name} (${p}%)`))
        if (m.kind === 'image') {
          const img = document.createElement('img')
          img.src = m.url
          img.alt = ''
          img.setAttribute('data-media-id', m.id)
          insertBlock(figure(img))
        } else if (m.kind === 'video') {
          const v = document.createElement('video')
          v.controls = true
          v.preload = 'metadata'
          v.src = m.url
          insertBlock(figure(v))
        } else {
          const p = document.createElement('p')
          const a = document.createElement('a')
          a.href = m.url
          a.textContent = m.originalName
          p.appendChild(a)
          insertBlock(p)
        }
      } catch (e) {
        setError(e instanceof ApiClientError ? e.message : `Không tải được ${file.name}.`)
      } finally {
        setBusy(null)
      }
    }
  }

  const exec = (cmd: string, value?: string) => {
    ref.current?.focus()
    if (range.current) {
      const sel = window.getSelection()
      sel?.removeAllRanges()
      sel?.addRange(range.current)
    }
    document.execCommand(cmd, false, value)
    emit()
  }

  const embed = () => {
    const raw = window.prompt('Dán liên kết YouTube hoặc Vimeo:')
    if (!raw) return
    const src = toEmbedUrl(raw)
    if (!src) return setError('Nguồn video này không được hỗ trợ. Chỉ nhúng YouTube hoặc Vimeo (https).')
    setError(null)
    const iframe = document.createElement('iframe')
    iframe.src = src
    iframe.setAttribute('allowfullscreen', '')
    iframe.setAttribute('loading', 'lazy')
    iframe.setAttribute('frameborder', '0')
    insertBlock(figure(iframe))
  }

  const link = () => {
    const url = window.prompt('Địa chỉ liên kết (https://… hoặc /đường-dẫn):')
    if (url && (/^https?:\/\//.test(url) || url.startsWith('/'))) exec('createLink', url)
    else if (url) setError('Liên kết phải bắt đầu bằng https:// hoặc /.')
  }

  const tool = 'rounded p-2 hover:bg-slate-100'
  return (
    <div className="rounded border border-slate-300 bg-white">
      <div role="toolbar" aria-label="Định dạng" className="flex flex-wrap gap-1 border-b border-slate-200 p-1">
        <button type="button" className={tool} title="Đậm" aria-label="Đậm" onClick={() => exec('bold')}><Bold className="h-4 w-4" /></button>
        <button type="button" className={tool} title="Nghiêng" aria-label="Nghiêng" onClick={() => exec('italic')}><Italic className="h-4 w-4" /></button>
        <button type="button" className={tool} title="Đề mục lớn" aria-label="Đề mục lớn" onClick={() => exec('formatBlock', 'h2')}><Heading2 className="h-4 w-4" /></button>
        <button type="button" className={tool} title="Đề mục nhỏ" aria-label="Đề mục nhỏ" onClick={() => exec('formatBlock', 'h3')}><Heading3 className="h-4 w-4" /></button>
        <button type="button" className={tool} title="Danh sách" aria-label="Danh sách" onClick={() => exec('insertUnorderedList')}><List className="h-4 w-4" /></button>
        <button type="button" className={tool} title="Danh sách số" aria-label="Danh sách số" onClick={() => exec('insertOrderedList')}><ListOrdered className="h-4 w-4" /></button>
        <button type="button" className={tool} title="Trích dẫn" aria-label="Trích dẫn" onClick={() => exec('formatBlock', 'blockquote')}><Quote className="h-4 w-4" /></button>
        <button type="button" className={tool} title="Liên kết" aria-label="Liên kết" onClick={link}><LinkIcon className="h-4 w-4" /></button>
        <label className={`${tool} cursor-pointer`} title="Chèn ảnh từ máy">
          <ImagePlus aria-hidden className="h-4 w-4" />
          <span className="sr-only">Chèn ảnh từ máy</span>
          <input data-upload="image" type="file" accept="image/png,image/jpeg,image/gif,image/webp" multiple className="sr-only"
            onChange={(e) => { const f = Array.from(e.target.files ?? []); e.target.value = ''; void addFiles(f) }} />
        </label>
        <label className={`${tool} cursor-pointer`} title="Chèn video từ máy">
          <Video aria-hidden className="h-4 w-4" />
          <span className="sr-only">Chèn video từ máy</span>
          <input data-upload="video" type="file" accept="video/mp4,video/webm" className="sr-only"
            onChange={(e) => { const f = Array.from(e.target.files ?? []); e.target.value = ''; void addFiles(f) }} />
        </label>
        <button type="button" className={`${tool} inline-flex items-center gap-1 text-sm`} onClick={embed}>
          <Film aria-hidden className="h-4 w-4" /> Nhúng YouTube
        </button>
      </div>
      {busy && <p role="status" className="bg-blue-50 px-3 py-1 text-sm text-brand-navy">{busy}</p>}
      {error && <p role="alert" className="bg-red-50 px-3 py-1 text-sm text-brand-red">{error}</p>}
      <div
        ref={ref}
        role="textbox"
        aria-multiline="true"
        aria-label={label}
        contentEditable
        suppressContentEditableWarning
        className="prose min-h-[320px] max-w-none p-4 outline-none [&_figcaption:empty]:before:text-slate-400 [&_figcaption:empty]:before:content-[attr(data-placeholder)] [&_iframe]:aspect-video [&_iframe]:w-full"
        onInput={emit}
        onKeyUp={remember}
        onMouseUp={remember}
        onBlur={remember}
        onPaste={(e) => {
          const files = Array.from(e.clipboardData?.files ?? []).filter((f) => IMAGE.test(f.type))
          if (files.length > 0) {
            e.preventDefault()
            remember()
            void addFiles(files)
            return
          }
          // Plain text only: pasted HTML from Word/web pages carries styles and scripts.
          const text = e.clipboardData?.getData('text/plain')
          if (text !== undefined && e.clipboardData?.types.includes('text/html')) {
            e.preventDefault()
            document.execCommand('insertText', false, text)
          }
        }}
        onDragOver={(e) => e.preventDefault()}
        onDrop={(e) => {
          const files = Array.from(e.dataTransfer?.files ?? []).filter((f) => IMAGE.test(f.type) || /^video\//.test(f.type))
          if (files.length === 0) return
          e.preventDefault()
          void addFiles(files)
        }}
      />
    </div>
  )
}
