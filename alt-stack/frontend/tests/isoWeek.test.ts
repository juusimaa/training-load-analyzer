// T099: the loading rail's ISO week, from the browser's local date (dashboard-ui.md §1), with the
// year-boundary cases from T027. The result depends only on the local calendar day, never on
// the process time zone: `npm run test:tz` runs this file under Pacific/Kiritimati (UTC+14) and
// America/Adak (UTC−10/−9) as well.
import { describe, expect, it } from "vitest";
import { isoWeek, localDay } from "../src/isoWeek";

/** A local calendar day, as the browser's clock would give it. */
const local = (year: number, month: number, day: number, hour = 12) => new Date(year, month - 1, day, hour);

describe("isoWeek", () => {
  it.each([
    [2026, 12, 31, "2026-W53"],
    [2027, 1, 1, "2026-W53"],
    [2021, 1, 3, "2020-W53"],
    [2021, 1, 4, "2021-W01"],
    [2026, 9, 18, "2026-W38"],
    [2026, 1, 1, "2026-W01"],
    [2024, 12, 30, "2025-W01"],
    [2026, 2, 2, "2026-W06"],
  ])("designates %i-%i-%i as %s", (year, month, day, expected) => {
    expect(isoWeek(local(year, month, day))).toBe(expected);
  });

  it("is the same at every hour of the local day", () => {
    for (const hour of [0, 1, 11, 13, 22, 23]) {
      expect(isoWeek(local(2027, 1, 1, hour))).toBe("2026-W53");
      expect(isoWeek(local(2021, 1, 3, hour))).toBe("2020-W53");
    }
  });
});

describe("localDay", () => {
  it("formats the local calendar day as YYYY-MM-DD at every hour", () => {
    for (const hour of [0, 12, 23]) {
      expect(localDay(local(2026, 9, 18, hour))).toBe("2026-09-18");
      expect(localDay(local(2027, 1, 1, hour))).toBe("2027-01-01");
    }
  });
});
