import type { SyncStatusView } from "../types";

export interface RailProps {
  asOf: string;
  isoWeek: string;
  maximumHeartRate: string;
  windowDays: number;
  onWindowChange: (days: number) => void;
  syncStatus: SyncStatusView;
  onSync: () => void;
}

export function Rail(_props: RailProps) {
  return null;
}
