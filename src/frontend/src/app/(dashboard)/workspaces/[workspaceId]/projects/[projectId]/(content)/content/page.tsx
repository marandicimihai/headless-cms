import Link from "next/link"
import { CircleAlert, Database, Plus } from "lucide-react"

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { getProject } from "@/lib/api/projects"
import { listMyWorkspaces } from "@/lib/api/workspaces"

export default async function ProjectContentPage({
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
  const canWrite =
    workspace?.currentRole === "owner" || workspace?.currentRole === "editor"
  const projectHref = `/workspaces/${workspaceId}/projects/${projectId}`
  const contentTypesHref = `${projectHref}/content-types`

  if (!projectResult.ok || !workspace?.currentRole) {
    const detail = !projectResult.ok
      ? projectResult.error.detail
      : !workspaceResult.ok
        ? workspaceResult.error.detail
        : "This project is not available to your account."

    return (
      <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            Content unavailable
          </h1>
        </div>
        <Alert variant="destructive">
          <CircleAlert />
          <AlertTitle>Unable to load content</AlertTitle>
          <AlertDescription>{detail}</AlertDescription>
        </Alert>
      </main>
    )
  }

  return (
    <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Content</h1>
      </div>
      <section className="flex min-h-80 flex-col items-center justify-center gap-4 text-center">
        <Database className="size-8 text-muted-foreground" />
        <div className="max-w-sm space-y-1">
          <h2 className="text-sm font-semibold">Select a content type</h2>
          <p className="text-sm text-muted-foreground">
            Choose a content type from the sidebar to view and manage its entries.
          </p>
        </div>
        {canWrite ? (
          <Button
            nativeButton={false}
            render={<Link href={`${contentTypesHref}/new`} />}
          >
            <Plus />
            Create content type
          </Button>
        ) : null}
      </section>
    </main>
  )
}
