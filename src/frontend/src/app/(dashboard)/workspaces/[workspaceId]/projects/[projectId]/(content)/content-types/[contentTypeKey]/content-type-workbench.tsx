"use client"

import { type KeyboardEvent, useState } from "react"
import { usePathname, useRouter, useSearchParams } from "next/navigation"
import { ArrowDown, ArrowUp, ChevronsUpDown, Funnel, Plus, X } from "lucide-react"

import {
  createContentEntryAction,
  updateContentEntryAction,
} from "../../../content-actions"
import { ContentTypeForm } from "../../../content-type-form"
import { ContentTypeSettings } from "./content-type-settings"
import { EntryActions } from "./entry-actions"
import { EntryEditor } from "./entry-editor"
import { CopyableId } from "@/components/copyable-id"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import {
  Popover,
  PopoverContent,
  PopoverHeader,
  PopoverTitle,
  PopoverTrigger,
} from "@/components/ui/popover"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import type {
  ContentEntry,
  ContentEntryFilter,
  ContentEntryFilterOperator,
  ContentFieldType,
  ContentType,
} from "@/lib/types/content"

type EditorPanel =
  | { kind: "schema" }
  | { kind: "entry"; entry?: ContentEntry }
  | null

type FilterField = {
  key: string
  label: string
  type: ContentFieldType | "id" | "status" | "timestamp"
}

type DraftFilter = Partial<ContentEntryFilter>

const defaultSort = "-$updatedAt"
const systemFilterFields: FilterField[] = [
  { key: "$id", label: "$id", type: "id" },
  { key: "$status", label: "$status", type: "status" },
  { key: "$createdAt", label: "$createdAt", type: "timestamp" },
  { key: "$updatedAt", label: "$updatedAt", type: "timestamp" },
]

const entryTimestampFormatter = new Intl.DateTimeFormat("en-GB", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
  hourCycle: "h23",
  timeZone: "UTC",
})

