import Link from "next/link"
import { CircleAlert, Plus } from "lucide-react"

import { DeleteContentTypeButton } from "./delete-content-type-button"
import { DeleteEntryButton } from "./delete-entry-button"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
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
  const contentTypeHref = `${projectHref}/content-types/${contentTypeKey}`

  if (!typeResult.ok) {
    return (
      <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            Content type unavailable
          </h1>
          <p className="text-sm text-muted-foreground">
            The schema could not be loaded.
          </p>
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
            render={<Link href={projectHref} />}
          >
            Back to project
          </Button>
        </div>
      </main>
    )
  }

  const contentType = typeResult.data
  const entries = entriesResult.ok ? entriesResult.data.items : []

  return (
    <main className="flex flex-1 flex-col gap-8 p-4 md:p-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-2xl font-semibold tracking-tight">
              {contentType.key}
            </h1>
          </div>
          <p className="text-sm text-muted-foreground">
            {contentType.fields.length} fields define this entry schema.
          </p>
        </div>
        {canWrite ? (
          <Button
            nativeButton={false}
            render={<Link href={`${contentTypeHref}/entries/new`} />}
          >
            <Plus />
            Create entry
          </Button>
        ) : null}
      </div>

      <section className="space-y-3" aria-labelledby="schema-heading">
        <h2 id="schema-heading" className="text-sm font-semibold">
          Schema
        </h2>
        <div className="overflow-hidden border">
          <Table className="min-w-[40rem]">
            <TableHeader>
              <TableRow className="hover:bg-transparent">
                <TableHead>Key</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Rules</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {contentType.fields.map((field) => (
                <TableRow key={field.key}>
                  <TableCell className="font-mono text-sm font-medium">
                    {field.key}
                  </TableCell>
                  <TableCell>{field.type}</TableCell>
                  <TableCell className="text-muted-foreground">
                    {field.required ? "Required" : "Optional"}
                    {field.nullable ? " · nullable" : ""}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      </section>

      <section className="space-y-4" aria-labelledby="entries-heading">
        <div className="flex items-center justify-between">
          <div>
            <h2 id="entries-heading" className="text-lg font-semibold">
              Entries
            </h2>
            <p className="text-sm text-muted-foreground">
              {entriesResult.ok ? `${entriesResult.data.total} total` : ""}
            </p>
          </div>
        </div>

        {!entriesResult.ok ? (
          <Alert variant="destructive">
            <CircleAlert />
            <AlertTitle>Unable to load entries</AlertTitle>
            <AlertDescription>{entriesResult.error.detail}</AlertDescription>
          </Alert>
        ) : entries.length ? (
          <div className="overflow-auto border">
            <Table className="min-w-[44rem]">
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  {contentType.fields.map((field) => (
                    <TableHead key={field.key}>{field.key}</TableHead>
                  ))}
                  <TableHead>Status</TableHead>
                  {canWrite ? (
                    <TableHead className="text-right">Actions</TableHead>
                  ) : null}
                </TableRow>
              </TableHeader>
              <TableBody>
                {entries.map((entry) => (
                  <TableRow key={entry.id}>
                    {contentType.fields.map((field) => (
                      <TableCell key={field.key}>
                        {formatValue(entry.data[field.key])}
                      </TableCell>
                    ))}
                    <TableCell>
                      <Badge
                        variant={
                          entry.status === "published" ? "default" : "secondary"
                        }
                      >
                        {entry.status}
                      </Badge>
                    </TableCell>
                    {canWrite ? (
                      <TableCell>
                        <div className="flex justify-end gap-1">
                          <Button
                            nativeButton={false}
                            variant="ghost"
                            size="sm"
                            render={
                              <Link href={`${contentTypeHref}/entries/${entry.id}`} />
                            }
                          >
                            Edit
                          </Button>
                          <DeleteEntryButton
                            workspaceId={workspaceId}
                            projectId={projectId}
                            contentTypeKey={contentType.key}
                            entryId={entry.id}
                          />
                        </div>
                      </TableCell>
                    ) : null}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        ) : (
          <div className="flex min-h-44 flex-col items-center justify-center gap-3 border text-center">
            <p className="text-sm font-semibold">No entries yet</p>
            <p className="text-sm text-muted-foreground">
              {canWrite
                ? "Create the first entry using this schema."
                : "Entries added by editors will appear here."}
            </p>
            {canWrite ? (
              <Button
                nativeButton={false}
                size="sm"
                render={<Link href={`${contentTypeHref}/entries/new`} />}
              >
                Create entry
              </Button>
            ) : null}
          </div>
        )}
      </section>

      {canWrite ? (
        <section className="border-t pt-8">
          <h2 className="text-sm font-semibold">Delete content type</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Deleting this schema permanently removes every entry that uses it.
          </p>
          <DeleteContentTypeButton
            workspaceId={workspaceId}
            projectId={projectId}
            contentTypeKey={contentType.key}
            contentTypeKeyLabel={contentType.key}
          />
        </section>
      ) : null}
    </main>
  )
}

function formatValue(value: unknown) {
  if (value === null || value === undefined) return "—"
  if (typeof value === "boolean") return value ? "True" : "False"
  return String(value)
}
