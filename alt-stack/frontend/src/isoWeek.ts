// The loading rail's as-of day and ISO week, from the browser's local date: the reference's
// DateTime.Now fallback (dashboard-ui.md §1). Only the local calendar day is read from the Date;
// the arithmetic runs on a UTC date built from it, so the process time zone cannot move a day
// across a week boundary. No Intl: the output is fixed-format text.

const pad = (n: number) => String(n).padStart(2, "0");
const dayMs = 24 * 60 * 60 * 1000;

/** The local calendar day as YYYY-MM-DD (Display.Day's format). */
export function localDay(date: Date): string {
  return `${String(date.getFullYear())}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/** The ISO-8601 week designation of the local calendar day, as 2026-W38 (DashboardViewBuilder.Designation). */
export function isoWeek(date: Date): string {
  const day = Date.UTC(date.getFullYear(), date.getMonth(), date.getDate());
  const weekday = new Date(day).getUTCDay() || 7; // Monday 1 … Sunday 7
  // The week belongs to the year its Thursday falls in.
  const thursday = new Date(day + (4 - weekday) * dayMs);
  const year = thursday.getUTCFullYear();
  const week = Math.floor((thursday.getTime() - Date.UTC(year, 0, 1)) / dayMs / 7) + 1;
  return `${String(year)}-W${pad(week)}`;
}
