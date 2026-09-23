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

export function viewBox(_width: number, _height: number): string {
  return "";
}

export function plot(_days: readonly Day[], _width: number, _height: number): ChartSeries[] {
  return [];
}

export function loadBars(_days: readonly Day[], _width: number, _height: number): LoadBar[] {
  return [];
}

export function zeroRule(_days: readonly Day[], _width: number, _height: number): string | null {
  return null;
}

export function axisTicks(_days: readonly Day[], _count: number): string[] {
  return [];
}

export function hoverSlots(_days: readonly Day[]): HoverSlot[] {
  return [];
}
