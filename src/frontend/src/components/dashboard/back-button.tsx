import type { ReactNode } from "react"
import Link from "next/link"
import { ArrowLeft } from "lucide-react"

import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"

export function BackButton({
  href,
  children = "Back",
  className,
}: {
  href: string
  children?: ReactNode
  className?: string
}) {
  return (
    <Button
      nativeButton={false}
      render={<Link href={href} />}
      variant="ghost"
      size="sm"
      className={cn("-ml-2.5 w-fit text-muted-foreground", className)}
    >
      <ArrowLeft />
      {children}
    </Button>
  )
}
