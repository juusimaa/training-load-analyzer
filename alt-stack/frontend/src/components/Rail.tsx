// Port of the rail in Components/Pages/Dashboard.razor: every control lives here (008 FR-003).
// A pure function of its props; the Dashboard owns the chosen window and the sync state.
//
// The {" "} separators stand where the Razor markup has whitespace between elements, so the
// rendered text matches the reference's exactly (009 SC-002).
import type { SyncStatusView } from "../types";
import { WINDOWS } from "../window";
import { SyncPanel } from "./SyncPanel";
import "./Dashboard.css";

export interface RailProps {
  asOf: string;
  isoWeek: string;
  /** Null before any view has arrived: the rail then shows `—`, never `0 bpm` (research R4). */
  maximumHeartRate: string | null;
  windowDays: number;
  onWindowChange: (days: number) => void;
  syncStatus: SyncStatusView;
  onSync: () => void;
}

export function Rail({ asOf, isoWeek, maximumHeartRate, windowDays, onWindowChange, syncStatus, onSync }: RailProps) {
  return (
    <aside className="rail">
      {/* The nameplate, and the page's one h1. */}
      <h1 className="masthead">
        <span className="rule"></span>{" "}
        <span className="wordmark">Training Load</span>
      </h1>{" "}
      <div className="rail-block">
        <h2 className="kicker">As of</h2>{" "}
        <div className="rail-value">{asOf}</div>{" "}
        <div className="rail-note">ISO week {isoWeek}</div>
      </div>{" "}
      <div className="rail-block">
        <h2 className="kicker">Window</h2>{" "}
        <div className="seg" role="radiogroup" aria-label="Chart window">
          {WINDOWS.map((days) => (
            <label key={days} className="seg-opt">
              <input
                type="radio"
                name="window"
                value={days}
                checked={windowDays === days}
                onChange={() => onWindowChange(days)}
              />{" "}
              {days} days{" "}
            </label>
          ))}
        </div>
      </div>{" "}
      <div className="rail-block">
        <h2 className="kicker">Strava</h2>{" "}
        <SyncPanel status={syncStatus} onSync={onSync} />
      </div>{" "}
      <div className="rail-block">
        <h2 className="kicker">Max heart rate</h2>{" "}
        <div className="rail-value">{maximumHeartRate === null ? "—" : `${maximumHeartRate} bpm`}</div>{" "}
        <div className="rail-note">from configuration</div>
      </div>
    </aside>
  );
}
