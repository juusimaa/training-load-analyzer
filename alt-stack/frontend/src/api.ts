import type { DashboardApi } from "./types";

export class Unavailable extends Error {}

export function createApi(_fetchImpl: typeof fetch = fetch): DashboardApi {
  const pending = () => Promise.reject(new Error("not implemented"));
  return { fetchDashboard: pending, fetchSyncStatus: pending, postSync: pending };
}
