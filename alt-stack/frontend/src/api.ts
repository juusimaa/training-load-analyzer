// Typed wrappers over fetch for the three routes the page reads (contracts/http-api.md, data-model.md §6).
import type { DashboardApi, DashboardView, SyncStatusView } from "./types";

/** The request failed or the backend answered with an error: the page shows the FR-016 notice. */
export class Unavailable extends Error {
  constructor(message: string, options?: { cause?: unknown }) {
    super(message, options);
    this.name = "Unavailable";
  }
}

export function createApi(fetchImpl: typeof fetch = fetch): DashboardApi {
  async function request<T>(url: string, method: "GET" | "POST"): Promise<T> {
    let response: Response;
    try {
      response = await fetchImpl(url, { method, headers: { Accept: "application/json" } });
    } catch (cause) {
      throw new Unavailable(`${method} ${url} failed`, { cause });
    }
    if (!response.ok) throw new Unavailable(`${method} ${url} answered ${response.status}`);
    try {
      return (await response.json()) as T;
    } catch (cause) {
      throw new Unavailable(`${method} ${url} answered with a body that is not JSON`, { cause });
    }
  }

  return {
    fetchDashboard: () => request<DashboardView>("/api/dashboard", "GET"),
    fetchSyncStatus: () => request<SyncStatusView>("/api/sync/status", "GET"),
    postSync: () => request<SyncStatusView>("/api/sync", "POST"),
  };
}
