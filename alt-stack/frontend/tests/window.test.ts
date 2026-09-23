// T084: the chart window is a trailing slice of the days already loaded (008 research R6).
import { describe, expect, it } from "vitest";
import { DEFAULT_WINDOW, WINDOWS, windowed } from "../src/window";
import { series } from "./days";

describe("window", () => {
  it("offers 30, 90 and 180 days, 180 by default", () => {
    expect(WINDOWS).toEqual([30, 90, 180]);
    expect(DEFAULT_WINDOW).toBe(180);
  });

  it("takes the trailing 30 of 200 days, in order", () => {
    const days = series(200);

    expect(windowed(days, 30)).toEqual(days.slice(170));
  });

  it("returns every day of a history shorter than the window", () => {
    const days = series(20);

    expect(windowed(days, 90)).toEqual(days);
  });
});
