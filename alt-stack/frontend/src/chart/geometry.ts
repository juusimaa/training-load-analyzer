// A pure port of src/TrainingLoadAnalyzer.Web/Features/Dashboard/MetricsChart.cs (008 FR-006,
// Amendments 2 and 3). Input is the windowed day list; output is strings, already rounded.
//
// Every number that reaches the markup goes through `num`: rounded to two places with
// Math.round and printed with String(), never toFixed or toLocaleString, so no locale can turn
// a decimal point into a comma (research R2, R8). The reference rounds half to even, this rounds
// half up, so a coordinate may differ from the golden by 0.01 view-box units (parity.md §5).
import type { Day } from "../types";

export interface ChartSeries {
  label: string;
  points: string;
  cssClass: string;
}

export interface LoadBar {
  x: string;
  y: string;
  width: string;
  height: string;
}

export interface SlotPoint {
  cssClass: string;
  label: string;
  value: string;
  top: string;
}

export interface HoverSlot {
  day: string;
  left: string;
  width: string;
  barTop: string | null;
  barHeight: string | null;
  load: string | null;
  opensLeft: boolean;
  points: SlotPoint[];
}

/** Breathing room above and below the metric band, as a share of its span. */
const Padding = 0.08;
/** The share of the plot's height the heaviest day's bar fills. */
const BarShareOfPlot = 0.5;
/** The narrowest a bar may be drawn, so a 180-day window keeps a visible backdrop. */
const MinimumBarWidth = 2;
/** The gap between neighbouring bars. */
const BarGap = 2;
/** Past this share of the slots, the readout opens to the left. */
const OpensLeftAfter = 0.55;

type Metric = "fitness" | "fatigue" | "form";

const Series: readonly { label: string; cssClass: string; metric: Metric }[] = [
  { label: "Fitness", cssClass: "series-fitness", metric: "fitness" },
  { label: "Fatigue", cssClass: "series-fatigue", metric: "fatigue" },
  { label: "Form", cssClass: "series-form", metric: "form" },
];

function num(value: number): string {
  return String(Math.round(value * 100) / 100);
}

/** The vertical band all three series share, with its breathing room applied. */
function band(days: readonly Day[]): { lowest: number; highest: number } {
  const values = days.flatMap((d) => [d.fitness, d.fatigue, d.form]);
  const lowest = Math.min(...values);
  const highest = Math.max(...values);
  const span = highest - lowest;
  const margin = span === 0 ? 1 : span * Padding;
  return { lowest: lowest - margin, highest: highest + margin };
}

/** SVG's y grows downward, so the higher figure gets the smaller coordinate. */
function y(value: number, lowest: number, highest: number, height: number): number {
  return height - ((value - lowest) / (highest - lowest)) * height;
}

/** The distance between neighbouring days; a single day sits at the left edge. */
function step(count: number, width: number): number {
  return count === 1 ? 0 : width / (count - 1);
}

function heaviest(days: readonly Day[]): number {
  return days.reduce((max, d) => Math.max(max, d.load), 0);
}

/** .NET's Math.Round default: to the nearest integer, halves to the even neighbour. */
function roundHalfEven(value: number): number {
  const floor = Math.floor(value);
  const fraction = value - floor;
  if (fraction > 0.5) return floor + 1;
  if (fraction < 0.5) return floor;
  return floor % 2 === 0 ? floor : floor + 1;
}

export function viewBox(width: number, height: number): string {
  return `0 0 ${String(width)} ${String(height)}`;
}

/** Fitness, Fatigue and Form as polyline points on one shared scale; nothing for no days. */
export function plot(days: readonly Day[], width: number, height: number): ChartSeries[] {
  if (days.length === 0) return [];
  const { lowest, highest } = band(days);
  const dx = step(days.length, width);
  return Series.map(({ label, cssClass, metric }) => ({
    label,
    cssClass,
    points: days.map((d, i) => `${num(i * dx)},${num(y(d[metric], lowest, highest, height))}`).join(" "),
  }));
}

/** Daily load as bars on their own scale; a rest day draws nothing, and all rest draws no bars. */
export function loadBars(days: readonly Day[], width: number, height: number): LoadBar[] {
  if (days.length === 0) return [];
  const max = heaviest(days);
  if (max <= 0) return [];
  const dx = step(days.length, width);
  const barWidth = Math.max(MinimumBarWidth, width / days.length - BarGap);
  return days.flatMap((d, i) => {
    if (d.load <= 0) return [];
    const barHeight = (d.load / max) * height * BarShareOfPlot;
    return [{ x: num(i * dx - barWidth / 2), y: num(height - barHeight), width: num(barWidth), height: num(barHeight) }];
  });
}

/** Where form's zero sits on the shared metric scale; null when the band does not reach zero. */
export function zeroRule(days: readonly Day[], _width: number, height: number): string | null {
  if (days.length === 0) return null;
  const { lowest, highest } = band(days);
  return lowest > 0 || highest < 0 ? null : num(y(0, lowest, highest, height));
}

/** Evenly spaced day labels, both ends included; every day when there are no more than `count`. */
export function axisTicks(days: readonly Day[], count: number): string[] {
  if (days.length === 0) return [];
  if (count <= 1 || days.length <= count) return days.map((d) => d.day);
  return Array.from({ length: count }, (_, tick) => days[roundHalfEven((tick * (days.length - 1)) / (count - 1))]!.day);
}

/** One readout per day, positioned in percentages of the plot, rendered once and shown by CSS. */
export function hoverSlots(days: readonly Day[]): HoverSlot[] {
  if (days.length === 0) return [];
  const { lowest, highest } = band(days);
  const range = highest - lowest;
  const dx = step(days.length, 100);
  const slotWidth = 100 / days.length;
  const max = heaviest(days);
  const top = (value: number) => num(((highest - value) / range) * 100);

  return days.map((d, index) => {
    const share = max > 0 && d.load > 0 ? (d.load / max) * 100 * BarShareOfPlot : null;
    return {
      day: d.day,
      left: num(index * dx),
      width: num(slotWidth),
      barTop: share === null ? null : num(100 - share),
      barHeight: share === null ? null : num(share),
      load: d.display.load,
      opensLeft: index > days.length * OpensLeftAfter,
      points: Series.map(({ label, cssClass, metric }) => ({
        cssClass,
        label,
        value: d.display[metric],
        top: top(d[metric]),
      })),
    };
  });
}
