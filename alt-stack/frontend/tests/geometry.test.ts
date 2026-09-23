// T085: MetricsChartTests.cs, ported, plus parity with every history golden's geometry for the
// 180-, 90- and 30-day windows (research R8, parity.md §5).
import { describe, expect, it } from "vitest";
import { axisTicks, hoverSlots, loadBars, plot, viewBox, zeroRule } from "../src/chart/geometry";
import type { Day } from "../src/types";
import { windowed } from "../src/window";
import { series, withLoads } from "./days";
import { expectCoordsClose, historyNames, loadHistory } from "./golden";

const Width = 600;
const Height = 200;

const ys = (points: string) => points.split(" ").map((p) => Number(p.split(",")[1]));
const invariant = /^-?\d+(\.\d+)?$/;

describe("MetricsChart (ported)", () => {
  it("formats the view box invariantly", () => {
    expect(viewBox(1000, 300)).toBe("0 0 1000 300");
  });

  it("three series carry one point per day", () => {
    const plotted = plot(series(5), Width, Height);

    expect(plotted.map((s) => s.label)).toEqual(["Fitness", "Fatigue", "Form"]);
    for (const s of plotted) expect(s.points.split(" ").filter(Boolean)).toHaveLength(5);
  });

  it("every coordinate carries exactly one comma and two invariant numbers", () => {
    for (const s of plot(series(5), Width, Height)) {
      for (const point of s.points.split(" ")) {
        const parts = point.split(",");
        expect(parts).toHaveLength(2);
        expect(parts[0]).toMatch(invariant);
        expect(parts[1]).toMatch(invariant);
      }
    }
  });

  it("a negative form is plotted rather than clipped", () => {
    const days = series(30);
    const form = plot(days, Width, Height).find((s) => s.label === "Form");

    expect(days.some((d) => d.form < 0)).toBe(true);
    expect(form).toBeDefined();
    for (const y of ys(form!.points)) {
      expect(y).toBeGreaterThanOrEqual(0);
      expect(y).toBeLessThanOrEqual(Height);
    }
  });

  it("an empty history yields no series at all", () => {
    expect(plot([], Width, Height)).toEqual([]);
  });

  it("each series carries a class and never a colour", () => {
    const plotted = plot(series(5), Width, Height);

    expect(plotted.map((s) => s.cssClass)).toEqual(["series-fitness", "series-fatigue", "series-form"]);
    expect(plotted.map((s) => s.cssClass + s.points).join(" ")).not.toContain("#");
  });

  it("one bar is drawn for each day that carried load", () => {
    expect(loadBars(withLoads(120, 0, 240, 0, 60), Width, Height)).toHaveLength(3);
  });

  it("a history with no load draws no bars", () => {
    expect(loadBars(withLoads(0, 0, 0), Width, Height)).toEqual([]);
    expect(loadBars([], Width, Height)).toEqual([]);
  });

  it("the bars are scaled on their own axis, not the metric band", () => {
    const tallest = (days: Day[]) => Math.max(...loadBars(days, Width, Height).map((b) => Number(b.height)));
    const heavy = tallest(withLoads(300, 600, 900));

    expect(tallest(withLoads(30, 60, 90))).toBeCloseTo(heavy, 6);
    expect(heavy).toBeGreaterThan(0);
    expect(heavy).toBeLessThanOrEqual(Height);
  });

  it("every bar keeps a minimum width across a full window", () => {
    const bars = loadBars(series(180), Width, Height);

    expect(bars).toHaveLength(180);
    for (const bar of bars) expect(Number(bar.width)).toBeGreaterThanOrEqual(1);
  });

  it("bar geometry is machine readable", () => {
    for (const bar of loadBars(series(30), Width, Height)) {
      for (const value of [bar.x, bar.y, bar.width, bar.height]) expect(value).toMatch(invariant);
    }
  });

  it("the zero rule sits where the form line crosses zero", () => {
    const days = series(30);
    expect(days[20]!.form).toBe(0);

    const form = plot(days, Width, Height).find((s) => s.label === "Form");
    expect(form).toBeDefined();
    const crossing = form!.points.split(" ")[20]!.split(",")[1];

    expect(zeroRule(days, Width, Height)).toBe(crossing);
  });

  it("a band that excludes zero has no rule", () => {
    const positive = series(30).map((d) => ({ ...d, fitness: 50, fatigue: 10, form: 40 }));

    expect(zeroRule(positive, Width, Height)).toBeNull();
    expect(zeroRule([], Width, Height)).toBeNull();
  });

  it("the axis labels both ends of the window and evenly between", () => {
    const days = series(180);
    const ticks = axisTicks(days, 6);

    expect(ticks).toHaveLength(6);
    expect(ticks[0]).toBe(days[0]!.day);
    expect(ticks[5]).toBe(days[179]!.day);
    expect(new Set(ticks).size).toBe(6);
    for (const tick of ticks) expect(tick).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });

  it("a window shorter than the tick count labels every day it has", () => {
    expect(axisTicks(series(3), 6)).toHaveLength(3);
    expect(axisTicks([], 6)).toEqual([]);
  });

  it("a hover slot is offered for every day in the window", () => {
    expect(hoverSlots(series(30))).toHaveLength(30);
    expect(hoverSlots([])).toEqual([]);
  });

  it("the slots span the plot from the first day to the last", () => {
    const slots = hoverSlots(series(30));

    expect(slots).toHaveLength(30);
    expect(slots[0]!.left).toBe("0");
    expect(slots[29]!.left).toBe("100");
  });

  it("slots past 55 % of the plot open to the left", () => {
    const slots = hoverSlots(series(20));

    // index > 20 * 0.55 = 11
    expect(slots.map((s) => s.opensLeft)).toEqual(Array.from({ length: 20 }, (_, i) => i > 11));
  });

  it("each slot marks a point on every line at that day's value", () => {
    const days = series(30);
    const slots = hoverSlots(days);
    const plotted = plot(days, Width, Height);

    expect(slots).toHaveLength(30);
    expect(plotted).toHaveLength(3);
    for (const index of [0, 7, 29]) {
      const slot = slots[index]!;
      expect(slot.points).toHaveLength(3);
      for (const point of slot.points) {
        const line = plotted.find((s) => s.cssClass === point.cssClass)!;
        const y = ys(line.points)[index]!;
        expect((Number(point.top) / 100) * Height).toBeCloseTo(y, 1);
      }
    }
  });

  it("each slot reports the day and all four figures as text", () => {
    const days = series(30);
    const slots = hoverSlots(days);
    expect(slots).toHaveLength(30);
    const slot = slots[7]!;

    expect(slot.day).toBe(days[7]!.day);
    expect(slot.load).toBe(days[7]!.display.load);
    expect(slot.points.map((p) => p.label)).toEqual(["Fitness", "Fatigue", "Form"]);
    expect(slot.points.map((p) => p.value)).toEqual([
      days[7]!.display.fitness,
      days[7]!.display.fatigue,
      days[7]!.display.form,
    ]);
  });

  it("a rest day has no bar to emphasise but is still reported", () => {
    const slots = hoverSlots(withLoads(120, 0, 240));

    expect(slots).toHaveLength(3);
    expect(slots[1]!.barTop).toBeNull();
    expect(slots[1]!.barHeight).toBeNull();
    expect(slots[1]!.load).toBe("0.0");
    expect(slots[0]!.barTop).not.toBeNull();
    expect(slots[0]!.barHeight).not.toBeNull();
  });

  it("the emphasis band covers the bar it emphasises", () => {
    const days = withLoads(120, 60, 240);
    const slots = hoverSlots(days);
    const bars = loadBars(days, Width, Height);
    expect(slots).toHaveLength(3);
    expect(bars).toHaveLength(3);
    const heaviest = slots[2]!;
    const bar = bars[2]!;

    expect((Number(heaviest.barTop) / 100) * Height).toBeCloseTo(Number(bar.y), 1);
    expect((Number(heaviest.barHeight) / 100) * Height).toBeCloseTo(Number(bar.height), 1);
  });

  it("slot geometry is machine readable", () => {
    for (const slot of hoverSlots(series(30))) {
      const numbers = [slot.left, slot.width, slot.barTop, slot.barHeight, ...slot.points.map((p) => p.top)];
      for (const value of numbers) if (value !== null) expect(value).toMatch(invariant);
    }
  });
});

