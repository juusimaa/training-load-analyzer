// SyncStatusView per row of the message table, built from the display probe golden the way the
// backend projects SyncCoordinator.Status (contracts/http-api.md §3).
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import type { SyncStatusView } from "../src/types";

interface ProbeRow {
  row: string;
  status: {
    isRunning: boolean;
    failure: string | null;
    outcome: string | null;
    finishedAt: string | null;
  };
  message: string;
}

const probe = join(dirname(fileURLToPath(import.meta.url)), "..", "..", "..", "parity", "golden", "probes", "display.json");

/** "2026-09-18T07:07:00+03:00" → "2026-09-18 07:07": the local wall clock the offset carries. */
function wallClock(finishedAt: string): string {
  return `${finishedAt.slice(0, 10)} ${finishedAt.slice(11, 16)}`;
}

export function syncStatusRows(): Record<string, SyncStatusView> {
  const rows = (JSON.parse(readFileSync(probe, "utf-8")) as { syncMessage: ProbeRow[] }).syncMessage;
  return Object.fromEntries(
    rows.map(({ row, status, message }) => [
      row,
      {
        isRunning: status.isRunning,
        message,
        needsConnection: status.failure !== null || status.outcome === "ReconnectionRequired",
        lastChecked: status.finishedAt !== null && !status.isRunning ? wallClock(status.finishedAt) : null,
      },
    ]),
  );
}

export const never: SyncStatusView = { isRunning: false, message: "", needsConnection: false, lastChecked: null };
