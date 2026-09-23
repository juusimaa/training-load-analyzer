// Port of Components/Dashboard/SyncPanel.razor: the sync button and whatever the last attempt
// has to say (006 FR-009, FR-010). The message arrives from the API as SyncMessage.For wrote it.
import type { SyncStatusView } from "../types";
import "./SyncPanel.css";

export interface SyncPanelProps {
  status: SyncStatusView;
  onSync: () => void;
}

export function SyncPanel({ status, onSync }: SyncPanelProps) {
  return (
    <div className="sync-panel">
      {/* A real <button> carrying class="sync": the hook tests bind to, with its disabled state. */}
      <button type="button" className="btn btn-primary btn-block sync" disabled={status.isRunning} onClick={onSync}>
        {status.isRunning ? "Syncing…" : "Sync Activities"}
      </button>
      {status.message !== "" && <p className="sync-message">{status.message}</p>}
      {status.needsConnection && (
        <a className="btn btn-primary btn-block connect" href="/connect">
          Connect Strava
        </a>
      )}
      {status.lastChecked !== null && !status.isRunning && <p className="sync-when">Last checked {status.lastChecked}</p>}
    </div>
  );
}
