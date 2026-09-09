"use client"

import { useActionState, useState } from "react"
import { Trash2 } from "lucide-react"

import {
  deleteContentTypeAction,
  type ContentActionState,
} from "../../../content-actions"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import { Button } from "@/components/ui/button"
import { useActionToast } from "@/hooks/use-action-toast"

const initialState: ContentActionState = {
  status: "idle",
  message: null,
  fieldErrors: {},
}

export function ContentTypeSettings({
  workspaceId,
  projectId,
  contentTypeKey,
  canWrite,
}: {
  workspaceId: string
  projectId: string
  contentTypeKey: string
  canWrite: boolean
}) {
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false)
  const action = deleteContentTypeAction.bind(
    null,
    workspaceId,
    projectId,
    contentTypeKey,
  )
  const [state, formAction, pending] = useActionState(action, initialState)
  const deleteFormId = `delete-content-type-${contentTypeKey}`

  useActionToast(state)

  return (
    <div className="max-w-2xl space-y-6">
      <section className="space-y-1" aria-labelledby="content-type-settings-heading">
        <h2 id="content-type-settings-heading" className="text-sm font-semibold">
          Content type settings
        </h2>
        <p className="text-sm text-muted-foreground">
          Manage the settings for {contentTypeKey}.
        </p>
      </section>

      {canWrite ? (
        <section
          aria-labelledby="content-type-danger-zone-heading"
          className="space-y-4 rounded-xl border border-destructive/50 p-4"
        >
          <div className="space-y-1">
            <h3 id="content-type-danger-zone-heading" className="text-sm font-semibold">
              Danger zone
            </h3>
            <p className="text-sm text-muted-foreground">
              Deleting this content type also permanently deletes all of its entries.
            </p>
          </div>
          <Button
            type="button"
            variant="destructive"
            disabled={pending}
            onClick={() => setDeleteDialogOpen(true)}
          >
            <Trash2 />
            Delete content type
          </Button>
        </section>
      ) : (
        <Alert>
          <AlertTitle>Read-only workspace access</AlertTitle>
          <AlertDescription>
            You can view this content type, but only an owner or editor can change
            its settings.
          </AlertDescription>
        </Alert>
      )}

      {canWrite ? (
        <AlertDialog open={deleteDialogOpen} onOpenChange={setDeleteDialogOpen}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Delete {contentTypeKey}?</AlertDialogTitle>
              <AlertDialogDescription>
                This permanently deletes the schema and all of its entries. This
                action cannot be undone.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <form id={deleteFormId} action={formAction} noValidate />
            <AlertDialogFooter>
              <AlertDialogCancel disabled={pending}>Cancel</AlertDialogCancel>
              <Button
                type="submit"
                form={deleteFormId}
                variant="destructive"
                disabled={pending}
              >
                {pending ? "Deleting..." : "Delete content type"}
              </Button>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      ) : null}
    </div>
  )
}
