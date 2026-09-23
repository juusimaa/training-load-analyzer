// The JSON the backend serves, field for field (contracts/http-api.md §2–§3, data-model.md §6).
// Every value the page displays is a string, already formatted by the backend (research R2);
// numbers exist only for chart geometry (research R7).

export type Provenance = "measured" | "estimated";

export interface Figure {
  value: string;
  qualifiers: string[];
}

export interface Current {
  fitness: Figure;
  fatigue: Figure;
  form: Figure;
}

export interface Trend {
  change: string;
  percent: string;
  judgement: string;
}

export interface Week {
  points: string;
  trend: Trend | null;
}

export interface DayDisplay {
  fitness: string;
  fatigue: string;
  form: string;
  load: string;
}

export interface Day {
  day: string;
  fitness: number;
  fatigue: number;
  form: number;
  load: number;
  display: DayDisplay;
}

export interface Recent {
  day: string;
  type: string;
  movingTime: string;
  provenance: Provenance;
  load: string;
}

export interface DashboardView {
  asOf: string;
  isoWeek: string;
  maximumHeartRate: string;
  isStravaConnected: boolean;
  isUnavailable: boolean;
  hasActivities: boolean;
  hasEnoughHistoryForChart: boolean;
  current: Current | null;
  week: Week;
  days: Day[];
  recent: Recent[];
}

export interface SyncStatusView {
  isRunning: boolean;
  message: string;
  needsConnection: boolean;
  lastChecked: string | null;
}

export interface DashboardApi {
  fetchDashboard(): Promise<DashboardView>;
  fetchSyncStatus(): Promise<SyncStatusView>;
  postSync(): Promise<SyncStatusView>;
}
