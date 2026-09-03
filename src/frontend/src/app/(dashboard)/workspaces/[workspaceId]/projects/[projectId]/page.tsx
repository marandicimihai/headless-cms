import Link from "next/link"
import { CircleAlert, FolderCog, Library } from "lucide-react"

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { getProject } from "@/lib/api/projects"

export default async function ProjectPreviewPage({
  params,
}: {
  params: Promise<{ workspaceId: string; projectId: string }>
}) {
  const { workspaceId, projectId } = await params
  const projectResult = await getProject(workspaceId, projectId)
  const projectHref = `/workspaces/${workspaceId}/projects/${projectId}`

  if (!projectResult.ok) {
    return (
      <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            Project unavailable
          </h1>
        </div>
        <Alert variant="destructive">
          <CircleAlert />
          <AlertTitle>Unable to load project</AlertTitle>
          <AlertDescription>{projectResult.error.detail}</AlertDescription>
        </Alert>
      </main>
    )
  }

  return (
    <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">
          {projectResult.data.name}
        </h1>
      </div>

      <div className="flex flex-col gap-3 py-5 sm:flex-row">
        <Button
          nativeButton={false}
          render={<Link href={`${projectHref}/content`} />}
        >
          <Library />
          Open content
        </Button>
        <Button
          nativeButton={false}
          variant="outline"
          render={<Link href={`${projectHref}/manage`} />}
        >
          <FolderCog />
          Manage project
        </Button>
      </div>
    </main>
  )
}
