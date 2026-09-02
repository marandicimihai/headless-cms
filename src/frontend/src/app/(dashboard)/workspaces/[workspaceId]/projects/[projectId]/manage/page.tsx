import { CircleAlert } from "lucide-react"

import { ProjectManagement } from "../project-management"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Badge } from "@/components/ui/badge"
import { getProject } from "@/lib/api/projects"
import { listMyWorkspaces } from "@/lib/api/workspaces"

const dateFormatter = new Intl.DateTimeFormat("en", {
  dateStyle: "medium",
  timeZone: "UTC",
})

function formatDate(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? "Unknown date" : dateFormatter.format(date)
}

export default async function ProjectManagementPage({
  params,
}: {
  params: Promise<{ workspaceId: string; projectId: string }>
}) {
  const { workspaceId, projectId } = await params
  const [workspaceResult, projectResult] = await Promise.all([
    listMyWorkspaces(),
    getProject(workspaceId, projectId),
  ])
  const workspace = workspaceResult.ok
    ? workspaceResult.data.find(
        (item) => item.id.toLowerCase() === workspaceId.toLowerCase(),
      )
    : undefined

  if (!workspaceResult.ok || !projectResult.ok || !workspace?.currentRole) {
    const detail = !projectResult.ok
      ? projectResult.error.detail
      : !workspaceResult.ok
        ? workspaceResult.error.detail
        : "This project is not available to your account."

    return (
      <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            Project unavailable
          </h1>
          <p className="text-sm text-muted-foreground">
            The project could not be loaded.
          </p>
        </div>
        <Alert variant="destructive">
          <CircleAlert />
          <AlertTitle>Unable to load project</AlertTitle>
          <AlertDescription>{detail}</AlertDescription>
        </Alert>
      </main>
    )
  }

  const project = projectResult.data
  const canManageProjects =
    workspace.currentRole === "owner" || workspace.currentRole === "editor"

  return (
    <main className="flex flex-1 flex-col gap-8 p-4 md:p-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Manage project</h1>
          <p className="text-sm text-muted-foreground">
            Update project settings and access.
          </p>
        </div>
        <Badge variant={canManageProjects ? "secondary" : "outline"}>
          {workspace.currentRole === "owner"
            ? "Owner"
            : workspace.currentRole === "editor"
              ? "Editor"
              : "Read only"}
        </Badge>
      </div>

      <dl className="grid gap-4 border-y py-5 sm:grid-cols-2">
        <div className="space-y-1">
          <dt className="text-sm text-muted-foreground">Created</dt>
          <dd className="text-sm font-medium">{formatDate(project.createdAt)}</dd>
        </div>
        <div className="space-y-1">
          <dt className="text-sm text-muted-foreground">Last updated</dt>
          <dd className="text-sm font-medium">{formatDate(project.updatedAt)}</dd>
        </div>
      </dl>

      {canManageProjects ? (
        <ProjectManagement
          workspaceId={workspaceId}
          projectId={project.id}
          projectName={project.name}
        />
      ) : (
        <section aria-labelledby="project-access-heading" className="space-y-2">
          <h2 id="project-access-heading" className="text-sm font-semibold">
            Project access
          </h2>
          <p className="max-w-2xl text-sm text-muted-foreground">
            You have read-only workspace access. Owners and editors can rename or
            delete this project.
          </p>
        </section>
      )}
    </main>
  )
}
