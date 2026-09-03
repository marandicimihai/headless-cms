"use client"

import Link from "next/link"
import { useActionState, useState } from "react"
import { MoreHorizontal } from "lucide-react"

import {
  deleteContentTypeAction,
  type ContentActionState,
} from "../content-actions"
import {
  AlertDialog,
  AlertDialogAction,
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

export function ContentTypeActions({
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
  const contentTypeHref = `/workspaces/${workspaceId}/projects/${projectId}/content-types/${contentTypeKey}`
  const deleteFormId = `delete-content-type-${contentTypeKey}`

  useActionToast(state)

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger
          render={
            <Button
              aria-label={`Open actions for ${contentTypeKey}`}
              size="icon-sm"
              variant="ghost"
            />
          }
        >
          <MoreHorizontal />
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-auto">
          <DropdownMenuItem render={<Link href={contentTypeHref} />}>
            Open
          </DropdownMenuItem>
          {canWrite ? (
            <>
              <DropdownMenuItem
                render={<Link href={`${contentTypeHref}/edit`} />}
              >
                Edit
              </DropdownMenuItem>
              <DropdownMenuItem
                variant="destructive"
                onClick={() => setDeleteDialogOpen(true)}
              >
                Delete content type
              </DropdownMenuItem>
            </>
          ) : null}
        </DropdownMenuContent>
      </DropdownMenu>

      {canWrite ? (
        <AlertDialog
          open={deleteDialogOpen}
          onOpenChange={setDeleteDialogOpen}
        >
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Delete {contentTypeKey}?</AlertDialogTitle>
              <AlertDialogDescription>
                This permanently deletes the schema and all of its entries.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <form id={deleteFormId} action={formAction} />
            <AlertDialogFooter>
              <AlertDialogCancel disabled={pending}>Cancel</AlertDialogCancel>
              <AlertDialogAction
                render={
                  <Button
                    type="submit"
                    form={deleteFormId}
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
      ) : null}
    </>
  )
}
