// No StrictMode: in development it runs every effect twice, which would double the page's reads
// and make the "one request on load" behaviour impossible to check in the Network tab (T097).
// The boundary wraps every route, as MainLayout's #blazor-error-ui sits under every page.
import { createRoot } from "react-dom/client";
import { createApi } from "./api";
import { App } from "./App";
import { ErrorBoundary } from "./components/ErrorBoundary";
import "./theme/broadsheet.css";
import "./theme/app.css";
import "./theme/layout.css";

createRoot(document.getElementById("root")!).render(
  <ErrorBoundary>
    <App api={createApi()} />
  </ErrorBoundary>,
);
