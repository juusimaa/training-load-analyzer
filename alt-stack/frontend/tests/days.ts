// Synthetic day series for the ported MetricsChartTests / DashboardComponentTests fixtures.
// Fitness is 40 + i, Fatigue 20 + 2i, so Form is 20 − i and crosses zero on day 20.
import type { Day } from "../src/types";

const today = Date.UTC(2026, 8, 18);
const dayMs = 24 * 60 * 60 * 1000;

/** The ISO day `offset` days before 2026-09-18 (the fixtures' today). */
export function isoDay(offset: number): string {
  return new Date(today - offset * dayMs).toISOString().slice(0, 10);
}

/** A whole number as the one-decimal string the backend would send ("40.0", "-5.0"). */
function oneDecimal(value: number): string {
  if (!Number.isInteger(value)) throw new Error(`fixture value ${value} is not whole`);
  return `${value}.0`;
}

/** `count` days ending today, with the given loads (default: a varying 60–220). */
export function series(count: number, loads?: readonly number[]): Day[] {
  return Array.from({ length: count }, (_, i) => {
    const fitness = 40 + i;
    const fatigue = 20 + i * 2;
    const form = fitness - fatigue;
    const load = loads ? loads[i]! : 60 + (i % 5) * 40;
    return {
      day: isoDay(count - 1 - i),
      fitness,
      fatigue,
      form,
      load,
      display: {
        fitness: oneDecimal(fitness),
        fatigue: oneDecimal(fatigue),
        form: oneDecimal(form),
        load: oneDecimal(load),
      },
    };
  });
}

/** Days with the given loads and the standard metric series. */
export function withLoads(...loads: number[]): Day[] {
  return series(loads.length, loads);
}
