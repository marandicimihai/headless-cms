import Link from "next/link"
import { CircleAlert, FolderKanban, Plus } from "lucide-react"

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { getWorkspacePreview } from "@/lib/api/previews"

const dateFormatter = new Intl.DateTimeFormat("en-GB", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
  hourCycle: "h23",
  timeZone: "UTC",
})

export default async function WorkspacePreviewPage({ params }: {
  params: Promise<{ workspaceId: string }>
}) {
  const { workspaceId } = await params
  const result = await getWorkspacePreview(workspaceId)
  if (!result.ok) {
    return (
      <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
        <h1 className="text-2xl font-semibold tracking-tight">Workspace preview</h1>
        <Alert variant="destructive">
          <CircleAlert />
          <AlertTitle>Unable to load workspace preview</AlertTitle>
          <AlertDescription>{result.error.detail}</AlertDescription>
        </Alert>
      </main>
    )
  }
  const preview = result.data
  const workspaceHref = `/workspaces/${workspaceId}`
  const canManage = preview.currentRole === "owner" || preview.currentRole === "editor"

  return (
    <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
      <h1 className="text-2xl font-semibold tracking-tight">{preview.name}</h1>
      {preview.projectCount > 0 ? (
        <>
          <section aria-label="Workspace statistics" className="grid gap-4 sm:grid-cols-3">
            <Card size="sm">
              <CardHeader><CardTitle>Projects</CardTitle></CardHeader>
              <CardContent><p className="text-2xl font-semibold tabular-nums">{preview.projectCount.toLocaleString("en")}</p></CardContent>
            </Card>
            <Card size="sm">
              <CardHeader><CardTitle>Content entries</CardTitle></CardHeader>
              <CardContent className="space-y-1">
                <p className="text-2xl font-semibold tabular-nums">{preview.entryCount.toLocaleString("en")}</p>
                <CardDescription>{preview.publishedEntryCount.toLocaleString("en")} published · {preview.draftEntryCount.toLocaleString("en")} drafts</CardDescription>
              </CardContent>
            </Card>
            <Card size="sm">
              <CardHeader><CardTitle>Members</CardTitle></CardHeader>
              <CardContent className="space-y-1">
                <p className="text-2xl font-semibold tabular-nums">{preview.memberCount.toLocaleString("en")}</p>
                {preview.currentRole === "owner" && preview.pendingInvitationCount != null ? (
                  <CardDescription><Link className="underline underline-offset-4" href={`${workspaceHref}/manage`}>{preview.pendingInvitationCount.toLocaleString("en")} pending invitations</Link></CardDescription>
                ) : null}
              </CardContent>
            </Card>
          </section>
          <section className="space-y-3" aria-labelledby="projects-heading">
            <div className="flex items-center justify-between gap-3">
              <h2 id="projects-heading" className="text-sm font-semibold">Projects</h2>
              <Button nativeButton={false} variant="outline" size="sm" render={<Link href={`${workspaceHref}/projects`} />}>View projects</Button>
            </div>
            <div className="overflow-x-auto rounded-xl border">
              <Table>
                <TableHeader><TableRow><TableHead>Project</TableHead><TableHead>Content types</TableHead><TableHead>Entries</TableHead><TableHead>Last content update (UTC)</TableHead></TableRow></TableHeader>
                <TableBody>
                  {preview.projects.map(project => (
                    <TableRow key={project.id}>
                      <TableCell className="font-medium"><Link className="underline-offset-4 hover:underline focus-visible:underline" href={`${workspaceHref}/projects/${project.id}`}>{project.name}</Link></TableCell>
                      <TableCell>{project.contentTypeCount.toLocaleString("en")}</TableCell>
                      <TableCell>{project.entryCount.toLocaleString("en")}</TableCell>
                      <TableCell className="text-muted-foreground">{project.lastContentUpdatedAt ? <time dateTime={project.lastContentUpdatedAt}>{dateFormatter.format(new Date(project.lastContentUpdatedAt))}</time> : "No entries yet"}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          </section>
        </>
      ) : (
        <section className="flex min-h-80 flex-col items-center justify-center gap-4 text-center">
          <FolderKanban className="size-8 text-muted-foreground" />
          <div className="max-w-sm space-y-1">
            <h2 className="text-sm font-semibold">No projects yet</h2>
            <p className="text-sm text-muted-foreground">{canManage ? "Create a project to start defining content types and adding entries." : "Projects will appear here once an owner or editor creates them."}</p>
          </div>
          {canManage ? <Button nativeButton={false} render={<Link href={`${workspaceHref}/projects/new`} />}><Plus />Create project</Button> : null}
        </section>
      )}
    </main>
  )
}
