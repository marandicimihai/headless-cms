"use client"

import { Fragment, type ReactNode } from "react"
import Link from "next/link"

import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb"
import { SidebarTrigger } from "@/components/ui/sidebar"

export type DashboardBreadcrumbItem = {
  label: string
  href?: string
}

export function DashboardBreadcrumbTrail({
  items,
}: {
  items: DashboardBreadcrumbItem[]
}) {
  return items.map((item, index) => {
    const isCurrentPage = index === items.length - 1

    return (
      <Fragment key={`${item.href ?? "current"}-${item.label}`}>
        {index > 0 ? <BreadcrumbSeparator /> : null}
        <BreadcrumbItem>
          {isCurrentPage || !item.href ? (
            <BreadcrumbPage>{item.label}</BreadcrumbPage>
          ) : (
            <BreadcrumbLink render={<Link href={item.href} />}>
              {item.label}
            </BreadcrumbLink>
          )}
        </BreadcrumbItem>
      </Fragment>
    )
  })
}

export function DashboardBreadcrumb({ children }: { children: ReactNode }) {
  return (
    <div className="relative z-10 flex h-9 shrink-0 items-center gap-1 border-b bg-background px-2 md:px-6">
      <SidebarTrigger className="md:hidden" />
      <Breadcrumb className="min-w-0 overflow-x-auto">
        <BreadcrumbList className="flex-nowrap text-xs">
          {children}
        </BreadcrumbList>
      </Breadcrumb>
    </div>
  )
}
