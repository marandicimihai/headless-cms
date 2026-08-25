import type { ReactNode } from "react"
import { redirect } from "next/navigation"

import { DashboardHeader } from "@/app/(dashboard)/dashboard-header"
import { getSession } from "@/lib/api/session"

export default async function DashboardLayout({
  children,
}: {
  children: ReactNode
}) {
  const session = await getSession()

  if (!session) {
    redirect("/auth/login")
  }

  return (
    <div className="flex h-svh flex-col overflow-hidden">
      <DashboardHeader email={session.email} role={session.platformRole} />
      <main className="flex min-h-0 flex-1 flex-col overflow-y-auto">
        {children}
      </main>
    </div>
  )
}
