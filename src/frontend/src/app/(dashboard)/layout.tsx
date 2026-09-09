import type { ReactNode } from "react"
import { redirect } from "next/navigation"

import { SidebarProvider } from "@/components/ui/sidebar"
import { getSession } from "@/lib/api/session"

export default async function DashboardLayout({
  children,
  header,
}: {
  children: ReactNode
  header: ReactNode
}) {
  const session = await getSession()

  if (!session) {
    redirect("/auth/login")
  }

  return (
    <SidebarProvider className="min-h-svh">
      <div className="flex h-svh flex-1 flex-col overflow-hidden">
        {header}
        <main className="flex min-h-0 flex-1 flex-col overflow-y-auto">
          {children}
        </main>
      </div>
    </SidebarProvider>
  )
}
