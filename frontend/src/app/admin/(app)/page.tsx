'use client'

import { useAdmin } from '@/components/admin/AdminContext'
import { Title } from '@/components/admin/ui'
import { DashboardStats } from '@/components/admin/DashboardStats'

export default function Dashboard() {
  const { me } = useAdmin()
  return (
    <>
      <Title>Xin chào, {me.displayName}</Title>
      <DashboardStats />
    </>
  )
}
