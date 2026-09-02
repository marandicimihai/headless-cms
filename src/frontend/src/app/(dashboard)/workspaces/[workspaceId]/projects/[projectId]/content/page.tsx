import Link from "next/link"
import { CircleAlert, Database, Plus } from "lucide-react"

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { listContentTypes } from "@/lib/api/content"
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

export default async function ProjectContentPage({
  params,
}: {
  params: Promise<{ workspaceId: string; projectId: string }>
}) {
  const { workspaceId, projectId } = await params
  const [workspaceResult, projectResult, contentTypesResult] = await Promise.all([
    listMyWorkspaces(),
    getProject(workspaceId, projectId),
    listContentTypes(workspaceId, projectId),
  ])
  const workspace = workspaceResult.ok
    ? workspaceResult.data.find(
        (item) => item.id.toLowerCase() === workspaceId.toLowerCase(),
      )
    : undefined
  const canWrite =
    workspace?.currentRole === "owner" || workspace?.currentRole === "editor"
  const contentTypes = contentTypesResult.ok ? contentTypesResult.data : []
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
          <p className="text-sm text-muted-foreground">
            The project content could not be loaded.
          </p>
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
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Content</h1>
          <p className="text-sm text-muted-foreground">
            Define content types and manage their entries in {projectResult.data.name}.
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
      </div>

      {!contentTypesResult.ok ? (
        <Alert variant="destructive">
          <CircleAlert />
          <AlertTitle>Unable to load content types</AlertTitle>
          <AlertDescription>{contentTypesResult.error.detail}</AlertDescription>
        </Alert>
      ) : contentTypes.length ? (
        <div className="overflow-x-auto rounded-xl border">
          <Table className="min-w-[42rem]">
            <TableHeader>
              <TableRow className="hover:bg-transparent">
                <TableHead>Content type</TableHead>
                <TableHead>Fields</TableHead>
                <TableHead>Created</TableHead>
                <TableHead>Last updated</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {contentTypes.map((contentType) => (
                <TableRow key={contentType.id}>
                  <TableCell className="font-medium">
                    <Link
                      href={`${contentTypesHref}/${contentType.key}`}
                      className="font-mono text-sm outline-none hover:underline focus-visible:rounded-sm focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
                    >
                      {contentType.key}
                    </Link>
                  </TableCell>
                  <TableCell>{contentType.fields.length}</TableCell>
                  <TableCell className="text-muted-foreground">
                    {formatDate(contentType.createdAt)}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {formatDate(contentType.updatedAt)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      ) : (
        <section className="flex min-h-80 flex-col items-center justify-center gap-4 text-center">
          <Database className="size-8 text-muted-foreground" />
          <div className="max-w-sm space-y-1">
            <h2 className="text-sm font-semibold">No content types yet</h2>
            <p className="text-sm text-muted-foreground">
              {canWrite
                ? "Create a schema to start entering content."
                : "This project has no content schemas yet."}
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
      )}
    </main>
  )
}
