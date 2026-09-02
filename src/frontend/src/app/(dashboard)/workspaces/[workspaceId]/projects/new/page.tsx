import Link from "next/link"
import { CircleAlert } from "lucide-react"

import { CreateProjectForm } from "./create-project-form"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { listMyWorkspaces } from "@/lib/api/workspaces"

export default async function NewProjectPage({
  params,
}: {
  params: Promise<{ workspaceId: string }>
}) {
  const { workspaceId } = await params
  const workspaceResult = await listMyWorkspaces()
  const workspace = workspaceResult.ok
    ? workspaceResult.data.find(
        (item) => item.id.toLowerCase() === workspaceId.toLowerCase(),
      )
    : undefined
  const canManageProjects =
    workspace?.currentRole === "owner" || workspace?.currentRole === "editor"
  const projectsHref = `/workspaces/${workspaceId}/projects`

  if (!workspaceResult.ok) {
    return (
      <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Create project</h1>
        </div>
        <Alert variant="destructive">
          <CircleAlert />
          <AlertTitle>Unable to load workspace access</AlertTitle>
          <AlertDescription>{workspaceResult.error.detail}</AlertDescription>
        </Alert>
      </main>
    )
  }

  if (!canManageProjects) {
    return (
      <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Create project</h1>
          <p className="text-sm text-muted-foreground">
            Only workspace owners and editors can create projects.
          </p>
        </div>
        <Alert>
          <CircleAlert />
          <AlertTitle>Read-only workspace access</AlertTitle>
          <AlertDescription>
            Ask a workspace owner to change your role if you need to create a project.
          </AlertDescription>
        </Alert>
        <div>
          <Button
            nativeButton={false}
            variant="outline"
            render={<Link href={projectsHref} />}
          >
            Back to projects
          </Button>
        </div>
      </main>
    )
  }

  return (
    <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Create project</h1>
        <p className="text-sm text-muted-foreground">
          Create a project to organize content types and entries in this workspace.
        </p>
      </div>
      <CreateProjectForm workspaceId={workspaceId} />
    </main>
  )
}
