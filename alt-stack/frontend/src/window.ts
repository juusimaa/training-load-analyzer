// The chart window: a trailing slice of the days already loaded, so choosing one issues no
// request (008 research R6, 009 US3 scenario 2).
import type { Day } from "./types";

/** The windows the athlete can choose between, widest last and the default (008 FR-005). */
export const WINDOWS: readonly number[] = [30, 90, 180];
export const DEFAULT_WINDOW = 180;

/** The trailing `n` days, in order; the whole series when it is shorter than that. */
export function windowed(days: readonly Day[], n: number): Day[] {
  return days.length <= n ? [...days] : days.slice(days.length - n);
}
