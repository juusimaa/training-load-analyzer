// Port of Components/Pages/Dashboard.razor. The one stateful component: it owns the fetch
// lifecycle, the chosen window and the sync-in-flight flag (data-model.md §6).
//
// - The view is read once on load and re-read, never patched, after a sync (006 FR-009, C80).
// - The window is a client-side slice: choosing one issues no request (008 research R6).
// - Pressing Sync shows the running state at once, before the request answers (research R3(2)).
// - A sync already running on load shows as running until reload: there is no polling (R9).
import { useEffect, useRef, useState } from "react";
import type { DashboardApi, DashboardView, SyncStatusView } from "../types";
import { isoWeek, localDay } from "../isoWeek";
import { DEFAULT_WINDOW, windowed } from "../window";
import { MetricRow } from "./MetricRow";
import { MetricsChart } from "./MetricsChart";
import { Rail } from "./Rail";
import { RecentActivities } from "./RecentActivities";
import "./Dashboard.css";

export interface DashboardProps {
  api: DashboardApi;
}

const Never: SyncStatusView = { isRunning: false, message: "", needsConnection: false, lastChecked: null };

/** What SyncCoordinator.Status reports while a sync runs, shown by the tab that started it. */
const Running: SyncStatusView = { isRunning: true, message: "Syncing activities…", needsConnection: false, lastChecked: null };

export function Dashboard({ api }: DashboardProps) {
  // The last good view, kept for the rail when a later read fails (dashboard-ui.md §1).
  const [view, setView] = useState<DashboardView | null>(null);
  const [failed, setFailed] = useState(false);
  const [windowDays, setWindowDays] = useState(DEFAULT_WINDOW);
  const [syncStatus, setSyncStatus] = useState<SyncStatusView>(Never);
  const syncStarted = useRef(false);

  useEffect(() => {
    document.title = "Training Load";
  }, []);

  useEffect(() => {
    let current = true;
    api.fetchSyncStatus().then(
      (status) => {
        // A sync this tab started has the newer word.
        if (current && !syncStarted.current) setSyncStatus(status);
      },
      () => {},
    );
    api.fetchDashboard().then(
      (next) => current && received(next),
      () => current && setFailed(true),
    );
    return () => {
      current = false;
    };
  }, [api]);

  async function runSync() {
    syncStarted.current = true;
    const before = syncStatus;
    setSyncStatus(Running);
    let finished: SyncStatusView;
    try {
      finished = await api.postSync();
    } catch {
      // A failed request is the unavailable notice, never stale figures (009 FR-016). The panel
      // returns to what it said before: there are no words for a failed sync request.
      setSyncStatus(before);
      setFailed(true);
      return;
    }
    setSyncStatus(finished);
    api.fetchDashboard().then(received, () => setFailed(true));
  }

  function received(next: DashboardView) {
    setView(next);
    setFailed(false);
  }

  // Before any view arrives, the rail falls back to the browser's clock, as the reference falls
  // back to DateTime.Now, and to `—` for the heart rate rather than its `0` (research R4).
  const now = new Date();

  return (
    <main className="page">
      <Rail
        asOf={view?.asOf ?? localDay(now)}
        isoWeek={view?.isoWeek ?? isoWeek(now)}
        maximumHeartRate={view?.maximumHeartRate ?? null}
        windowDays={windowDays}
        onWindowChange={setWindowDays}
        syncStatus={syncStatus}
        onSync={runSync}
      />{" "}
      <section className="content">
        <Content view={view} failed={failed} windowDays={windowDays} />
      </section>
    </main>
  );
}

/**
 * The content column, one branch per state of Dashboard.razor. The {" "} separators stand where
 * the Razor markup has whitespace between elements, so the text matches the goldens exactly.
 */
function Content({ view, failed, windowDays }: { view: DashboardView | null; failed: boolean; windowDays: number }) {
  if (failed || view?.isUnavailable) {
    // Told, rather than shown zeroes or stale figures (C87, 009 FR-016).
    return (
      <div className="state state-unavailable">
        <h2>Data unavailable</h2>{" "}
        <p>
          Your stored training history could not be read. Nothing has been lost — try again, and if it persists, the
          application log says why.
        </p>
      </div>
    );
  }

  if (view === null) {
    return (
      <div className="state">
        <p>Reading your training history…</p>
      </div>
    );
  }

  if (!view.hasActivities) {
    return view.isStravaConnected ? (
      <div className="state">
        <h2>No activities recorded</h2>{" "}
        <p>Your Strava account is connected, but nothing has been imported yet. Sync to bring your training in.</p>
      </div>
    ) : (
      <div className="state">
        <h2>No activities recorded</h2>
        <p>Connect your Strava account to bring your training in.</p>{" "}
        <a className="btn btn-primary connect" href="/connect">
          Connect Strava
        </a>
      </div>
    );
  }

  return (
    <>
      <MetricRow current={view.current} week={view.week} />
      <div className="block">
        <MetricsChart days={windowed(view.days, windowDays)} hasEnoughHistory={view.hasEnoughHistoryForChart} windowDays={windowDays} />
      </div>
      <div className="block">
        <RecentActivities activities={view.recent} />
      </div>
    </>
  );
}
