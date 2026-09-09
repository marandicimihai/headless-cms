import Link from "next/link"
import { CircleAlert } from "lucide-react"

import { ContentTypeWorkbench } from "./content-type-workbench"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { getContentEntry, getContentType, listContentEntries } from "@/lib/api/content"
import { listMyWorkspaces } from "@/lib/api/workspaces"
import type { ContentEntryFilter, ContentEntryFilterOperator } from "@/lib/types/content"

const supportedFilterOperators = new Set<ContentEntryFilterOperator>([
  "eq",
  "contains",
  "gt",
  "gte",
  "lt",
  "lte",
])

function firstValue(value: string | string[] | undefined) {
  return typeof value === "string" ? value : value?.[0]
}

function parseFilters(searchParams: Record<string, string | string[] | undefined>) {
  const filters: ContentEntryFilter[] = []
  const filterPattern = /^filter\[([$a-z][a-zA-Z0-9_]*)\]\[([a-z]+)\]$/

  for (const [key, value] of Object.entries(searchParams)) {
    const match = filterPattern.exec(key)
    if (!match || !supportedFilterOperators.has(match[2] as ContentEntryFilterOperator)) {
      continue
    }

    for (const filterValue of Array.isArray(value) ? value : [value]) {
      if (typeof filterValue !== "string" || !filterValue) continue
      filters.push({
        field: match[1],
        operator: match[2] as ContentEntryFilterOperator,
        value: filterValue,
      })
    }
  }

  return filters
}

export default async function ContentTypePage({
  params,
  searchParams,
}: {
  params: Promise<{
    workspaceId: string
    projectId: string
    contentTypeKey: string
  }>
  searchParams: Promise<Record<string, string | string[] | undefined>>
}) {
  const { workspaceId, projectId, contentTypeKey } = await params
  const query = await searchParams
  const selectedEntryId = firstValue(query.entry)
  const sort = firstValue(query.sort)
  const filters = parseFilters(query)
  const [workspaceResult, typeResult, entriesResult, selectedEntryResult] = await Promise.all([
    listMyWorkspaces(),
    getContentType(workspaceId, projectId, contentTypeKey),
    listContentEntries(workspaceId, projectId, contentTypeKey, { filters, sort }),
    selectedEntryId
      ? getContentEntry(workspaceId, projectId, contentTypeKey, selectedEntryId)
      : Promise.resolve(undefined),
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
        filters={filters}
        sort={sort}
        selectedEntry={selectedEntryResult?.ok ? selectedEntryResult.data : undefined}
      />
    </main>
  )
}
