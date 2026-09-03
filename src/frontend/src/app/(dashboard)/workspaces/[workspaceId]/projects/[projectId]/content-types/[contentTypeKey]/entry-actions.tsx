"use client"

import { useActionState, useState } from "react"
import { MoreHorizontal } from "lucide-react"

import {
  deleteContentEntryAction,
  type ContentActionState,
} from "../../content-actions"
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
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { useActionToast } from "@/hooks/use-action-toast"

const initialState: ContentActionState = {
  status: "idle",
  message: null,
  fieldErrors: {},
}

export function EntryActions({
  workspaceId,
  projectId,
  contentTypeKey,
  entryId,
  onEdit,
}: {
  workspaceId: string
  projectId: string
  contentTypeKey: string
  entryId: string
  onEdit: () => void
}) {
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false)
  const action = deleteContentEntryAction.bind(
    null,
    workspaceId,
    projectId,
    contentTypeKey,
    entryId,
  )
  const [state, formAction, pending] = useActionState(action, initialState)
  const deleteFormId = `delete-entry-${entryId}`

  useActionToast(state)

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger
          render={
            <Button
              aria-label="Open entry actions"
              size="icon-sm"
              variant="ghost"
            />
          }
        >
          <MoreHorizontal />
        </DropdownMenuTrigger>
        <DropdownMenuContent
          align="end"
          className="w-auto"
          onClick={(event) => event.stopPropagation()}
        >
          <DropdownMenuItem onClick={onEdit}>Edit</DropdownMenuItem>
          <DropdownMenuItem
            variant="destructive"
            onClick={() => setDeleteDialogOpen(true)}
          >
            Delete entry
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      <AlertDialog open={deleteDialogOpen} onOpenChange={setDeleteDialogOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete this entry?</AlertDialogTitle>
            <AlertDialogDescription>
              This permanently removes the entry. This cannot be undone.
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
              {pending ? "Deleting..." : "Delete entry"}
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  )
}
