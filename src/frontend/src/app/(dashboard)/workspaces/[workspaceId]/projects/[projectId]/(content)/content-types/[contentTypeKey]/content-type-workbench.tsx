"use client"

import { type KeyboardEvent, useState } from "react"
import { Plus } from "lucide-react"

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
import type { ContentEntry, ContentType } from "@/lib/types/content"

type EditorPanel =
  | { kind: "schema" }
  | { kind: "entry"; entry?: ContentEntry }
  | null

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
  selectedEntry,
}: {
  workspaceId: string
  projectId: string
  contentType: ContentType
  entries: ContentEntry[]
  entriesError: string | null
  canWrite: boolean
  selectedEntry?: ContentEntry
}) {
  const [panel, setPanel] = useState<EditorPanel>(
    canWrite && selectedEntry ? { kind: "entry", entry: selectedEntry } : null,
  )

  function openFromKeyboard(
    event: KeyboardEvent<HTMLTableRowElement>,
    open: () => void,
  ) {
    if (event.key !== "Enter" && event.key !== " ") return

    event.preventDefault()
    open()
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
            <Table className="min-w-[48rem]">
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
                    className={canWrite ? "h-10 cursor-pointer focus-visible:outline focus-visible:outline-2 focus-visible:outline-ring" : "h-10"}
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
                  <TableRow className="hover:bg-transparent">
                    <TableHead className="w-8 min-w-8 max-w-8 px-1 text-right" aria-label="Index">
                      #
                    </TableHead>
                    <TableHead className="w-52 min-w-52 max-w-52">$id</TableHead>
                    {contentType.fields.map((field) => (
                      <TableHead key={field.key} className="w-44 min-w-44 max-w-44">
                        {field.key}
                      </TableHead>
                    ))}
                    <TableHead className="w-32 min-w-32 max-w-32">$status</TableHead>
                    <TableHead className="w-44 min-w-44 max-w-44">$createdAt</TableHead>
                    <TableHead className="w-44 min-w-44 max-w-44">$updatedAt</TableHead>
                    {canWrite ? (
                      <TableHead
                        aria-hidden="true"
                        className="invisible sticky right-0 z-10 w-8 min-w-8 max-w-8 p-0"
                      />
                    ) : null}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {entries.map((entry, index) => (
                    <TableRow
                      key={entry.id}
                      className={canWrite ? "cursor-pointer focus-visible:outline focus-visible:outline-2 focus-visible:outline-ring" : undefined}
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
          className="data-[side=right]:!w-full sm:data-[side=right]:!w-2/5 sm:data-[side=right]:!max-w-none"
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
