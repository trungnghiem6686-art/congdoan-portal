'use client'

import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { api, ApiClientError } from '@/lib/api'

const schema = z.object({
  name: z.string().trim().min(1, 'Vui lòng nhập họ tên').max(200),
  email: z.string().trim().email('Email chưa đúng định dạng'),
  phone: z.string().trim().max(30).optional(),
  subject: z.string().trim().max(300).optional(),
  message: z.string().trim().min(1, 'Vui lòng nhập nội dung').max(5000, 'Tối đa 5000 ký tự'),
  website: z.string().optional(),
})
type Form = z.infer<typeof schema>

const field = 'w-full rounded border border-slate-300 bg-white px-3 py-2'

export function ContactForm() {
  const { register, handleSubmit, reset, formState } = useForm<Form>({ resolver: zodResolver(schema) })
  const [done, setDone] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Success is only shown once the server confirms the request was stored.
  const onSubmit = async (values: Form) => {
    setError(null)
    setDone(false)
    try {
      await api('/api/public/contact', { method: 'POST', body: values })
      setDone(true)
      reset()
    } catch (e) {
      setError(e instanceof ApiClientError ? e.message : 'Không gửi được liên hệ. Vui lòng thử lại.')
    }
  }

  const err = (k: keyof Form) => formState.errors[k]?.message && (
    <p id={`${k}-err`} className="mt-1 text-sm text-brand-red">{formState.errors[k]?.message}</p>
  )

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4 rounded bg-white p-5">
      {done && <p role="status" className="rounded bg-green-50 p-3 text-green-800">Đã gửi liên hệ. Cảm ơn bạn, chúng tôi sẽ phản hồi sớm.</p>}
      {error && <p role="alert" className="rounded bg-red-50 p-3 text-brand-red">{error}</p>}
      <div className="grid gap-4 sm:grid-cols-2">
        <label className="block"><span className="mb-1 block font-medium">Họ và tên *</span>
          <input className={field} {...register('name')} aria-invalid={!!formState.errors.name} aria-describedby="name-err" autoComplete="name" /></label>
        <label className="block"><span className="mb-1 block font-medium">Email *</span>
          <input className={field} type="email" {...register('email')} aria-invalid={!!formState.errors.email} autoComplete="email" /></label>
      </div>
      {err('name')}{err('email')}
      <div className="grid gap-4 sm:grid-cols-2">
        <label className="block"><span className="mb-1 block font-medium">Điện thoại</span><input className={field} {...register('phone')} autoComplete="tel" /></label>
        <label className="block"><span className="mb-1 block font-medium">Tiêu đề</span><input className={field} {...register('subject')} /></label>
      </div>
      <label className="block"><span className="mb-1 block font-medium">Nội dung *</span>
        <textarea className={field} rows={6} {...register('message')} aria-invalid={!!formState.errors.message} /></label>
      {err('message')}
      {/* Honeypot: hidden from people, filled by bots. */}
      <input type="text" tabIndex={-1} autoComplete="off" aria-hidden className="hidden" {...register('website')} />
      <button type="submit" disabled={formState.isSubmitting} className="rounded bg-brand-red px-5 py-2.5 font-semibold text-white hover:bg-red-800 disabled:opacity-60">
        {formState.isSubmitting ? 'Đang gửi…' : 'Gửi liên hệ'}
      </button>
    </form>
  )
}
