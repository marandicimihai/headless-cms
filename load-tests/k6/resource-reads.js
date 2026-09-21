import http from "k6/http";
import { check, fail } from "k6";

const baseUrl = (__ENV.BASE_URL || "http://api:8080").replace(/\/$/, "");
const targetPath = __ENV.LOAD_TEST_PATH || "";
const email = __ENV.LOAD_TEST_EMAIL || "";
const password = __ENV.LOAD_TEST_PASSWORD || "";
const warmupRequests = Number.parseInt(__ENV.LOAD_TEST_WARMUP_REQUESTS || "1", 10);

export const options = {
  scenarios: {
    resource_reads: {
      executor: "constant-vus",
      vus: Number.parseInt(__ENV.LOAD_TEST_VUS || "10", 10),
      duration: __ENV.LOAD_TEST_DURATION || "30s",
    },
  },
  thresholds: {
    http_req_failed: ["rate<0.01"],
    "http_req_duration{request:resource-read}": ["p(95)<1000"],
  },
};

function requireConfiguration() {
  if (!targetPath.startsWith("/api/")) {
    fail("LOAD_TEST_PATH must be an authenticated API path beginning with /api/");
  }
  if (!email || !password) {
    fail("LOAD_TEST_EMAIL and LOAD_TEST_PASSWORD must be set");
  }
  if (!Number.isInteger(warmupRequests) || warmupRequests < 0) {
    fail("LOAD_TEST_WARMUP_REQUESTS must be a non-negative integer");
  }
}

export function setup() {
  requireConfiguration();

  const login = http.post(
    `${baseUrl}/api/auth/login`,
    JSON.stringify({ email, password }),
    {
      headers: { "Content-Type": "application/json" },
      tags: { request: "login" },
    },
  );

  const loggedIn = check(login, {
    "login succeeded": (response) => response.status === 200,
    "session cookie received": (response) =>
      Boolean(response.cookies.cms_session?.[0]?.value),
  });
  if (!loggedIn) {
    fail(`Login failed with HTTP ${login.status}`);
  }

  const session = login.cookies.cms_session[0].value;
  const requestOptions = {
    headers: { Cookie: `cms_session=${session}` },
    tags: { request: "warmup" },
  };

  for (let index = 0; index < warmupRequests; index += 1) {
    const warmup = http.get(`${baseUrl}${targetPath}`, requestOptions);
    if (!check(warmup, { "warmup succeeded": (response) => response.status === 200 })) {
      fail(`Warmup failed with HTTP ${warmup.status}; verify LOAD_TEST_PATH and access`);
    }
  }

  return { session };
}

export default function ({ session }) {
  const response = http.get(`${baseUrl}${targetPath}`, {
    headers: { Cookie: `cms_session=${session}` },
    tags: { request: "resource-read" },
  });

  check(response, {
    "resource read succeeded": (result) => result.status === 200,
  });
}
