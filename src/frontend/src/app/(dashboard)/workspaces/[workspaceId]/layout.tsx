import type { ReactNode } from "react"
import { redirect } from "next/navigation"
import { CircleAlert } from "lucide-react"

import { BackButton } from "@/components/back-button"
import { DashboardBreadcrumb } from "@/app/(dashboard)/workspaces/[workspaceId]/dashboard-breadcrumb"
import { WorkspaceSidebar } from "@/app/(dashboard)/workspaces/[workspaceId]/workspace-sidebar"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { SidebarProvider } from "@/components/ui/sidebar"
import { getSession } from "@/lib/api/session"
import { listMyWorkspaces } from "@/lib/api/workspaces"

export default async function WorkspaceLayout({
  breadcrumb,
  children,
  params,
}: {
  breadcrumb: ReactNode
  children: ReactNode
  params: Promise<{ workspaceId: string }>
}) {
  const session = await getSession()

  if (!session) {
    redirect("/auth/login")
  }

  const { workspaceId } = await params
  const workspaceResult = await listMyWorkspaces()

  if (!workspaceResult.ok) {
    return (
      <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
        <BackButton href="/">Back to dashboard</BackButton> 
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Workspace</h1>
        </div>
        <Alert variant="destructive">
          <CircleAlert />
          <AlertTitle>Unable to load workspace</AlertTitle>
          <AlertDescription>{workspaceResult.error.detail}</AlertDescription>
        </Alert>
      </main>
    )
  }

  const workspace = workspaceResult.data.find(
    (item) => item.id.toLowerCase() === workspaceId.toLowerCase(),
  )

  if (!workspace?.currentRole) {
    return (
      <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
        <BackButton href="/">Back to dashboard</BackButton>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            Workspace unavailable
          </h1>
          <p className="text-sm text-muted-foreground">
            This workspace is not available to your account.
          </p>
        </div>
        <Alert variant="destructive">
          <CircleAlert />
          <AlertTitle>Access unavailable</AlertTitle>
          <AlertDescription>
            Ask a workspace owner to send an invitation if you need access.
          </AlertDescription>
        </Alert>
      </main>
    )
  }

  return (
    <SidebarProvider className="min-h-0 flex-1">
      <WorkspaceSidebar
        workspaceId={workspace.id}
        workspaceName={workspace.name}
      />
      <div className="flex min-w-0 flex-1 flex-col">
        <DashboardBreadcrumb>{breadcrumb}</DashboardBreadcrumb>
        <div className="flex min-h-0 flex-1 flex-col overflow-y-auto">
          {children}
        </div>
      </div>
    </SidebarProvider>
  )
}
