"use client"

import { AppSidebar } from "@/components/dashboard/app-sidebar"
import { DashboardBreadcrumb } from "@/components/dashboard/dashboard-breadcrumb"
import { DashboardHeader } from "@/components/dashboard/dashboard-header"
import { DashboardOverview } from "@/components/dashboard/dashboard-overview"
import {
  SidebarInset,
  SidebarProvider,
} from "@/components/ui/sidebar"

export function DashboardShell() {
  return (
    <SidebarProvider>
      <AppSidebar />
      <SidebarInset>
        <DashboardHeader />
        <DashboardOverview />
        <DashboardBreadcrumb />
      </SidebarInset>
    </SidebarProvider>
  )
}
