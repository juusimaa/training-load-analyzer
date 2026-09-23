// T090: RecentActivitiesTests.cs and the recent-list cases of DashboardComponentTests.cs.
import { cleanup, render } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { RecentActivities } from "../src/components/RecentActivities";
import type { Recent } from "../src/types";
import { historyNames, loadHistory, normalisedText } from "./golden";

afterEach(cleanup);

const h3f = loadHistory("H3f").view;

const entry = (provenance: Recent["provenance"]): Recent => ({
  day: "2026-09-18",
  type: "Running",
  movingTime: "1h 00m",
  provenance,
  load: "120.0",
});

describe("RecentActivities", () => {
  it("is headed, with the five column headings", () => {
    const { container } = render(<RecentActivities activities={h3f.recent} />);

    expect(container.querySelector("section.recent > h2")?.textContent).toBe("Recent activities");
    expect([...container.querySelectorAll("table.table thead th")].map((th) => th.textContent?.trim())).toEqual([
      "Day", "Type", "Moving time", "Basis", "Load",
    ]);
  });

  it("is a table with one row of five cells per session, newest first", () => {
    const { container } = render(<RecentActivities activities={h3f.recent} />);
    const rows = [...container.querySelectorAll("tr.recent-row")];

    expect(rows).toHaveLength(7);
    for (const row of rows) expect(row.querySelectorAll("td")).toHaveLength(5);
    expect(rows.map((r) => r.querySelector(".recent-day")?.textContent?.trim())).toEqual(h3f.recent.map((r) => r.day));
    expect(rows[0]!.querySelector(".recent-type")?.textContent?.trim()).toBe("Cycling");
    expect(rows[0]!.querySelector(".recent-duration")?.textContent?.trim()).toBe("45m");
    expect(rows[0]!.querySelector(".recent-load")?.textContent).toBe("177.0");
  });

  it("words each row's provenance in a tag, not only in its colour", () => {
    const measured = render(<RecentActivities activities={[entry("measured")]} />);
    expect(measured.container.querySelector("tr.recent-row span.tag.tag-accent")?.textContent?.trim()).toBe("measured");
    const measuredText = normalisedText(measured.container);
    cleanup();

    const estimated = render(<RecentActivities activities={[entry("estimated")]} />);
    expect(estimated.container.querySelector("tr.recent-row span.tag.tag-outline")?.textContent?.trim()).toBe("estimated");

    // With every class removed, the two still differ.
    expect(normalisedText(estimated.container)).not.toBe(measuredText);
  });

  it("says so when nothing has been recorded", () => {
    const { container } = render(<RecentActivities activities={[]} />);

    expect(container.querySelector(".recent-empty")?.textContent).toBe("Nothing recorded yet.");
    expect(container.querySelectorAll(".recent-row")).toHaveLength(0);
    expect(container.querySelector("table")).toBeNull();
  });

  it.each(historyNames())("renders the golden text of %s", (name) => {
    const { view, renderedText } = loadHistory(name);
    const { container } = render(<RecentActivities activities={view.recent} />);

    expect(normalisedText(container)).toBe(renderedText.recent);
  });
});
