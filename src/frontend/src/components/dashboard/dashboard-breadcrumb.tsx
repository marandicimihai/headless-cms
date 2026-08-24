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
        <BreadcrumbSeparator />
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
    <footer className="relative z-10 shrink-0 border-t bg-background px-4 py-3 shadow-[0_-2px_4px_-2px_var(--border)] md:px-6">
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink render={<Link href="/" />}>Home</BreadcrumbLink>
          </BreadcrumbItem>
          {children}
        </BreadcrumbList>
      </Breadcrumb>
    </footer>
  )
}
