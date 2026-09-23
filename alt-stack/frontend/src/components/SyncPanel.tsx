import type { SyncStatusView } from "../types";

export interface SyncPanelProps {
  status: SyncStatusView;
  onSync: () => void;
}

export function SyncPanel(_props: SyncPanelProps) {
  return null;
}
