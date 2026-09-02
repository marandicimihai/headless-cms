"use client"

import { useActionState } from "react"

import {
  deleteContentEntryAction,
  type ContentActionState,
} from "../../content-actions"
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog"
import { Button } from "@/components/ui/button"
import { useActionToast } from "@/hooks/use-action-toast"

const initialState: ContentActionState = {
  status: "idle",
  message: null,
  fieldErrors: {},
}

export function DeleteEntryButton({
  workspaceId,
  projectId,
  contentTypeKey,
  entryId,
}: {
  workspaceId: string
  projectId: string
  contentTypeKey: string
  entryId: string
}) {
  const action = deleteContentEntryAction.bind(
    null,
    workspaceId,
    projectId,
    contentTypeKey,
    entryId,
  )
  const [state, formAction, pending] = useActionState(action, initialState)
  useActionToast(state)

  return (
    <AlertDialog>
      <AlertDialogTrigger render={<Button variant="ghost" size="sm" />}>
        Delete
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete this entry?</AlertDialogTitle>
          <AlertDialogDescription>
            This permanently removes the entry. This cannot be undone.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <form id={`delete-entry-${entryId}`} action={formAction} />
        <AlertDialogFooter>
          <AlertDialogCancel disabled={pending}>Cancel</AlertDialogCancel>
          <AlertDialogAction
            render={
              <Button
                type="submit"
                form={`delete-entry-${entryId}`}
                variant="destructive"
                disabled={pending}
              />
            }
          >
            {pending ? "Deleting..." : "Delete entry"}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
