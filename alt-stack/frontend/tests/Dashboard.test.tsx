// T094: the Dashboard owns the fetch lifecycle, the chosen window and the sync-in-flight flag
// (data-model.md §6, dashboard-ui.md §2–§3). The DashboardApi is a plain object whose promises
// the test settles by hand (tests/deferred.ts).
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { Dashboard } from "../src/components/Dashboard";
import { content, loaded, rail, settle } from "./dashboardHarness";
import { controlledApi } from "./deferred";
import { loadHistory, loadSurfaces, normalisedText } from "./golden";
import { syncStatusRows } from "./syncStatus";

afterEach(cleanup);

const h3f = loadHistory("H3f");
const long = loadHistory("consecutive-200");
const rows = syncStatusRows();

describe("Dashboard", () => {
  it("reads the view and the sync status once on load", async () => {
    const c = controlledApi();
    render(<Dashboard api={c.api} />);

    expect(c.calls).toEqual({ fetchDashboard: 1, fetchSyncStatus: 1, postSync: 0 });
  });

  it("titles the page Training Load", async () => {
    await loaded(h3f.view);

    expect(document.title).toBe("Training Load");
  });

  it("renders the rail and the populated column: metric row, chart and recent list", async () => {
    const { container } = await loaded(h3f.view);

    expect(rail(container)).not.toBeNull();
    expect(content(container)?.querySelector(".metrics")).not.toBeNull();
    expect(content(container)?.querySelector(".block > section.chart")).not.toBeNull();
    expect(content(container)?.querySelector(".block > section.recent")).not.toBeNull();
    expect(container.querySelectorAll("h1")).toHaveLength(1);
  });

  it.each([h3f, long, loadHistory("consecutive-35"), loadHistory("short-history")])(
    "renders the golden rail and content of $name",
    async (golden) => {
      const { container } = await loaded(golden.view);

      expect(normalisedText(rail(container))).toBe(golden.renderedText.rail);
      expect(normalisedText(content(container))).toBe(golden.renderedText.content);
    },
  );

  it("switches the window on the client, with no request", async () => {
    const c = await loaded(long.view);
    const before = c.total();

    expect(c.container.querySelector(".chart-head h2")?.textContent).toBe("Daily load and metrics · last 180 days");
    expect(c.container.querySelectorAll("rect.load-bar")).toHaveLength(180);

    fireEvent.click(screen.getByRole("radio", { name: "30 days" }));

    expect(c.container.querySelector(".chart-head h2")?.textContent).toBe("Daily load and metrics · last 30 days");
    expect(c.container.querySelectorAll("rect.load-bar")).toHaveLength(30);
    expect(screen.getByRole("radio", { name: "30 days" })).toHaveProperty("checked", true);
    expect(normalisedText(c.container.querySelector("section.chart"))).toBe(long.renderedText.chart30);
    await settle();
    expect(c.total()).toBe(before);
  });

  it("changes nothing and asks for nothing when the pointer moves over the chart", async () => {
    const c = await loaded(long.view);
    const before = c.total();
    const html = c.container.innerHTML;
    const slots = c.container.querySelectorAll(".hover-layer > .day");

    expect(slots).toHaveLength(180);
    for (const slot of slots) {
      fireEvent.pointerMove(slot);
      fireEvent.mouseOver(slot);
      fireEvent.mouseEnter(slot);
    }
    await settle();

    expect(c.total()).toBe(before);
    expect(c.container.innerHTML).toBe(html);
  });

  it("shows the running state the moment Sync Activities is pressed, before the sync answers", async () => {
    const c = await loaded(h3f.view);
    const button = () => c.container.querySelector<HTMLButtonElement>("button.sync")!;

    expect(c.container.querySelector("button.sync")).not.toBeNull();
    expect(button().disabled).toBe(false);
    fireEvent.click(button());

    expect(c.calls.postSync).toBe(1);
    expect(button().textContent?.trim()).toBe("Syncing…");
    expect(button().disabled).toBe(true);
    expect(c.container.querySelector(".sync-message")?.textContent).toBe("Syncing activities…");
    expect(normalisedText(c.container.querySelector(".sync-panel"))).toBe(loadSurfaces().syncPanel["running"]);
  });

  it("renders the returned status, then re-reads the view exactly once and replaces it", async () => {
    const c = await loaded(h3f.view);
    expect(c.container.querySelector("button.sync")).not.toBeNull();
    fireEvent.click(c.container.querySelector("button.sync")!);

    await act(async () => c.sync[0]!.resolve(rows["completedImported"]!));

    expect(normalisedText(c.container.querySelector(".sync-panel"))).toBe(
      "Sync Activities3 activities imported.Last checked 2026-09-18 07:07",
    );
    expect(c.calls.fetchDashboard).toBe(2);

    await act(async () => c.dashboard[1]!.resolve(long.view));
    await settle();

    expect(normalisedText(content(c.container))).toBe(long.renderedText.content);
    expect(c.calls).toEqual({ fetchDashboard: 2, fetchSyncStatus: 1, postSync: 1 });
  });

  it("shows a sync already running on load, and does not poll", async () => {
    const c = await loaded(h3f.view, rows["running"]!);
    await settle();

    const button = c.container.querySelector<HTMLButtonElement>("button.sync");
    expect(button).not.toBeNull();
    expect(button!.textContent?.trim()).toBe("Syncing…");
    expect(button!.disabled).toBe(true);
    expect(normalisedText(c.container.querySelector(".sync-panel"))).toBe(loadSurfaces().syncPanel["running"]);
    expect(c.calls).toEqual({ fetchDashboard: 1, fetchSyncStatus: 1, postSync: 0 });
  });
});
