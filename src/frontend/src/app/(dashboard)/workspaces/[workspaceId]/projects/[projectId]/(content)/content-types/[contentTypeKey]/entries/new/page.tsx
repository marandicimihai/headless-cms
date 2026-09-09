import { getContentType } from "@/lib/api/content"
import { listMyWorkspaces } from "@/lib/api/workspaces"

import { createContentEntryAction } from "../../../../../content-actions"
import { EntryEditor } from "../../entry-editor"
import { Alert, AlertTitle } from "@/components/ui/alert"

export default async function NewEntryPage({
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

  if (!typeResult.ok) return null

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
            Create entry
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
          Create {typeResult.data.key} entry
        </h1>
      </div>
      <EntryEditor
        workspaceId={workspaceId}
        projectId={projectId}
        contentTypeKey={contentTypeKey}
        fields={typeResult.data.fields}
        action={createContentEntryAction.bind(
          null,
          workspaceId,
          projectId,
          contentTypeKey,
          typeResult.data.fields,
        )}
      />
    </main>
  )
}
