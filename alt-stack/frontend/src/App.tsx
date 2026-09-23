// Routes chosen by location.pathname, with no router library (research R11): `/` is the
// dashboard, `/Error` the error page, and every other path the not-found page.
//
// Each route is loaded lazily, so Vite splits it into its own chunk with its own stylesheet. The
// copied component stylesheets were scoped per component in the reference; here they are global,
// and NotFound.css, ErrorPage.css and Dashboard.css all define .page and .state differently.
// Every route is a full page load, so a page only ever has its own route's rules.
import { lazy, Suspense } from "react";
import type { DashboardApi } from "./types";

const Dashboard = lazy(() => import("./components/Dashboard").then((m) => ({ default: m.Dashboard })));
const ErrorPage = lazy(() => import("./components/ErrorPage").then((m) => ({ default: m.ErrorPage })));
const NotFound = lazy(() => import("./components/NotFound").then((m) => ({ default: m.NotFound })));

export interface AppProps {
  api: DashboardApi;
}

export function App({ api }: AppProps) {
  const path = window.location.pathname;
  return (
    <Suspense fallback={null}>
      {path === "/" ? <Dashboard api={api} /> : path === "/Error" ? <ErrorPage /> : <NotFound />}
    </Suspense>
  );
}
