import type { Day } from "../types";

export interface MetricsChartProps {
  /** The days to plot, already sliced to the selected window. */
  days: Day[];
  hasEnoughHistory: boolean;
  windowDays: number;
}

export function MetricsChart(_props: MetricsChartProps) {
  return null;
}
