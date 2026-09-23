// T092: the SyncPanel cases of DashboardComponentTests.cs, and every row of the message table
// against parity/golden/probes/surfaces.json.
import { cleanup, fireEvent, render } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { SyncPanel } from "../src/components/SyncPanel";
import type { SyncStatusView } from "../src/types";
import { loadSurfaces, normalisedText } from "./golden";
import { never, syncStatusRows } from "./syncStatus";

afterEach(cleanup);

const rows = syncStatusRows();
const panel = (status: SyncStatusView, onSync = () => {}) =>
  render(<SyncPanel status={status} onSync={onSync} />).container;

describe("SyncPanel", () => {
  it("offers an enabled Sync Activities button when idle, and pressing it asks for a sync", () => {
    let pressed = 0;
    const container = panel(never, () => {
      pressed += 1;
    });
    const button = container.querySelector<HTMLButtonElement>(".sync-panel > button.sync");

    expect(button?.textContent?.trim()).toBe("Sync Activities");
    expect(button?.type).toBe("button");
    expect(button?.className).toBe("btn btn-primary btn-block sync");
    expect(button?.disabled).toBe(false);
    fireEvent.click(button!);
    expect(pressed).toBe(1);
  });

  it("reads Syncing… and cannot be pressed while a sync runs", () => {
    const container = panel(rows["running"]!);
    const button = container.querySelector<HTMLButtonElement>("button.sync");

    expect(button?.textContent?.trim()).toBe("Syncing…");
    expect(button?.disabled).toBe(true);
    expect(container.querySelector("p.sync-message")?.textContent).toBe("Syncing activities…");
  });

  it.each(["notConnected", "reconnectionRequired"])("offers the Connect Strava route when %s", (row) => {
    const link = panel(rows[row]!).querySelector("a.btn.btn-primary.btn-block.connect");

    expect(link?.getAttribute("href")).toBe("/connect");
    expect(link?.textContent).toBe("Connect Strava");
  });

  it("offers no Connect route when a connection is in place", () => {
    expect(panel(rows["completedImported"]!).querySelector("a")).toBeNull();
  });

  it("states when it last checked", () => {
    const status = { ...never, message: "Already up to date.", lastChecked: "2026-09-18 07:15" };

    expect(panel(status).querySelector("p.sync-when")?.textContent).toBe("Last checked 2026-09-18 07:15");
  });

  it("says nothing when there is nothing to say", () => {
    const container = panel(never);

    expect(container.querySelector(".sync-message")).toBeNull();
    expect(container.querySelector(".sync-when")).toBeNull();
  });

  it("states a rate limit's retry time in its existing words", () => {
    expect(panel(rows["rateLimitedKnown"]!).querySelector(".sync-message")?.textContent).toBe(
      "Rate limited by Strava. Available again at 07:15.",
    );
  });

  const surfaces = loadSurfaces().syncPanel;

  it("has a status for every golden row", () => {
    expect(Object.keys(rows).sort()).toEqual(Object.keys(surfaces).sort());
  });

  it.each(Object.keys(surfaces))("renders the golden text of the %s row", (row) => {
    expect(normalisedText(panel(rows[row]!))).toBe(surfaces[row]);
  });
});
