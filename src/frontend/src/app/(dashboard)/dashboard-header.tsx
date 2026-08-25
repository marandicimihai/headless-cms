"use client"

import Link from "next/link"
import { useTransition } from "react"
import {
  Bell,
  Boxes,
  ChevronsUpDown,
  LogOut,
  Search,
  Settings,
  User,
} from "lucide-react"

import { logoutAction } from "@/app/auth/logout/actions"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { Input } from "@/components/ui/input"

export function DashboardHeader({
  email,
  role,
}: {
  email: string
  role: string
}) {
  const [isLoggingOut, startTransition] = useTransition()

  function handleLogout() {
    startTransition(async () => {
      await logoutAction()
    })
  }

  return (
    <header className="relative z-10 flex h-16 shrink-0 items-center gap-3 border-b px-4">
      <Link
        aria-label="Headless CMS dashboard"
        className="flex items-center gap-2.5"
        href="/"
      >
        <div className="flex size-8 items-center justify-center rounded-lg bg-primary text-primary-foreground">
          <Boxes className="size-4" />
        </div>
        <div className="grid text-left text-sm leading-tight">
          <span className="font-semibold">Headless CMS</span>
          <span className="text-xs text-muted-foreground">Control panel</span>
        </div>
      </Link>
      <div className="relative hidden w-full max-w-sm sm:ml-4 sm:block">
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
      <DropdownMenu>
        <DropdownMenuTrigger
          render={
            <Button
              aria-label="Open account menu"
              className="h-12 gap-2 px-2"
              variant="ghost"
            />
          }
        >
          <Avatar className="size-8 rounded-lg">
            <AvatarFallback className="rounded-lg">MC</AvatarFallback>
          </Avatar>
          <div className="hidden text-left leading-tight md:grid">
            <span className="max-w-48 truncate text-sm font-medium">{email}</span>
            {role === "PlatformAdmin" && (
              <span className="text-xs text-muted-foreground">Administrator</span>
            )}
          </div>
          <ChevronsUpDown className="hidden size-4 md:block" />
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-56">
          <DropdownMenuGroup>
            <DropdownMenuLabel>My account</DropdownMenuLabel>
            <DropdownMenuItem>
              <User />
              Profile
            </DropdownMenuItem>
            <DropdownMenuItem>
              <Settings />
              Settings
            </DropdownMenuItem>
          </DropdownMenuGroup>
          <DropdownMenuSeparator />
          <DropdownMenuItem
            disabled={isLoggingOut}
            variant="destructive"
            onClick={handleLogout}
          >
            <LogOut />
            {isLoggingOut ? "Logging out..." : "Log out"}
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </header>
  )
}
