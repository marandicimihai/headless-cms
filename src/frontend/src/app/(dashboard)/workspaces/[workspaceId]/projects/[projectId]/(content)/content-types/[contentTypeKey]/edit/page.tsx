import { CircleAlert } from "lucide-react"

import { ContentTypeForm } from "../../../../content-type-form"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { getContentType } from "@/lib/api/content"
import { listMyWorkspaces } from "@/lib/api/workspaces"

export default async function EditContentTypePage({
  params,
}: {
  params: Promise<{
    workspaceId: string
    projectId: string
    contentTypeKey: string
  }>
}) {
  const { workspaceId, projectId, contentTypeKey } = await params
  const [typeResult, workspaceResult] = await Promise.all([
    getContentType(workspaceId, projectId, contentTypeKey),
    listMyWorkspaces(),
  ])

  if (!typeResult.ok) {
    return (
      <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            Content type unavailable
          </h1>
        </div>
        <Alert variant="destructive">
          <CircleAlert />
          <AlertTitle>Unable to load content type</AlertTitle>
          <AlertDescription>{typeResult.error.detail}</AlertDescription>
        </Alert>
      </main>
    )
  }

  const canWrite = workspaceResult.ok && workspaceResult.data.some(
    (workspace) =>
      workspace.id.toLowerCase() === workspaceId.toLowerCase() &&
      (workspace.currentRole === "owner" || workspace.currentRole === "editor"),
  )

  if (!canWrite) {
    return (
      <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            Edit content type
          </h1>
        </div>
        <Alert>
          <AlertTitle>Read-only workspace access</AlertTitle>
        </Alert>
      </main>
    )
  }

  return (
    <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">
          Edit {typeResult.data.key} content type
        </h1>
      </div>
      <ContentTypeForm
        workspaceId={workspaceId}
        projectId={projectId}
        mode="edit"
        initialContentType={typeResult.data}
      />
    </main>
  )
}
