// T095 routing: `/` is the dashboard; every other path is not (research R11). The routes are
// code-split (T101), so each test lets the lazy chunk resolve before it looks.
import { act, cleanup, render, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { App } from "../src/App";
import { controlledApi } from "./deferred";

afterEach(() => {
  cleanup();
  window.history.replaceState(null, "", "/");
});

/** Waits for the route's lazy chunk to render, then lets its effects run. */
async function settle(container: HTMLElement) {
  await waitFor(() => expect(container.firstChild).not.toBeNull());
  await act(async () => {
    for (let i = 0; i < 5; i += 1) await new Promise((r) => setTimeout(r, 0));
  });
}

describe("App", () => {
  it("renders the dashboard at /", async () => {
    window.history.replaceState(null, "", "/");
    const c = controlledApi();
    const { container } = render(<App api={c.api} />);
    await settle(container);

    expect(container.querySelector("main.page > aside.rail")).not.toBeNull();
    expect(c.calls.fetchDashboard).toBe(1);
  });

  it("does not render the dashboard, or read it, anywhere else", async () => {
    window.history.replaceState(null, "", "/nowhere");
    const c = controlledApi();
    const { container } = render(<App api={c.api} />);
    await settle(container);

    expect(container.querySelector("aside.rail")).toBeNull();
    expect(c.total()).toBe(0);
  });
});
