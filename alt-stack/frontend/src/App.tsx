// Two routes, chosen by location.pathname with no router library (research R11): `/` is the
// dashboard, and every other path is the not-found page, which US4 (T100–T101) fills in.
import { Dashboard } from "./components/Dashboard";
import type { DashboardApi } from "./types";

export interface AppProps {
  api: DashboardApi;
}

export function App({ api }: AppProps) {
  if (window.location.pathname === "/") return <Dashboard api={api} />;
  return null;
}
