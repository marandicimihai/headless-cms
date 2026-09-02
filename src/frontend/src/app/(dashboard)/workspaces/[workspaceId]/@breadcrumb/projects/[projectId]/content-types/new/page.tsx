import { DashboardBreadcrumbTrail } from "@/app/(dashboard)/workspaces/[workspaceId]/dashboard-breadcrumb"

export default async function NewContentTypeBreadcrumbPage({
  params,
}: {
  params: Promise<{ workspaceId: string; projectId: string }>
}) {
  const { workspaceId, projectId } = await params

  return (
    <DashboardBreadcrumbTrail
      items={[
        { label: "Projects", href: `/workspaces/${workspaceId}/projects` },
        { label: "Project", href: `/workspaces/${workspaceId}/projects/${projectId}` },
        { label: "Create content type" },
      ]}
    />
  )
}
