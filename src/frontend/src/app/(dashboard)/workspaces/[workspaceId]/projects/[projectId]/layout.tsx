import type { ReactNode } from "react"

import { WorkspaceSidebar } from "@/app/(dashboard)/workspaces/[workspaceId]/workspace-sidebar"
import { getProject } from "@/lib/api/projects"
import { getSession } from "@/lib/api/session"
import { listMyWorkspaces } from "@/lib/api/workspaces"

export default async function ProjectLayout({
  children,
  params,
}: {
  children: ReactNode
  params: Promise<{ workspaceId: string; projectId: string }>
}) {
  const { workspaceId, projectId } = await params
  const [session, workspacesResult, projectResult] = await Promise.all([
    getSession(),
    listMyWorkspaces(),
    getProject(workspaceId, projectId, { fresh: true }),
  ])

  const workspace = workspacesResult.ok
    ? workspacesResult.data.find((item) => item.id.toLowerCase() === workspaceId.toLowerCase())
    : undefined
  const project = projectResult.ok ? projectResult.data : null

  if (!session || !workspace?.currentRole || !project) return children

  return (
    <div className="flex min-h-0 min-w-0 flex-1">
      <WorkspaceSidebar
        workspaceId={workspace.id}
        projects={[project]}
        activeProject={project}
        canWrite={workspace.currentRole === "owner" || workspace.currentRole === "editor"}
      />
      <div className="flex min-h-0 min-w-0 flex-1 flex-col">
        {children}
      </div>
    </div>
  )
}
