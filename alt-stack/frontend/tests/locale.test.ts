// T107 (SC-007): nothing the page draws may depend on the ambient locale. `npm run test:fi` runs
// the whole suite under LANG=fi_FI.UTF-8 LC_ALL=fi_FI.UTF-8. Node reads the locale from the
// environment at process start, so this file first proves the Finnish locale really took effect
// (a run where it did not would pass vacuously), then checks the geometry against the golden.
import { describe, expect, it } from "vitest";
import { axisTicks, hoverSlots, loadBars, plot, zeroRule } from "../src/chart/geometry";
import { windowed } from "../src/window";
import { expectCoordsClose, loadHistory } from "./golden";

const finnish = (process.env["LC_ALL"] ?? "").startsWith("fi_FI");
/** What the ambient locale makes of 1.5; recorded so a failure shows which locale ran. */
const ambient = new Intl.NumberFormat().format(1.5);

describe(`locale (LC_ALL=${process.env["LC_ALL"] ?? "unset"}, 1.5 formats as "${ambient}")`, () => {
  // One test in both runs, so the two runs' pass counts stay identical; the branch is chosen by
  // LC_ALL, as T107 says.
  it("records the ambient locale, and under test:fi proves it is Finnish", () => {
    if (finnish) expect(ambient).toBe("1,5");
    else expect(ambient).toMatch(/^1\D5$/);
  });

  const h3f = loadHistory("H3f");
  const long = loadHistory("consecutive-200");

  it.each([
    ["H3f", h3f],
    ["consecutive-200", long],
  ] as const)("the %s geometry equals the golden whatever the locale", (_name, golden) => {
    for (const w of ["180", "90", "30"] as const) {
      const days = windowed(golden.view.days, Number(w));
      const expected = golden.geometry[w];

      for (const series of plot(days, 1000, 300)) {
        expect(series.points).toMatch(/^-?\d+(\.\d+)?,-?\d+(\.\d+)?( -?\d+(\.\d+)?,-?\d+(\.\d+)?)*$/);
        expectCoordsClose(series.points, expected.plot[series.label]!);
      }
      const bars = loadBars(days, 1000, 300);
      expect(bars).toHaveLength(expected.bars.length);
      bars.forEach((bar, i) => {
        for (const value of [bar.x, bar.y, bar.width, bar.height]) expect(value).toMatch(/^-?\d+(\.\d+)?$/);
        expectCoordsClose([bar.x, bar.y, bar.width, bar.height].join(" "), expected.bars[i]!.join(" "));
      });
      const rule = zeroRule(days, 1000, 300);
      expect(rule === null).toBe(expected.zeroRule === null);
      if (rule !== null) expectCoordsClose(rule, expected.zeroRule!);
      expect(axisTicks(days, 6)).toEqual(expected.ticks);
      hoverSlots(days).forEach((slot, i) => {
        for (const value of [slot.left, slot.width, ...slot.points.map((p) => p.top)]) expect(value).toMatch(/^-?\d+(\.\d+)?$/);
        expectCoordsClose(slot.left, expected.slots[i]!.left);
      });
    }
  });
});