const windows = ["180", "90", "30"] as const;
const cases = historyNames().flatMap((name) => windows.map((w) => [name, w] as const));

describe.each(cases)("geometry parity: %s, %s-day window", (name, w) => {
  const golden = loadHistory(name);
  const expected = golden.geometry[w];
  const days = windowed(golden.view.days, Number(w));

  it("view box", () => {
    expect(viewBox(1000, 300)).toBe(expected.viewBox);
  });

  it("plot", () => {
    const plotted = plot(days, 1000, 300);

    expect(plotted.map((s) => s.label)).toEqual(Object.keys(expected.plot));
    expect(plotted.map((s) => s.cssClass)).toEqual(plotted.length === 0 ? [] : expected.plotClasses);
    for (const s of plotted) expectCoordsClose(s.points, expected.plot[s.label]!);
  });

  it("load bars", () => {
    const bars = loadBars(days, 1000, 300);

    expect(bars).toHaveLength(expected.bars.length);
    bars.forEach((bar, i) => expectCoordsClose([bar.x, bar.y, bar.width, bar.height].join(" "), expected.bars[i]!.join(" ")));
  });

  it("zero rule", () => {
    const rule = zeroRule(days, 1000, 300);

    expect(rule === null).toBe(expected.zeroRule === null);
    if (rule !== null) expectCoordsClose(rule, expected.zeroRule!);
  });

  it("axis ticks", () => {
    expect(axisTicks(days, 6)).toEqual(expected.ticks);
  });

  it("hover slots", () => {
    const slots = hoverSlots(days);

    expect(slots).toHaveLength(expected.slots.length);
    slots.forEach((slot, i) => {
      const e = expected.slots[i]!;
      expect(slot.day).toBe(e.day);
      expect(slot.load).toBe(e.load);
      expect(slot.opensLeft).toBe(e.opensLeft);
      expectCoordsClose(slot.left, e.left);
      expectCoordsClose(slot.width, e.width);
      expect(slot.barTop === null).toBe(e.barTop === null);
      expect(slot.barHeight === null).toBe(e.barHeight === null);
      if (slot.barTop !== null) expectCoordsClose(slot.barTop, e.barTop!);
      if (slot.barHeight !== null) expectCoordsClose(slot.barHeight, e.barHeight!);
      expect(slot.points.map((p) => [p.label, p.value])).toEqual(e.points.map(([label, value]) => [label, value]));
      slot.points.forEach((p, j) => expectCoordsClose(p.top, e.points[j]![2]));
    });
  });
});
