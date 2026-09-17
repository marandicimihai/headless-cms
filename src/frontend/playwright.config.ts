import { defineConfig } from "@playwright/test"

export default defineConfig({
  testDir: "./tests/render-dedup",
  workers: 1,
  timeout: 30_000,
  webServer: {
    command: "pnpm start --port 3210 --hostname 127.0.0.1",
    url: "http://127.0.0.1:3210/auth/login",
    reuseExistingServer: false,
    env: { BACKEND_URL: "http://127.0.0.1:3211" },
  },
  use: { baseURL: "http://127.0.0.1:3210" },
})
