"use client"

import { useActionState } from "react"

import {
  deleteContentTypeAction,
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

export function DeleteContentTypeButton({
  workspaceId,
  projectId,
  contentTypeKey,
  contentTypeKeyLabel,
}: {
  workspaceId: string
  projectId: string
  contentTypeKey: string
  contentTypeKeyLabel: string
}) {
  const action = deleteContentTypeAction.bind(
    null,
    workspaceId,
    projectId,
    contentTypeKey,
  )
  const [state, formAction, pending] = useActionState(action, initialState)
  useActionToast(state)

  return (
    <AlertDialog>
      <AlertDialogTrigger
        render={<Button variant="destructive" className="mt-4" />}
      >
        Delete content type
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete {contentTypeKeyLabel}?</AlertDialogTitle>
          <AlertDialogDescription>
            This permanently deletes the schema and all of its entries.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <form id="delete-content-type" action={formAction} />
        <AlertDialogFooter>
          <AlertDialogCancel disabled={pending}>Cancel</AlertDialogCancel>
          <AlertDialogAction
            render={
              <Button
                type="submit"
                form="delete-content-type"
                variant="destructive"
                disabled={pending}
              />
            }
          >
            {pending ? "Deleting..." : "Delete content type"}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
