import { Plus } from "lucide-react"

import { Button } from "@/components/ui/button"
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"

const stats = [
  { title: "Workspaces", value: "4", description: "+1 this month" },
  { title: "Projects", value: "12", description: "Across all workspaces" },
  { title: "Content types", value: "28", description: "+3 this week" },
  { title: "Entries", value: "1,284", description: "86 drafts" },
]

export function DashboardOverview() {
  return (
    <div className="flex flex-1 flex-col gap-6 p-4 md:p-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Dashboard</h1>
          <p className="text-sm text-muted-foreground">
            An overview of your CMS activity.
          </p>
        </div>
        <Button>
          <Plus />
          New project
        </Button>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {stats.map((stat) => (
          <Card key={stat.title}>
            <CardHeader>
              <CardDescription>{stat.title}</CardDescription>
              <CardTitle className="text-2xl">{stat.value}</CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-xs text-muted-foreground">{stat.description}</p>
            </CardContent>
          </Card>
        ))}
      </div>

      <div className="grid flex-1 gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Recent content</CardTitle>
            <CardDescription>Your latest updated entries.</CardDescription>
            <CardAction>
              <Button variant="outline">View all</Button>
            </CardAction>
          </CardHeader>
          <CardContent>
            <div className="flex min-h-64 items-center justify-center rounded-lg border border-dashed text-sm text-muted-foreground">
              Content entries will appear here.
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Workspace activity</CardTitle>
            <CardDescription>Changes from your team.</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="flex min-h-64 items-center justify-center rounded-lg border border-dashed text-sm text-muted-foreground">
              Activity will appear here.
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
