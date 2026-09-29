import { CircleAlert } from "lucide-react"

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { SchemaAssistantChat } from "../schema-assistant-chat"
import { listMyWorkspaces } from "@/lib/api/workspaces"

export default async function SchemaAssistantPage({
  params,
}: {
  params: Promise<{ workspaceId: string; projectId: string }>
}) {
  const { workspaceId, projectId } = await params
  const result = await listMyWorkspaces()
  const workspace = result.ok
    ? result.data.find((item) => item.id.toLowerCase() === workspaceId.toLowerCase())
    : undefined
  const canWrite = workspace?.currentRole === "owner" || workspace?.currentRole === "editor"

  if (!canWrite) {
    return (
      <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
        <h1 className="text-2xl font-semibold tracking-tight">Schema assistant</h1>
        <Alert>
          <CircleAlert />
          <AlertTitle>Read-only workspace access</AlertTitle>
          <AlertDescription>Ask a workspace owner to change your role if you need to manage content.</AlertDescription>
        </Alert>
      </main>
    )
  }

  return <SchemaAssistantChat workspaceId={workspaceId} projectId={projectId} />
}
