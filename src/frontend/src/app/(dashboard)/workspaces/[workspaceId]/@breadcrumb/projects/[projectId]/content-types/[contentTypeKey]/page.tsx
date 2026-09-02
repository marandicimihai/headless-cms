import { DashboardBreadcrumbTrail } from "@/app/(dashboard)/workspaces/[workspaceId]/dashboard-breadcrumb"

export default async function ContentTypeBreadcrumbPage({
  params,
}: {
  params: Promise<{
    workspaceId: string
    projectId: string
    contentTypeKey: string
  }>
}) {
  const { workspaceId, projectId, contentTypeKey } = await params

  return (
    <DashboardBreadcrumbTrail
      items={[
        { label: "Projects", href: `/workspaces/${workspaceId}/projects` },
        { label: "Project", href: `/workspaces/${workspaceId}/projects/${projectId}` },
        { label: contentTypeKey },
      ]}
    />
  )
}
