import { redirect } from "next/navigation"

import { DashboardBreadcrumbTrail } from "@/components/dashboard/dashboard-breadcrumb"
import { getSession } from "@/lib/api/session"
import { listMyWorkspaces } from "@/lib/api/workspaces"

export default async function WorkspaceBreadcrumbPage({
  params,
}: {
  params: Promise<{ workspaceId: string }>
}) {
  const session = await getSession()

  if (!session) {
    redirect("/auth/login")
  }

  const { workspaceId } = await params
  const result = await listMyWorkspaces(session.accessToken)
  const workspace = result.ok
    ? result.data.find(
        (item) => item.id.toLowerCase() === workspaceId.toLowerCase(),
      )
    : undefined

  return (
    <DashboardBreadcrumbTrail
      items={[
        { label: "Workspaces", href: "/workspaces" },
        { label: workspace?.name ?? "Workspace" },
      ]}
    />
  )
}
