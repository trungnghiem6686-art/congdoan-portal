import type { Metadata } from 'next'
import { Breadcrumb, PageTitle } from '@/components/site/Common'
import { ContactForm } from '@/components/site/ContactForm'
import { getSite } from '@/lib/public'

export const metadata: Metadata = { title: 'Liên hệ' }

export default async function ContactPage() {
  const { contact } = await getSite()
  return (
    <>
      <Breadcrumb items={[{ label: 'Liên hệ' }]} />
      <PageTitle>Liên hệ</PageTitle>
      <div className="grid gap-8 lg:grid-cols-3">
        <address className="space-y-2 rounded bg-white p-5 not-italic">
          <p className="font-semibold text-brand-navy">{contact.organization}</p>
          {contact.address && <p>Địa chỉ: {contact.address}</p>}
          {contact.phone && <p>Điện thoại: {contact.phone}</p>}
          {contact.email && <p>Email: {contact.email}</p>}
          {contact.workingHours && <p>{contact.workingHours}</p>}
        </address>
        <div className="lg:col-span-2"><ContactForm /></div>
      </div>
    </>
  )
}
