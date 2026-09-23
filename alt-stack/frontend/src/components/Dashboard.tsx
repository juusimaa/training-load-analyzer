// Port of Components/Pages/Dashboard.razor. The one stateful component: it owns the fetch
// lifecycle, the chosen window and the sync-in-flight flag (data-model.md §6).
//
// - The view is read once on load and re-read, never patched, after a sync (006 FR-009, C80).
// - The window is a client-side slice: choosing one issues no request (008 research R6).
// - Pressing Sync shows the running state at once, before the request answers (research R3(2)).
// - A sync already running on load shows as running until reload: there is no polling (R9).
import { useEffect, useRef, useState } from "react";
import type { DashboardApi, DashboardView, SyncStatusView } from "../types";
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
  const [view, setView] = useState<DashboardView | null>(null);
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
    // A failed read leaves the loading state; US4 (T099a) adds the unavailable notice.
    api.fetchDashboard().then((next) => current && setView(next), () => {});
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
      setSyncStatus(before);
      return;
    }
    setSyncStatus(finished);
    api.fetchDashboard().then(setView, () => {});
  }

  return (
    <main className="page">
      <Rail
        asOf={view?.asOf ?? ""}
        isoWeek={view?.isoWeek ?? ""}
        maximumHeartRate={view?.maximumHeartRate ?? ""}
        windowDays={windowDays}
        onWindowChange={setWindowDays}
        syncStatus={syncStatus}
        onSync={runSync}
      />{" "}
      <section className="content">
        {view === null ? (
          <div className="state">
            <p>Reading your training history…</p>
          </div>
        ) : view.isUnavailable || !view.hasActivities ? null : (
          // The unavailable and empty states are US4 (T098–T099a).
          <>
            <MetricRow current={view.current} week={view.week} />
            <div className="block">
              <MetricsChart
                days={windowed(view.days, windowDays)}
                hasEnoughHistory={view.hasEnoughHistoryForChart}
                windowDays={windowDays}
              />
            </div>
            <div className="block">
              <RecentActivities activities={view.recent} />
            </div>
          </>
        )}
      </section>
    </main>
  );
}
