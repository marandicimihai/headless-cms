import type { ReactNode } from "react"
import { redirect } from "next/navigation"

import { DashboardHeader } from "@/app/(dashboard)/dashboard-header"
import { listProjects } from "@/lib/api/projects"
import { getSession } from "@/lib/api/session"
import { listMyWorkspaces } from "@/lib/api/workspaces"

export default async function WorkspaceHeaderLayout({
  children,
  params,
}: {
  children: ReactNode
  params: Promise<{ workspaceId: string }>
}) {
  const session = await getSession()

  if (!session) redirect("/auth/login")

  const { workspaceId } = await params
  const workspacesResult = await listMyWorkspaces()

  if (!workspacesResult.ok) {
    return (
      <>
        <DashboardHeader email={session.email} role={session.platformRole} />
        {children}
      </>
    )
  }

  const workspace = workspacesResult.data.find(
    (item) => item.id.toLowerCase() === workspaceId.toLowerCase(),
  )

  if (!workspace?.currentRole) redirect("/")

  const projectsResult = await listProjects(workspace.id)

  return (
    <>
      <DashboardHeader
        email={session.email}
        role={session.platformRole}
        workspaceContext={{
          workspace,
          workspaces: workspacesResult.data,
          projects: projectsResult.ok ? projectsResult.data : [],
          projectsError: projectsResult.ok ? undefined : projectsResult.error.detail,
        }}
      />
      {children}
    </>
  )
}
