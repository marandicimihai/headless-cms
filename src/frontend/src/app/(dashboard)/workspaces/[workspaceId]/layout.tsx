import type { ReactNode } from "react"
import { redirect } from "next/navigation"
import { CircleAlert } from "lucide-react"

import { BackButton } from "@/components/back-button"
import { WorkspaceSidebar } from "@/app/(dashboard)/workspaces/[workspaceId]/workspace-sidebar"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { listProjects } from "@/lib/api/projects"
import { getSession } from "@/lib/api/session"
import { listMyWorkspaces } from "@/lib/api/workspaces"

export default async function WorkspaceLayout({
  children,
  params,
}: {
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

  if (!workspace?.currentRole) redirect("/")

  const projectsResult = await listProjects(workspace.id)

  return (
    <div className="flex min-h-0 flex-1">
      <WorkspaceSidebar
        workspaceId={workspace.id}
        projects={projectsResult.ok ? projectsResult.data : []}
        canWrite={workspace.currentRole === "owner" || workspace.currentRole === "editor"}
      />
      <div className="flex min-h-0 min-w-0 flex-1 flex-col">
        {children}
      </div>
    </div>
  )
}
