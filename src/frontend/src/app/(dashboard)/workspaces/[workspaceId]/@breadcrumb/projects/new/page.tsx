import { redirect } from "next/navigation"

import { DashboardBreadcrumbTrail } from "@/app/(dashboard)/workspaces/[workspaceId]/dashboard-breadcrumb"
import { getSession } from "@/lib/api/session"
import { listMyWorkspaces } from "@/lib/api/workspaces"

export default async function NewProjectBreadcrumbPage({
  params,
}: {
  params: Promise<{ workspaceId: string }>
}) {
  const session = await getSession()

  if (!session) redirect("/auth/login")

  const { workspaceId } = await params
  const result = await listMyWorkspaces()
  const workspace = result.ok
    ? result.data.find(
        (item) => item.id.toLowerCase() === workspaceId.toLowerCase(),
      )
    : undefined
  const projectsHref = `/workspaces/${workspaceId}/projects`

  return (
    <DashboardBreadcrumbTrail
      items={[
        { label: workspace?.name ?? "Workspace", href: `/workspaces/${workspaceId}` },
        { label: "Projects", href: projectsHref },
        { label: "Create project" },
      ]}
    />
  )
}
