import Link from "next/link"
import { CircleAlert, FolderCog, Library, Plus } from "lucide-react"

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { getProjectPreview } from "@/lib/api/previews"

export default async function ProjectPreviewPage({ params }: {
  params: Promise<{ workspaceId: string; projectId: string }>
}) {
  const { workspaceId, projectId } = await params
  const result = await getProjectPreview(workspaceId, projectId)
  const projectHref = `/workspaces/${workspaceId}/projects/${projectId}`

  if (!result.ok) {
    return (
      <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
        <h1 className="text-2xl font-semibold tracking-tight">Project preview</h1>
        <Alert variant="destructive">
          <CircleAlert />
          <AlertTitle>Unable to load project preview</AlertTitle>
          <AlertDescription>{result.error.detail}</AlertDescription>
        </Alert>
      </main>
    )
  }
  const preview = result.data
  const canManage = preview.currentRole === "owner" || preview.currentRole === "editor"

  return (
    <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <h1 className="text-2xl font-semibold tracking-tight">{preview.name}</h1>
        <div className="flex flex-wrap gap-3">
          <Button nativeButton={false} render={<Link href={`${projectHref}/content`} />}><Library />Open content</Button>
          {canManage ? <Button nativeButton={false} variant="outline" render={<Link href={`${projectHref}/manage`} />}><FolderCog />Manage project</Button> : null}
        </div>
      </div>
      {preview.contentTypeCount > 0 ? (
        <>
          <section aria-label="Project statistics" className="grid gap-4 sm:grid-cols-3">
            {[
              { label: "Content types", value: preview.contentTypeCount },
              { label: "Published entries", value: preview.publishedEntryCount },
              { label: "Draft entries", value: preview.draftEntryCount },
            ].map(stat => (
              <Card size="sm" key={stat.label}>
                <CardHeader><CardTitle>{stat.label}</CardTitle></CardHeader>
                <CardContent><p className="text-2xl font-semibold tabular-nums">{stat.value.toLocaleString("en")}</p></CardContent>
              </Card>
            ))}
          </section>
          {preview.recentEntries.length > 0 ? (
            <section className="space-y-3" aria-labelledby="recent-entries-heading">
              <h2 id="recent-entries-heading" className="text-sm font-semibold">Recently updated entries</h2>
              <div className="overflow-x-auto rounded-xl border">
                <Table>
                  <TableHeader><TableRow><TableHead>Entry</TableHead><TableHead>Content type</TableHead><TableHead>Status</TableHead></TableRow></TableHeader>
                  <TableBody>
                    {preview.recentEntries.map(entry => (
                      <TableRow key={entry.id}>
                        <TableCell className="font-medium"><Link className="underline-offset-4 hover:underline focus-visible:underline" href={`${projectHref}/content-types/${encodeURIComponent(entry.contentTypeKey)}?entry=${encodeURIComponent(entry.id)}`}>Entry {entry.id.slice(0, 8)}</Link></TableCell>
                        <TableCell>{entry.contentTypeKey}</TableCell>
                        <TableCell><Badge variant={entry.status === "published" ? "secondary" : "outline"}>{entry.status === "published" ? "Published" : "Draft"}</Badge></TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            </section>
          ) : (
            <section className="flex min-h-48 flex-col items-center justify-center gap-4 text-center">
              <h2 className="text-sm font-semibold">No entries yet</h2>
              <p className="text-sm text-muted-foreground">{canManage ? "Open a content type to add your first entry." : "Entries will appear here once an owner or editor adds them."}</p>
              <Button nativeButton={false} variant="outline" render={<Link href={`${projectHref}/content`} />}>View content types</Button>
            </section>
          )}
        </>
      ) : (
        <section className="flex min-h-80 flex-col items-center justify-center gap-4 text-center">
          <Library className="size-8 text-muted-foreground" />
          <div className="max-w-sm space-y-1">
            <h2 className="text-sm font-semibold">No content types yet</h2>
            <p className="text-sm text-muted-foreground">{canManage ? "Define your first content type, then add entries to this project." : "Content types will appear here once an owner or editor creates them."}</p>
          </div>
          {canManage ? <Button nativeButton={false} render={<Link href={`${projectHref}/content-types/new`} />}><Plus />Create content type</Button> : null}
        </section>
      )}
    </main>
  )
}
