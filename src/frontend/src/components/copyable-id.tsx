"use client"

import type { MouseEvent } from "react"
import { Copy } from "lucide-react"
import { toast } from "sonner"

import { Badge } from "@/components/ui/badge"
import { cn } from "@/lib/utils"

export function CopyableId({
  id,
  className,
}: {
  id: string
  className?: string
}) {
  async function copyId(event: MouseEvent<HTMLButtonElement>) {
    event.stopPropagation()

    if (!navigator.clipboard?.writeText) {
      toast.error("Clipboard access is unavailable")
      return
    }

    try {
      await navigator.clipboard.writeText(id)
      toast.success("ID copied to clipboard")
    } catch {
      toast.error("Unable to copy ID")
    }
  }

  return (
    <Badge
      variant="outline"
      className={cn("max-w-full cursor-pointer font-mono", className)}
      render={
        <button
          type="button"
          aria-label={`Copy ID ${id}`}
          title="Copy ID"
          onClick={copyId}
        />
      }
    >
      <Copy aria-hidden="true" />
      <span className="truncate">{id}</span>
    </Badge>
  )
}
