import Link from "next/link"
import { CircleAlert } from "lucide-react"

import { ContentTypeWorkbench } from "./content-type-workbench"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { getContentType, listContentEntries } from "@/lib/api/content"
import { listMyWorkspaces } from "@/lib/api/workspaces"

export default async function ContentTypePage({
  params,
}: {
  params: Promise<{
    workspaceId: string
    projectId: string
    contentTypeKey: string
  }>
}) {
  const { workspaceId, projectId, contentTypeKey } = await params
  const [workspaceResult, typeResult, entriesResult] = await Promise.all([
    listMyWorkspaces(),
    getContentType(workspaceId, projectId, contentTypeKey),
    listContentEntries(workspaceId, projectId, contentTypeKey),
  ])
  const workspace = workspaceResult.ok
    ? workspaceResult.data.find(
        (item) => item.id.toLowerCase() === workspaceId.toLowerCase(),
      )
    : undefined
  const canWrite =
    workspace?.currentRole === "owner" || workspace?.currentRole === "editor"
  const projectHref = `/workspaces/${workspaceId}/projects/${projectId}`
  const contentHref = `${projectHref}/content`

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
        <div>
          <Button
            nativeButton={false}
            variant="outline"
            render={<Link href={contentHref} />}
          >
            Back to content
          </Button>
        </div>
      </main>
    )
  }

  const contentType = typeResult.data
  return (
    <main className="flex flex-1 flex-col gap-8 p-4 md:p-6">
      <ContentTypeWorkbench
        workspaceId={workspaceId}
        projectId={projectId}
        contentType={contentType}
        entries={entriesResult.ok ? entriesResult.data.items : []}
        entriesError={entriesResult.ok ? null : entriesResult.error.detail}
        canWrite={canWrite}
      />
    </main>
  )
}
