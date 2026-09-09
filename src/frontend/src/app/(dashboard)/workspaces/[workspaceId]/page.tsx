import Link from "next/link"
import { CircleAlert, FolderKanban, Plus } from "lucide-react"

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { listProjects } from "@/lib/api/projects"
import { listMyWorkspaces } from "@/lib/api/workspaces"

const dateFormatter = new Intl.DateTimeFormat("en-GB", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
  hourCycle: "h23",
  timeZone: "UTC",
})

export default async function WorkspaceProjectsPage({ params }: {
  params: Promise<{ workspaceId: string }>
}) {
  const { workspaceId } = await params
  const [workspaceResult, projectsResult] = await Promise.all([
    listMyWorkspaces(),
    listProjects(workspaceId),
  ])

  if (!workspaceResult.ok) {
    return (
      <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
        <h1 className="text-2xl font-semibold tracking-tight">Projects</h1>
        <Alert variant="destructive">
          <CircleAlert />
          <AlertTitle>Unable to load workspace</AlertTitle>
          <AlertDescription>{workspaceResult.error.detail}</AlertDescription>
        </Alert>
      </main>
    )
  }

  const workspace = workspaceResult.data.find(
    (item) => item.id.toLowerCase() === workspaceId.toLowerCase(),
  )
  const projects = projectsResult.ok ? projectsResult.data : []
  const workspaceHref = `/workspaces/${workspaceId}`
  const projectsHref = `${workspaceHref}/projects`
  const canManage = workspace?.currentRole === "owner" || workspace?.currentRole === "editor"

  return (
    <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Projects</h1>
          <p className="text-sm text-muted-foreground">Choose a project to manage its content.</p>
        </div>
        {canManage ? <Button nativeButton={false} render={<Link href={`${projectsHref}/new`} />}><Plus />Create project</Button> : null}
      </div>

      {!projectsResult.ok ? (
        <Alert variant="destructive">
          <CircleAlert />
          <AlertTitle>Unable to load projects</AlertTitle>
          <AlertDescription>{projectsResult.error.detail}</AlertDescription>
        </Alert>
      ) : projects.length ? (
        <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3" aria-label="Projects">
          {projects.map((project) => (
            <Link
              key={project.id}
              href={`${projectsHref}/${project.id}`}
              className="group rounded-xl outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
            >
              <Card className="h-full transition-colors group-hover:border-foreground/30">
                <CardHeader>
                  <CardTitle>{project.name}</CardTitle>
                  <CardDescription>Open project</CardDescription>
                </CardHeader>
                <CardContent className="text-sm text-muted-foreground">
                  Updated <time dateTime={project.updatedAt}>{dateFormatter.format(new Date(project.updatedAt))} UTC</time>
                </CardContent>
              </Card>
            </Link>
          ))}
        </section>
      ) : (
        <section className="flex min-h-80 flex-col items-center justify-center gap-4 text-center">
          <FolderKanban className="size-8 text-muted-foreground" />
          <div className="max-w-sm space-y-1">
            <h2 className="text-sm font-semibold">No projects yet</h2>
            <p className="text-sm text-muted-foreground">{canManage ? "Create a project to start defining content types and adding entries." : "Projects will appear here once an owner or editor creates them."}</p>
          </div>
          {canManage ? <Button nativeButton={false} render={<Link href={`${projectsHref}/new`} />}><Plus />Create project</Button> : null}
        </section>
      )}
    </main>
  )
}
