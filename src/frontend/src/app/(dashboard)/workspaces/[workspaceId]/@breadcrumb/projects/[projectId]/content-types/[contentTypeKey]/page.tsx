import { redirect } from "next/navigation"

import { DashboardBreadcrumbTrail } from "@/app/(dashboard)/workspaces/[workspaceId]/dashboard-breadcrumb"
import { getProject } from "@/lib/api/projects"
import { getSession } from "@/lib/api/session"
import { listMyWorkspaces } from "@/lib/api/workspaces"

export default async function ContentTypeBreadcrumbPage({
  params,
}: {
  params: Promise<{
    workspaceId: string
    projectId: string
    contentTypeKey: string
  }>
}) {
  const session = await getSession()

  if (!session) redirect("/auth/login")

  const { workspaceId, projectId, contentTypeKey } = await params
  const [workspaceResult, projectResult] = await Promise.all([
    listMyWorkspaces(),
    getProject(workspaceId, projectId),
  ])
  const workspace = workspaceResult.ok
    ? workspaceResult.data.find(
        (item) => item.id.toLowerCase() === workspaceId.toLowerCase(),
      )
    : undefined
  const projectHref = `/workspaces/${workspaceId}/projects/${projectId}`

  return (
    <DashboardBreadcrumbTrail
      items={[
        { label: workspace?.name ?? "Workspace", href: `/workspaces/${workspaceId}` },
        { label: "Projects", href: `/workspaces/${workspaceId}/projects` },
        {
          label: projectResult.ok ? projectResult.data.name : "Project",
          href: projectHref,
        },
        { label: "Content", href: `${projectHref}/content` },
        { label: contentTypeKey },
      ]}
    />
  )
}
