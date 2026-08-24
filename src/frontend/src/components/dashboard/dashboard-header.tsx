import { Bell, Search } from "lucide-react"

import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { SidebarTrigger } from "@/components/ui/sidebar"

export function DashboardHeader() {
  return (
    <header className="relative z-10 flex h-16 shrink-0 items-center gap-2 border-b px-4 shadow-[0_2px_4px_-2px_var(--border)]">
      <SidebarTrigger />
      <div className="relative hidden w-full max-w-sm sm:block">
        <Search className="absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
        <Input className="pl-8" placeholder="Search content..." type="search" />
      </div>
      <Button
        aria-label="Notifications"
        className="ml-auto"
        size="icon"
        variant="ghost"
      >
        <Bell />
      </Button>
    </header>
  )
}
