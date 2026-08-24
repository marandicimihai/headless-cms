import { DashboardBreadcrumbTrail } from "@/components/dashboard/dashboard-breadcrumb"

export default function NewWorkspaceBreadcrumbPage() {
  return (
    <DashboardBreadcrumbTrail
      items={[
        { label: "Workspaces", href: "/workspaces" },
        { label: "New" },
      ]}
    />
  )
}
