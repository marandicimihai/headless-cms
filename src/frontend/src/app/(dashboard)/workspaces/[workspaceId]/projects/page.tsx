import Link from "next/link"
import { AlertCircle, FolderKanban, Plus } from "lucide-react"

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
import { listProjects } from "@/lib/api/projects"
import { listMyWorkspaces } from "@/lib/api/workspaces"

const dateFormatter = new Intl.DateTimeFormat("en", {
  dateStyle: "medium",
  timeZone: "UTC",
})

function formatDate(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? "Unknown date" : dateFormatter.format(date)
}

export default async function ProjectsPage({
  params,
}: {
  params: Promise<{ workspaceId: string }>
}) {
  const { workspaceId } = await params
  const [workspaceResult, projectsResult] = await Promise.all([
    listMyWorkspaces(),
    listProjects(workspaceId),
  ])
  const workspace = workspaceResult.ok
    ? workspaceResult.data.find(
        (item) => item.id.toLowerCase() === workspaceId.toLowerCase(),
      )
    : undefined
  const canManageProjects =
    workspace?.currentRole === "Owner" || workspace?.currentRole === "Editor"
  const projects = projectsResult.ok ? projectsResult.data : []
  const error = projectsResult.ok ? undefined : projectsResult.error.detail
  const projectsHref = `/workspaces/${workspaceId}/projects`

  return (
    <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Projects</h1>
          <p className="text-sm text-muted-foreground">
            Projects keep this workspace&apos;s content structures and entries separate.
          </p>
        </div>
        {canManageProjects ? (
          <Button
            nativeButton={false}
            render={<Link href={`${projectsHref}/new`} />}
          >
            <Plus />
            Create project
          </Button>
        ) : null}
      </div>

      {error ? (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertTitle>Unable to load projects</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      ) : projects.length ? (
        <div className="overflow-hidden rounded-xl border">
          <Table className="min-w-[38rem]">
            <TableHeader>
              <TableRow className="hover:bg-transparent">
                <TableHead>Project</TableHead>
                <TableHead>Created</TableHead>
                <TableHead>Last updated</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {projects.map((project) => (
                <TableRow key={project.id}>
                  <TableCell className="font-medium">
                    <Link
                      href={`${projectsHref}/${project.id}`}
                      className="outline-none hover:underline focus-visible:rounded-sm focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
                    >
                      {project.name}
                    </Link>
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {formatDate(project.createdAt)}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {formatDate(project.updatedAt)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      ) : (
        <section className="flex min-h-80 flex-col items-center justify-center gap-4 text-center">
          <FolderKanban className="size-8 text-muted-foreground" />
          <div className="max-w-sm space-y-1">
            <h2 className="text-sm font-semibold">No projects yet</h2>
            <p className="text-sm text-muted-foreground">
              {canManageProjects
                ? "Create a project before defining its content types and entries."
                : "No projects have been created in this workspace yet."}
            </p>
          </div>
          {canManageProjects ? (
            <Button
              nativeButton={false}
              render={<Link href={`${projectsHref}/new`} />}
            >
              <Plus />
              Create project
            </Button>
          ) : null}
        </section>
      )}
    </main>
  )
}
