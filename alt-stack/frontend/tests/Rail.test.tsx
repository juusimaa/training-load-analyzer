// T092: the rail cases of DashboardComponentTests.cs and InformationPreservationTests.cs.
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { Rail } from "../src/components/Rail";
import type { DashboardView, SyncStatusView } from "../src/types";
import { historyNames, loadHistory, normalisedText } from "./golden";
import { never } from "./syncStatus";

afterEach(cleanup);

const h3f = loadHistory("H3f").view;

function rail(view: DashboardView = h3f, options: { windowDays?: number; onWindowChange?: (n: number) => void; status?: SyncStatusView } = {}) {
  return render(
    <Rail
      asOf={view.asOf}
      isoWeek={view.isoWeek}
      maximumHeartRate={view.maximumHeartRate}
      windowDays={options.windowDays ?? 180}
      onWindowChange={options.onWindowChange ?? (() => {})}
      syncStatus={options.status ?? never}
      onSync={() => {}}
    />,
  ).container;
}

describe("Rail", () => {
  it("is the aside that carries the page's one h1", () => {
    const container = rail();
    const h1s = container.querySelectorAll("h1");

    expect(container.querySelector("aside.rail")).not.toBeNull();
    expect(h1s).toHaveLength(1);
    expect(h1s[0]!.className).toBe("masthead");
    expect(normalisedText(h1s[0]!)).toBe("Training Load");
    expect(h1s[0]!.querySelector(".rule")).not.toBeNull();
    expect(h1s[0]!.querySelector(".wordmark")?.textContent).toBe("Training Load");
  });

  it("names its regions with headings", () => {
    const headings = [...rail().querySelectorAll(".rail-block > h2.kicker")].map((h) => h.textContent);

    expect(headings).toEqual(["As of", "Window", "Strava", "Max heart rate"]);
  });

  it("states the as-of day, its ISO week and the maximum heart rate from configuration", () => {
    const values = [...rail().querySelectorAll(".rail-value, .rail-note")].map((e) => normalisedText(e));

    expect(values).toEqual(["2026-09-18", "ISO week 2026-W38", "190 bpm", "from configuration"]);
  });

  it("offers the three windows as a radio group, 180 days checked by default", () => {
    rail();
    const group = screen.getByRole("radiogroup", { name: "Chart window" });
    const radios = within(group).getAllByRole("radio") as HTMLInputElement[];

    expect(radios.map((r) => r.closest("label")?.textContent?.trim())).toEqual(["30 days", "90 days", "180 days"]);
    expect(radios.map((r) => r.name)).toEqual(["window", "window", "window"]);
    expect(radios.map((r) => r.value)).toEqual(["30", "90", "180"]);
    expect(within(group).getByRole("radio", { name: "180 days" })).toHaveProperty("checked", true);
    expect(radios.filter((r) => r.checked)).toHaveLength(1);
    expect(group.className).toBe("seg");
    expect([...group.querySelectorAll("label")].every((l) => l.className === "seg-opt")).toBe(true);
  });

  it("reports the window the athlete chooses", () => {
    const chosen: number[] = [];
    rail(h3f, { onWindowChange: (n) => chosen.push(n) });

    fireEvent.click(screen.getByRole("radio", { name: "30 days" }));

    expect(chosen).toEqual([30]);
  });

  it("checks the window it is given", () => {
    rail(h3f, { windowDays: 90 });

    expect(screen.getByRole("radio", { name: "90 days" })).toHaveProperty("checked", true);
    expect(screen.getByRole("radio", { name: "180 days" })).toHaveProperty("checked", false);
  });

  it("holds the sync control", () => {
    expect(rail().querySelector(".rail-block .sync-panel button.sync")).not.toBeNull();
  });

  it.each(historyNames())("renders the golden text of %s", (name) => {
    const { view, renderedText } = loadHistory(name);

    expect(normalisedText(rail(view))).toBe(renderedText.rail);
  });
});
