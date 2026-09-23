// T098: every state of the page that is not the populated dashboard (dashboard-ui.md §1), ported
// from the state cases of DashboardComponentTests.cs, with the content column and rail compared
// against the goldens.
import { act, cleanup, fireEvent, render } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { Unavailable } from "../src/api";
import { Dashboard } from "../src/components/Dashboard";
import { isoWeek } from "../src/isoWeek";
import { content, loaded, rail } from "./dashboardHarness";
import { controlledApi } from "./deferred";
import { loadHistory, loadSurfaces, normalisedText } from "./golden";
import { never, syncStatusRows } from "./syncStatus";

afterEach(cleanup);

const rows = syncStatusRows();
const h3f = loadHistory("H3f");
const unavailableText =
  "Data unavailable Your stored training history could not be read. Nothing has been lost — try again, and if it persists, the application log says why.";
const railValues = (container: HTMLElement) =>
  [...container.querySelectorAll(".rail-value, .rail-note")].map((e) => normalisedText(e));

/** The browser's local calendar day, written out independently of the code under test. */
function today(): string {
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${String(now.getFullYear())}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

/** surfaces.json's loading rail, filled from the browser's clock, with `—` for `0 bpm` (Amendment 1(c)). */
function loadingRail(): string {
  return loadSurfaces()
    .loading.rail.replace("{asOf}", today())
    .replace("{isoWeek}", isoWeek(new Date()))
    .replace("0 bpm", "—");
}

describe("Dashboard states", () => {
  it("while the history is read: the column says so, the rail shows today and no heart rate", async () => {
    const c = controlledApi();
    const { container } = render(<Dashboard api={c.api} />);
    await act(async () => c.status[0]?.resolve(never));

    expect(normalisedText(content(container))).toBe(loadSurfaces().loading.content);
    expect(normalisedText(rail(container))).toBe(loadingRail());
    expect(normalisedText(rail(container))).not.toContain("0 bpm");
    expect(container.querySelector(".tile-value")).toBeNull();
    expect(rail(container)?.querySelector("button.sync")).not.toBeNull();
  });

  it("an unreadable history shows the distinct notice in place of the figures", async () => {
    const { container } = await loaded({ ...h3f.view, isUnavailable: true });

    expect(content(container)?.querySelector(".state.state-unavailable > h2")?.textContent).toBe("Data unavailable");
    expect(normalisedText(content(container))).toBe(unavailableText);
    expect(container.querySelector(".tile-value")).toBeNull();
    expect(container.querySelector("svg")).toBeNull();
    expect(container.querySelector("table")).toBeNull();
    expect(railValues(container)).toEqual(["2026-09-18", "ISO week 2026-W38", "190 bpm", "from configuration"]);
  });

  it("a failed request shows the same notice, with no figure and nothing stale", async () => {
    const c = controlledApi();
    const { container } = render(<Dashboard api={c.api} />);
    await act(async () => {
      c.status[0]?.resolve(never);
      c.dashboard[0]?.reject(new Unavailable("GET /api/dashboard answered 500"));
    });

    expect(normalisedText(content(container))).toBe(unavailableText);
    expect(content(container)?.querySelector(".state-unavailable")).not.toBeNull();
    expect(normalisedText(content(container))).not.toMatch(/\d/);
    expect(normalisedText(rail(container))).toBe(loadingRail());
  });

  it("a failure after a good load keeps the rail's last good values and replaces the column", async () => {
    const c = await loaded(h3f.view);
    const button = c.container.querySelector("button.sync");
    expect(button).not.toBeNull();
    fireEvent.click(button!);
    await act(async () => c.sync[0]!.resolve(rows["completedImported"]!));
    await act(async () => c.dashboard[1]!.reject(new Unavailable("GET /api/dashboard failed")));

    expect(normalisedText(content(c.container))).toBe(unavailableText);
    expect(normalisedText(content(c.container))).not.toMatch(/\d/);
    expect(railValues(c.container)).toEqual(["2026-09-18", "ISO week 2026-W38", "190 bpm", "from configuration"]);
  });

  it("connected with nothing imported: the column explains itself, with no redundant Connect", async () => {
    const empty = loadHistory("empty");
    const { container } = await loaded(empty.view);

    expect(normalisedText(content(container))).toBe(
      "No activities recorded Your Strava account is connected, but nothing has been imported yet. Sync to bring your training in.",
    );
    expect(content(container)?.querySelector('a[href="/connect"]')).toBeNull();
    expect(rail(container)?.querySelector("button.sync")).not.toBeNull();
    expect(container.querySelector(".tile-value")).toBeNull();
  });

  it("unconnected with nothing imported: the column offers the Connect route", async () => {
    const { container } = await loaded(loadHistory("empty-unconnected").view);
    const link = content(container)?.querySelector("a.btn.btn-primary.connect");

    expect(normalisedText(content(container))).toContain("Connect your Strava account to bring your training in.");
    expect(link?.getAttribute("href")).toBe("/connect");
    expect(link?.textContent).toBe("Connect Strava");
    expect(container.querySelector(".tile-value")).toBeNull();
  });

  it("a rejected credential (unauthorized-401) says so in the rail and offers the Connect route", async () => {
    const { container } = await loaded(h3f.view, rows["reconnectionRequired"]!);
    const panel = rail(container)?.querySelector(".sync-panel");

    expect(panel?.querySelector(".sync-message")?.textContent).toBe("Strava connection required.");
    expect(panel?.querySelector('a[href="/connect"]')?.textContent).toBe("Connect Strava");
  });

  it("a rate-limited sync states its retry time in the rail", async () => {
    const { container } = await loaded(h3f.view, rows["rateLimitedKnown"]!);

    expect(rail(container)?.querySelector(".sync-message")?.textContent).toBe(
      "Rate limited by Strava. Available again at 07:15.",
    );
  });

  it.each(["empty", "empty-unconnected", "short-history", "H3f"])("renders the golden rail and content of %s", async (name) => {
    const golden = loadHistory(name);
    const { container } = await loaded(golden.view);

    expect(normalisedText(content(container))).toBe(golden.renderedText.content);
    expect(normalisedText(rail(container))).toBe(golden.renderedText.rail);
  });
});
