// T100: ReconnectModalTests.cs ported as an exclusion (Amendment 1(c)), plus the not-found page,
// the /Error page and the error boundary that stands in for #blazor-error-ui (research R4).
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { App } from "../src/App";
import { ErrorBoundary } from "../src/components/ErrorBoundary";
import { controlledApi } from "./deferred";
import { loadHistory, loadSurfaces, normalisedText } from "./golden";
import { never } from "./syncStatus";

afterEach(() => {
  cleanup();
  window.history.replaceState(null, "", "/");
  document.title = "";
});

const surfaces = loadSurfaces();

async function at(path: string) {
  window.history.replaceState(null, "", path);
  const c = controlledApi();
  const rendered = render(<App api={c.api} />);
  // The routes are code-split, so wait for the lazy chunk to render, then let its effects run.
  await waitFor(() => expect(rendered.container.firstChild).not.toBeNull());
  await act(async () => {
    for (let i = 0; i < 5; i += 1) await new Promise((r) => setTimeout(r, 0));
  });
  return { ...c, container: rendered.container };
}

function Throws(): never {
  throw new Error("render failed");
}

describe("Not found", () => {
  it.each(["/nowhere", "/not-found", "/dashboard/extra"])("renders the not-found page at %s", async (path) => {
    const { container, total } = await at(path);
    const page = container.querySelector("main.page");

    expect(page?.querySelector("h1")?.textContent).toBe("Not Found");
    expect(page?.querySelector(".state > p")?.textContent).toBe("Sorry, the content you are looking for does not exist.");
    expect(normalisedText(container)).toBe(surfaces.notFound);
    expect(document.title).toBe("Not found");
    expect(total()).toBe(0);
  });
});

describe("Error page", () => {
  it("renders the reference's words at /Error, with no Request ID and no Development Mode", async () => {
    const { container, total } = await at("/Error");
    const page = container.querySelector("main.page");

    expect(page?.querySelector("h1.alarm")?.textContent).toBe("Error.");
    expect(page?.querySelector(".state.state-alarm > h2.alarm")?.textContent).toBe(
      "An error occurred while processing your request.",
    );
    expect(normalisedText(container)).toBe(surfaces.error);
    expect(normalisedText(container)).not.toContain("Request ID");
    expect(normalisedText(container)).not.toContain("Development Mode");
    expect(document.title).toBe("Error");
    expect(total()).toBe(0);
  });
});

describe("ErrorBoundary", () => {
  it("renders its children when nothing fails", () => {
    const { container } = render(
      <ErrorBoundary>
        <p>fine</p>
      </ErrorBoundary>,
    );

    expect(container.textContent).toBe("fine");
    expect(container.querySelector(".error-ui")).toBeNull();
  });

  it("shows the unhandled-error notice, with Reload and a dismiss control, when a child throws", () => {
    let reloads = 0;
    const { container } = render(
      <ErrorBoundary reload={() => (reloads += 1)}>
        <Throws />
      </ErrorBoundary>,
    );
    const notice = container.querySelector(".error-ui.error-ui-shown");

    expect(notice).not.toBeNull();
    expect(normalisedText(notice)).toBe("An unhandled error has occurred. Reload 🗙");
    const reload = notice?.querySelector("a.reload");
    expect(reload?.textContent).toBe("Reload");
    expect(reload?.getAttribute("href")).toBe(".");
    expect(notice?.querySelector("span.dismiss")?.textContent).toBe("🗙");

    fireEvent.click(reload!);
    expect(reloads).toBe(1);
  });

  it("hides the notice when it is dismissed", () => {
    const { container } = render(
      <ErrorBoundary reload={() => {}}>
        <Throws />
      </ErrorBoundary>,
    );
    const dismiss = container.querySelector(".error-ui span.dismiss");
    expect(dismiss).not.toBeNull();

    fireEvent.click(dismiss!);

    expect(container.querySelector(".error-ui.error-ui-shown")).toBeNull();
  });
});

describe("The circuit-reconnect modal is not ported (Amendment 1(c))", () => {
  it.each(["/", "/nowhere", "/Error"])("no surface at %s mentions rejoining the server", async (path) => {
    const c = await at(path);
    await act(async () => {
      c.status[0]?.resolve(never);
      c.dashboard[0]?.resolve(loadHistory("H3f").view);
    });

    expect(document.body.textContent).not.toMatch(/Rejoin|rejoin|Retry|Resume|paused by the server/);
    expect(document.querySelector("#components-reconnect-modal")).toBeNull();
  });

  it("nor does the error boundary", () => {
    render(
      <ErrorBoundary reload={() => {}}>
        <Throws />
      </ErrorBoundary>,
    );

    expect(document.body.textContent).not.toMatch(/Rejoin|rejoin/);
  });
});

describe("Routing is by path only", () => {
  it("renders the dashboard at /, titled Training Load", async () => {
    const c = await at("/");

    expect(c.container.querySelector("main.page > aside.rail")).not.toBeNull();
    expect(c.calls.fetchDashboard).toBe(1);
    expect(document.title).toBe("Training Load");
    expect(screen.queryByText("Not Found")).toBeNull();
  });
});