export function ContentTypeWorkbench({
  workspaceId,
  projectId,
  contentType,
  entries,
  entriesError,
  canWrite,
  filters,
  sort,
  selectedEntry,
}: {
  workspaceId: string
  projectId: string
  contentType: ContentType
  entries: ContentEntry[]
  entriesError: string | null
  canWrite: boolean
  filters: ContentEntryFilter[]
  sort?: string
  selectedEntry?: ContentEntry
}) {
  const pathname = usePathname()
  const router = useRouter()
  const searchParams = useSearchParams()
  const [panel, setPanel] = useState<EditorPanel>(
    canWrite && selectedEntry ? { kind: "entry", entry: selectedEntry } : null,
  )
  const [filtersOpen, setFiltersOpen] = useState(false)
  const [draftFilters, setDraftFilters] = useState<DraftFilter[]>([])
  const filterFields: FilterField[] = [
    ...systemFilterFields,
    ...contentType.fields.map((field) => ({
      key: field.key,
      label: field.key,
      type: field.type,
    })),
  ]
  const effectiveSort = sort || defaultSort

  function openFromKeyboard(
    event: KeyboardEvent<HTMLTableRowElement>,
    open: () => void,
  ) {
    if (event.key !== "Enter" && event.key !== " ") return

    event.preventDefault()
    open()
  }

  function updateSearchParams(
    update: (params: URLSearchParams) => void,
  ) {
    const params = new URLSearchParams(searchParams.toString())
    params.delete("page")
    update(params)
    const query = params.toString()
    router.push(query ? `${pathname}?${query}` : pathname, { scroll: false })
  }

  function sortDirection(field: string) {
    if (effectiveSort === field) return "ascending" as const
    if (effectiveSort === `-${field}`) return "descending" as const
    return undefined
  }

  function toggleSort(field: string) {
    const direction = sortDirection(field)
    const nextSort =
      field === "$updatedAt"
        ? direction === "ascending"
          ? "-$updatedAt"
          : "$updatedAt"
        : direction === "ascending"
          ? `-${field}`
          : direction === "descending"
            ? undefined
            : field

    updateSearchParams((params) => {
      if (nextSort) params.set("sort", nextSort)
      else params.delete("sort")
    })
  }

  function replaceFilters(nextFilters: ContentEntryFilter[]) {
    updateSearchParams((params) => {
      for (const key of Array.from(params.keys())) {
        if (key.startsWith("filter[")) params.delete(key)
      }
      for (const filter of nextFilters) {
        params.append(
          `filter[${filter.field}][${filter.operator}]`,
          filter.value,
        )
      }
    })
  }

  function isCompleteFilter(filter: DraftFilter): filter is ContentEntryFilter {
    if (!filter.field || !filter.operator || !filter.value?.trim()) return false

    const field = filterFields.find((item) => item.key === filter.field)
    if (!field || !filterOperators(field.type).some((item) => item.value === filter.operator)) {
      return false
    }

    if (field.type === "number") return Number.isFinite(Number(filter.value))
    if (field.type === "boolean") return filter.value === "true" || filter.value === "false"
    if (field.type === "status") return filter.value === "draft" || filter.value === "published"
    if (field.type === "id") {
      return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(filter.value)
    }
    if (field.type === "timestamp") {
      return filter.value.includes("T") && !Number.isNaN(Date.parse(filter.value))
    }

    return true
  }

  function commitDraftFilters(nextDraftFilters = draftFilters) {
    replaceFilters(nextDraftFilters.filter(isCompleteFilter))
  }

  function openFilters(open: boolean) {
    if (open) {
      setDraftFilters(filters)
    } else {
      commitDraftFilters()
    }
    setFiltersOpen(open)
  }

  function updateDraftFilter(index: number, patch: DraftFilter) {
    setDraftFilters((current) =>
      current.map((filter, filterIndex) =>
        filterIndex === index ? { ...filter, ...patch } : filter,
      ),
    )
  }

  function addFilter() {
    const committedFilters = draftFilters.filter(isCompleteFilter)
    commitDraftFilters(committedFilters)
    setDraftFilters([...committedFilters, {}])
  }

  function removeFilter(index: number) {
    const nextFilters = draftFilters.filter((_, filterIndex) => filterIndex !== index)
    setDraftFilters(nextFilters)
    commitDraftFilters(nextFilters)
  }

  function clearFilters() {
    setDraftFilters([])
    replaceFilters([])
  }

  const closePanel = () => setPanel(null)
  const panelTitle =
    panel?.kind === "schema"
      ? `Edit ${contentType.key} schema`
      : panel?.entry
        ? `Edit ${contentType.key} entry`
        : `Create ${contentType.key} entry`
  const entryTableWidth =
    32 + 208 + contentType.fields.length * 176 + 128 + 176 + 176 + (canWrite ? 32 : 0)

  return (
    <>
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
          <h1 className="min-w-0 text-2xl font-semibold tracking-tight">
            {contentType.key}
          </h1>
          <CopyableId id={contentType.id} />
        </div>
        {canWrite ? (
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" onClick={() => setPanel({ kind: "schema" })}>
              Edit content type
            </Button>
            <Button onClick={() => setPanel({ kind: "entry" })}>
              <Plus />
              Create entry
            </Button>
          </div>
        ) : null}
      </div>

      <Tabs defaultValue="entries" className="gap-5">
        <TabsList variant="line" aria-label="Content type sections">
          <TabsTrigger value="entries">Entries</TabsTrigger>
          <TabsTrigger value="schema">Schema</TabsTrigger>
          <TabsTrigger value="settings">Settings</TabsTrigger>
        </TabsList>

        <TabsContent value="schema">
          <div className="overflow-x-auto rounded-xl border">
            <Table className="min-w-3xl">
              <TableHeader>
                <TableRow className="h-11 hover:bg-transparent">
                  <TableHead className="h-11">Key</TableHead>
                  <TableHead className="h-11">Type</TableHead>
                  <TableHead className="h-11">Rules</TableHead>
                  <TableHead className="h-11">Default</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {contentType.fields.map((field) => (
                  <TableRow
                    key={field.key}
                    className={canWrite ? "h-10 cursor-pointer focus-visible:outline focus-visible:outline-ring" : "h-10"}
                    tabIndex={canWrite ? 0 : undefined}
                    onClick={canWrite ? () => setPanel({ kind: "schema" }) : undefined}
                    onKeyDown={
                      canWrite
                        ? (event) =>
                            openFromKeyboard(event, () => setPanel({ kind: "schema" }))
                        : undefined
                    }
                  >
                    <TableCell className="h-10 py-2 font-mono text-sm font-medium">
                      {field.key}
                    </TableCell>
                    <TableCell className="h-10 py-2">{field.type}</TableCell>
                    <TableCell className="h-10 py-2 text-muted-foreground">
                      {field.required ? "Required" : "Optional"}
                    </TableCell>
                    <TableCell className="h-10 py-2 text-muted-foreground">
                      {Object.prototype.hasOwnProperty.call(field.settings, "default")
                        ? formatValue(field.settings.default)
                        : "—"}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </TabsContent>

        <TabsContent value="settings">
          <ContentTypeSettings
            workspaceId={workspaceId}
            projectId={projectId}
            contentTypeKey={contentType.key}
            canWrite={canWrite}
          />
        </TabsContent>

        <TabsContent value="entries">
          <div className="mb-3 flex justify-start">
            <Popover open={filtersOpen} onOpenChange={openFilters}>
              <PopoverTrigger
                render={<Button variant="outline" size="sm" aria-label="Filter entries" />}
              >
                <Funnel />
                Filter
                {filters.length ? (
                  <span className="flex size-4 items-center justify-center rounded-full bg-muted text-[0.65rem] tabular-nums">
                    {filters.length}
                  </span>
                ) : null}
              </PopoverTrigger>
              <PopoverContent align="start" className="w-[min(34rem,calc(100vw-2rem))] max-w-none gap-3">
                <PopoverHeader className="flex-row items-center justify-between gap-3">
                  <PopoverTitle>Filter entries</PopoverTitle>
                  {filters.length ? (
                    <Button variant="ghost" size="xs" onClick={clearFilters}>
                      Clear all
                    </Button>
                  ) : null}
                </PopoverHeader>
                {draftFilters.length ? (
                  <div className="flex flex-col gap-2">
                    {draftFilters.map((filter, index) => {
                      const field = filterFields.find((item) => item.key === filter.field)
                      const operators = field ? filterOperators(field.type) : []

                      return (
                        <div key={`${filter.field ?? "new"}-${index}`} className="grid grid-cols-[minmax(0,1fr)_minmax(0,1fr)_minmax(0,1fr)_auto] items-center gap-1.5">
                          <Select
                            value={filter.field}
                            onValueChange={(value) => {
                              if (!value) return
                              const nextField = filterFields.find((item) => item.key === value)
                              updateDraftFilter(index, {
                                field: value,
                                operator: nextField ? filterOperators(nextField.type)[0]?.value : undefined,
                                value: undefined,
                              })
                            }}
                          >
                            <SelectTrigger size="sm" aria-label={`Filter ${index + 1} field`} className="w-full">
                              <SelectValue placeholder="Field">
                                {(value) => filterFields.find((item) => item.key === value)?.label ?? "Field"}
                              </SelectValue>
                            </SelectTrigger>
                            <SelectContent align="start" alignItemWithTrigger={false}>
                              {filterFields.map((item) => (
                                <SelectItem key={item.key} value={item.key}>{item.label}</SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                          <Select
                            value={filter.operator}
                            disabled={!field}
                            onValueChange={(value) => updateDraftFilter(index, {
                              operator: value as ContentEntryFilterOperator,
                            })}
                          >
                            <SelectTrigger size="sm" aria-label={`Filter ${index + 1} operator`} className="w-full">
                              <SelectValue placeholder="Operator">
                                {(value) => operators.find((item) => item.value === value)?.label ?? "Operator"}
                              </SelectValue>
                            </SelectTrigger>
                            <SelectContent align="start" alignItemWithTrigger={false}>
                              {operators.map((operator) => (
                                <SelectItem key={operator.value} value={operator.value}>{operator.label}</SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                          {field?.type === "timestamp" ? (
                            <Input
                              type="datetime-local"
                              step={60}
                              aria-label={`Filter ${index + 1} value (UTC)`}
                              disabled={!field}
                              value={formatTimestampInputValue(filter.value)}
                              onChange={(event) => updateDraftFilter(index, {
                                value: parseTimestampInputValue(event.target.value),
                              })}
                            />
                          ) : field?.type === "status" || field?.type === "boolean" ? (
                            <Select
                              value={filter.value}
                              disabled={!field}
                              onValueChange={(value) => {
                                if (value) updateDraftFilter(index, { value })
                              }}
                            >
                              <SelectTrigger size="sm" aria-label={`Filter ${index + 1} value`} className="w-full">
                                <SelectValue placeholder="Value" />
                              </SelectTrigger>
                              <SelectContent align="start" alignItemWithTrigger={false}>
                                {filterValues(field.type).map((value) => (
                                  <SelectItem key={value.value} value={value.value}>{value.label}</SelectItem>
                                ))}
                              </SelectContent>
                            </Select>
                          ) : (
                            <Input
                              aria-label={`Filter ${index + 1} value`}
                              disabled={!field}
                              placeholder="Value"
                              value={filter.value ?? ""}
                              onChange={(event) => updateDraftFilter(index, { value: event.target.value })}
                            />
                          )}
                          <Button
                            aria-label={`Remove filter ${index + 1}`}
                            variant="ghost"
                            size="icon-sm"
                            onClick={() => removeFilter(index)}
                          >
                            <X />
                          </Button>
                        </div>
                      )
                    })}
                  </div>
                ) : (
                  <p className="text-sm text-muted-foreground">Add rules to narrow the entries shown.</p>
                )}
                <div className="flex justify-between gap-2">
                  <Button variant="outline" size="sm" onClick={addFilter}>
                    <Plus />
                    Add filter
                  </Button>
                  {draftFilters.length ? (
                    <Button size="sm" onClick={() => openFilters(false)}>
                      Done
                    </Button>
                  ) : null}
                </div>
              </PopoverContent>
            </Popover>
          </div>
          {entriesError ? (
            <Alert variant="destructive">
              <AlertTitle>Unable to load entries</AlertTitle>
              <AlertDescription>{entriesError}</AlertDescription>
            </Alert>
          ) : entries.length ? (
            <div className="overflow-x-auto rounded-xl border">
              <Table className="table-fixed" style={{ width: `${entryTableWidth}px` }}>
                <colgroup>
                  <col className="w-8" />
                  <col className="w-52" />
                  {contentType.fields.map((field) => (
                    <col key={field.key} className="w-44" />
                  ))}
                  <col className="w-32" />
                  <col className="w-44" />
                  <col className="w-44" />
                  {canWrite ? <col className="w-8" /> : null}
                </colgroup>
                <TableHeader>
                  <TableRow className="h-11 hover:bg-transparent">
                    <TableHead className="h-11 w-8 min-w-8 max-w-8 px-1 text-right" aria-label="Index">
                      #
                    </TableHead>
                    <SortableTableHead
                      className="w-52 min-w-52 max-w-52"
                      direction={sortDirection("$id")}
                      field="$id"
                      label="$id"
                      onSort={toggleSort}
                    />
                    {contentType.fields.map((field) => (
                      <SortableTableHead
                        key={field.key}
                        className="w-44 min-w-44 max-w-44"
                        direction={sortDirection(field.key)}
                        field={field.key}
                        label={field.key}
                        onSort={toggleSort}
                      />
                    ))}
                    <SortableTableHead
                      className="w-32 min-w-32 max-w-32"
                      direction={sortDirection("$status")}
                      field="$status"
                      label="$status"
                      onSort={toggleSort}
                    />
                    <SortableTableHead
                      className="w-44 min-w-44 max-w-44"
                      direction={sortDirection("$createdAt")}
                      field="$createdAt"
                      label="$createdAt"
                      onSort={toggleSort}
                    />
                    <SortableTableHead
                      className="w-44 min-w-44 max-w-44"
                      direction={sortDirection("$updatedAt")}
                      field="$updatedAt"
                      label="$updatedAt"
                      onSort={toggleSort}
                    />
                    {canWrite ? (
                      <TableHead
                        aria-hidden="true"
                        className="invisible sticky right-0 z-10 h-11 w-8 min-w-8 max-w-8 p-0"
                      />
                    ) : null}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {entries.map((entry, index) => (
                    <TableRow
                      key={entry.id}
                      className={canWrite ? "cursor-pointer focus-visible:outline focus-visible:outline-ring" : undefined}
                      tabIndex={canWrite ? 0 : undefined}
                      onClick={canWrite ? () => setPanel({ kind: "entry", entry }) : undefined}
                      onKeyDown={
                        canWrite
                          ? (event) =>
                              openFromKeyboard(event, () =>
                                setPanel({ kind: "entry", entry }),
                              )
                        : undefined
                      }
                    >
                      <TableCell className="w-8 min-w-8 max-w-8 overflow-hidden text-ellipsis px-1 text-right tabular-nums text-muted-foreground">
                        {index + 1}
                      </TableCell>
                      <TableCell className="w-52 min-w-52 max-w-52 overflow-hidden text-ellipsis font-mono text-muted-foreground">
                        <CopyableId id={entry.id} />
                      </TableCell>
                      {contentType.fields.map((field) => (
                        <TableCell
                          key={field.key}
                          className="w-44 min-w-44 max-w-44 overflow-hidden text-ellipsis"
                          title={formatEntryValue(entry.data[field.key])}
                        >
                          {formatEntryValue(entry.data[field.key])}
                        </TableCell>
                      ))}
                      <TableCell className="w-32 min-w-32 max-w-32 overflow-hidden text-ellipsis">
                        {entry.status}
                      </TableCell>
                      <TableCell className="w-44 min-w-44 max-w-44 overflow-hidden text-ellipsis">
                        {formatEntryTimestamp(entry.createdAt)}
                      </TableCell>
                      <TableCell className="w-44 min-w-44 max-w-44 overflow-hidden text-ellipsis">
                        {formatEntryTimestamp(entry.updatedAt)}
                      </TableCell>
                      {canWrite ? (
                        <TableCell
                          className="sticky right-0 z-10 w-8 min-w-8 max-w-8 bg-background p-0"
                          onClick={(event) => event.stopPropagation()}
                        >
                          <div className="flex justify-center">
                            <EntryActions
                              workspaceId={workspaceId}
                              projectId={projectId}
                              contentTypeKey={contentType.key}
                              entryId={entry.id}
                              onEdit={() => setPanel({ kind: "entry", entry })}
                            />
                          </div>
                        </TableCell>
                      ) : null}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          ) : filters.length ? (
            <div className="flex min-h-44 flex-col items-center justify-center gap-3 rounded-xl border text-center">
              <p className="text-sm font-semibold">No matching entries</p>
              <p className="text-sm text-muted-foreground">Try adjusting or clearing the active filters.</p>
              <Button size="sm" variant="outline" onClick={clearFilters}>
                Clear filters
              </Button>
            </div>
          ) : (
            <div className="flex min-h-44 flex-col items-center justify-center gap-3 rounded-xl border text-center">
              <p className="text-sm font-semibold">No entries yet</p>
              <p className="text-sm text-muted-foreground">
                {canWrite
                  ? "Create the first entry using this schema."
                  : "Entries added by editors will appear here."}
              </p>
              {canWrite ? (
                <Button size="sm" onClick={() => setPanel({ kind: "entry" })}>
                  Create entry
                </Button>
              ) : null}
            </div>
          )}
        </TabsContent>
      </Tabs>

      <Sheet open={panel !== null} onOpenChange={(open) => !open && closePanel()}>
        <SheetContent
          side="right"
          className="data-[side=right]:w-full! sm:data-[side=right]:w-2/5! sm:data-[side=right]:max-w-none!"
        >
          <SheetHeader className="pr-12">
            <SheetTitle>{panelTitle}</SheetTitle>
            <SheetDescription>
              {panel?.kind === "schema"
                ? "Change the fields used by this content type."
                : "Edit this entry using the current schema."}
            </SheetDescription>
          </SheetHeader>
          <div className="min-h-0 flex-1 overflow-y-auto px-4 pb-4">
            {panel?.kind === "schema" ? (
              <ContentTypeForm
                workspaceId={workspaceId}
                projectId={projectId}
                mode="edit"
                initialContentType={contentType}
                onCancel={closePanel}
              />
            ) : panel?.kind === "entry" ? (
              <EntryEditor
                key={panel.entry?.id ?? "new-entry"}
                workspaceId={workspaceId}
                projectId={projectId}
                contentTypeKey={contentType.key}
                fields={contentType.fields}
                entry={panel.entry}
                action={
                  panel.entry
                    ? updateContentEntryAction.bind(
                        null,
                        workspaceId,
                        projectId,
                        contentType.key,
                        panel.entry.id,
                        contentType.fields,
                      )
                    : createContentEntryAction.bind(
                        null,
                        workspaceId,
                        projectId,
                        contentType.key,
                        contentType.fields,
                      )
                }
                onCancel={closePanel}
              />
            ) : null}
          </div>
        </SheetContent>
      </Sheet>
    </>
  )
}

function SortableTableHead({
  className,
  direction,
  field,
  label,
  onSort,
}: {
  className?: string
  direction?: "ascending" | "descending"
  field: string
  label: string
  onSort: (field: string) => void
}) {
  const icon =
    direction === "ascending" ? (
      <ArrowUp />
    ) : direction === "descending" ? (
      <ArrowDown />
    ) : (
      <ChevronsUpDown className="text-muted-foreground" />
    )

  return (
    <TableHead aria-sort={direction ?? "none"} className={`h-11 py-2 ${className ?? ""}`}>
      <Button
        aria-label={`Sort by ${label}${direction ? `, currently ${direction}` : ""}`}
        className="-ml-2 h-7 max-w-[calc(100%+1rem)] justify-start px-2"
        size="sm"
        variant="ghost"
        onClick={() => onSort(field)}
      >
        <span className="truncate">{label}</span>
        {icon}
      </Button>
    </TableHead>
  )
}

function filterOperators(type: FilterField["type"]) {
  switch (type) {
    case "text":
      return [
        { value: "eq" as const, label: "Is" },
        { value: "contains" as const, label: "Contains" },
      ]
    case "number":
      return [
        { value: "eq" as const, label: "Is" },
        { value: "gt" as const, label: "Greater than" },
        { value: "gte" as const, label: "At least" },
        { value: "lt" as const, label: "Less than" },
        { value: "lte" as const, label: "At most" },
      ]
    case "timestamp":
      return [
        { value: "eq" as const, label: "Is" },
        { value: "gt" as const, label: "After" },
        { value: "gte" as const, label: "On or after" },
        { value: "lt" as const, label: "Before" },
        { value: "lte" as const, label: "On or before" },
      ]
    default:
      return [{ value: "eq" as const, label: "Is" }]
  }
}

function filterValues(type: FilterField["type"]) {
  if (type === "status") {
    return [
      { value: "draft", label: "Draft" },
      { value: "published", label: "Published" },
    ]
  }

  return [
    { value: "true", label: "True" },
    { value: "false", label: "False" },
  ]
}

function formatValue(value: unknown) {
  if (value === null || value === undefined) return "—"
  if (typeof value === "boolean") return value ? "True" : "False"
  return String(value)
}

function formatEntryValue(value: unknown) {
  if (value === null || value === undefined) return "NULL"
  return formatValue(value)
}

function formatEntryTimestamp(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime())
    ? "Unknown date"
    : entryTimestampFormatter.format(date)
}

function formatTimestampInputValue(value?: string) {
  if (!value) return ""

  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ""

  const pad = (part: number) => String(part).padStart(2, "0")
  return `${String(date.getUTCFullYear()).padStart(4, "0")}-${pad(date.getUTCMonth() + 1)}-${pad(date.getUTCDate())}T${pad(date.getUTCHours())}:${pad(date.getUTCMinutes())}`
}

function parseTimestampInputValue(value: string) {
  if (!value) return ""

  const normalizedValue = value.length === 16 ? `${value}:00` : value
  const date = new Date(`${normalizedValue}Z`)
  return Number.isNaN(date.getTime()) ? "" : date.toISOString()
}
