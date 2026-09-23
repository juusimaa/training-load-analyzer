// Readers for the parity goldens (parity.md §3) and the geometry tolerance (research R8).
import { readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { expect } from "vitest";
import type { DashboardView } from "../src/types";

// A path rather than `new URL("…", import.meta.url)`: Vite rewrites that pattern as an asset
// reference, which under the jsdom environment resolves against http://localhost:3000.
const golden = join(dirname(fileURLToPath(import.meta.url)), "..", "..", "..", "parity", "golden");

export interface GoldenSlot {
  day: string;
  left: string;
  width: string;
  barTop: string | null;
  barHeight: string | null;
  load: string | null;
  opensLeft: boolean;
  points: [string, string, string][];
}

export interface GoldenGeometry {
  viewBox: string;
  plot: Record<string, string>;
  plotClasses: string[];
  bars: [string, string, string, string][];
  zeroRule: string | null;
  ticks: string[];
  slots: GoldenSlot[];
}

export interface HistoryGolden {
  name: string;
  view: DashboardView;
  geometry: Record<"180" | "90" | "30", GoldenGeometry>;
  renderedText: Record<"metricRow" | "recent" | "chart180" | "chart90" | "chart30" | "rail" | "content", string>;
}

export interface SurfacesGolden {
  loading: { rail: string; content: string };
  syncPanel: Record<string, string>;
  notFound: string;
  error: string;
}

export function historyNames(): string[] {
  return readdirSync(join(golden, "histories"))
    .filter((f) => f.endsWith(".json"))
    .map((f) => f.slice(0, -".json".length))
    .sort();
}

export function loadHistory(name: string): HistoryGolden {
  return JSON.parse(readFileSync(join(golden, "histories", `${name}.json`), "utf-8")) as HistoryGolden;
}

export function loadSurfaces(): SurfacesGolden {
  return JSON.parse(readFileSync(join(golden, "probes", "surfaces.json"), "utf-8")) as SurfacesGolden;
}

/** Whitespace-normalised text, the reduction the golden's renderedText was made with. */
export function normalisedText(element: Element | null): string {
  return (element?.textContent ?? "").replace(/\s+/g, " ").trim();
}

function numbers(value: string): number[] {
  return value
    .split(/[\s,]+/)
    .filter((part) => part.length > 0)
    .map(Number);
}

/**
 * Coordinates compared element by element within `tolerance` view-box units (research R8): the
 * reference rounds half to even and the port half up, so a coordinate may differ by 0.01.
 */
export function expectCoordsClose(actual: string, expected: string, tolerance = 0.01): void {
  const a = numbers(actual);
  const e = numbers(expected);
  expect(a.length, `coordinate count of "${actual}"`).toBe(e.length);
  a.forEach((value, i) => {
    expect(Number.isFinite(value), `coordinate ${i} of "${actual}" is a number`).toBe(true);
    expect(Math.abs(value - e[i]!), `coordinate ${i}: ${value} vs ${e[i]}`).toBeLessThanOrEqual(tolerance + 1e-9);
  });
}
