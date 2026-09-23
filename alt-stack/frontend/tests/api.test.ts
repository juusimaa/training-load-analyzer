// T083: the fetch wrappers (data-model.md §6). The fetch function is a plain async function passed
// in, never a mock: it records what it was asked and answers what the test chose.
import { describe, expect, it } from "vitest";
import { createApi, Unavailable } from "../src/api";
import type { SyncStatusView } from "../src/types";
import { loadHistory } from "./golden";

interface Call {
  url: string;
  method: string;
}

function answering(status: number, body: unknown) {
  const calls: Call[] = [];
  const fetchImpl = async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    calls.push({ url: String(input), method: init?.method ?? "GET" });
    return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
  };
  return { calls, fetchImpl: fetchImpl as typeof fetch };
}

const status: SyncStatusView = {
  isRunning: false,
  message: "3 activities imported.",
  needsConnection: false,
  lastChecked: "2026-09-18 07:15",
};

describe("createApi", () => {
  it("GETs /api/dashboard and returns the parsed body", async () => {
    const view = loadHistory("H3f").view;
    const { calls, fetchImpl } = answering(200, view);

    expect(await createApi(fetchImpl).fetchDashboard()).toEqual(view);
    expect(calls).toEqual([{ url: "/api/dashboard", method: "GET" }]);
  });

  it("GETs /api/sync/status and returns the parsed body", async () => {
    const { calls, fetchImpl } = answering(200, status);

    expect(await createApi(fetchImpl).fetchSyncStatus()).toEqual(status);
    expect(calls).toEqual([{ url: "/api/sync/status", method: "GET" }]);
  });

  it("POSTs /api/sync and returns the parsed body", async () => {
    const { calls, fetchImpl } = answering(200, status);

    expect(await createApi(fetchImpl).postSync()).toEqual(status);
    expect(calls).toEqual([{ url: "/api/sync", method: "POST" }]);
  });

  it.each([500, 502, 404])("throws Unavailable on a %i response", async (code) => {
    const { fetchImpl } = answering(code, { detail: "no" });
    const api = createApi(fetchImpl);

    await expect(api.fetchDashboard()).rejects.toBeInstanceOf(Unavailable);
    await expect(api.fetchSyncStatus()).rejects.toBeInstanceOf(Unavailable);
    await expect(api.postSync()).rejects.toBeInstanceOf(Unavailable);
  });

  it("throws Unavailable when the fetch itself rejects", async () => {
    const fetchImpl = (async () => {
      throw new TypeError("Failed to fetch");
    }) as typeof fetch;
    const api = createApi(fetchImpl);

    await expect(api.fetchDashboard()).rejects.toBeInstanceOf(Unavailable);
    await expect(api.fetchSyncStatus()).rejects.toBeInstanceOf(Unavailable);
    await expect(api.postSync()).rejects.toBeInstanceOf(Unavailable);
  });
});
