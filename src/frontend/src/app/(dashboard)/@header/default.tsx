import { DashboardHeader } from "@/app/(dashboard)/dashboard-header"
import { getSession } from "@/lib/api/session"

export default async function DefaultHeader() {
  const session = await getSession()

  if (!session) return null

  return <DashboardHeader email={session.email} role={session.platformRole} />
}
