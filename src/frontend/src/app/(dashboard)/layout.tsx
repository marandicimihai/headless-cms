import type { ReactNode } from "react"
import { redirect } from "next/navigation"

import { AppSidebar } from "@/components/dashboard/app-sidebar"
import { DashboardBreadcrumb } from "@/components/dashboard/dashboard-breadcrumb"
import { DashboardHeader } from "@/components/dashboard/dashboard-header"
import {
  SidebarInset,
  SidebarProvider,
} from "@/components/ui/sidebar"
import { getSession } from "@/lib/api/session"

export default async function DashboardLayout({
  breadcrumb,
  children,
}: {
  breadcrumb: ReactNode
  children: ReactNode
}) {
  const session = await getSession()

  if (!session) {
    redirect("/auth/login")
  }

  return (
    <SidebarProvider>
      <AppSidebar />
      <SidebarInset className="h-svh overflow-hidden">
        <DashboardHeader />
        <div className="flex min-h-0 flex-1 flex-col overflow-y-auto">
          {children}
        </div>
        <DashboardBreadcrumb>{breadcrumb}</DashboardBreadcrumb>
      </SidebarInset>
    </SidebarProvider>
  )
}
