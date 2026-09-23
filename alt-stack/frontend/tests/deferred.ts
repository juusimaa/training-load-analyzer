// A promise the test settles by hand, and a DashboardApi built from them as a plain object that
// counts its calls: no vi.fn, no vi.mock (constitution Principle IV as amended, research R12).
import type { DashboardApi, DashboardView, SyncStatusView } from "../src/types";

export interface Deferred<T> {
  promise: Promise<T>;
  resolve(value: T): void;
  reject(reason: unknown): void;
}

export function deferred<T>(): Deferred<T> {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

export interface ControlledApi {
  api: DashboardApi;
  /** How many times each method has been called. */
  calls: { fetchDashboard: number; fetchSyncStatus: number; postSync: number };
  /** One deferred per call, in call order, for the test to settle. */
  dashboard: Deferred<DashboardView>[];
  status: Deferred<SyncStatusView>[];
  sync: Deferred<SyncStatusView>[];
  total(): number;
}

export function controlledApi(): ControlledApi {
  const calls = { fetchDashboard: 0, fetchSyncStatus: 0, postSync: 0 };
  const dashboard: Deferred<DashboardView>[] = [];
  const status: Deferred<SyncStatusView>[] = [];
  const sync: Deferred<SyncStatusView>[] = [];

  function next<T>(queue: Deferred<T>[]): Promise<T> {
    const d = deferred<T>();
    queue.push(d);
    return d.promise;
  }

  const api: DashboardApi = {
    fetchDashboard() {
      calls.fetchDashboard += 1;
      return next(dashboard);
    },
    fetchSyncStatus() {
      calls.fetchSyncStatus += 1;
      return next(status);
    },
    postSync() {
      calls.postSync += 1;
      return next(sync);
    },
  };

  return {
    api,
    calls,
    dashboard,
    status,
    sync,
    total: () => calls.fetchDashboard + calls.fetchSyncStatus + calls.postSync,
  };
}
