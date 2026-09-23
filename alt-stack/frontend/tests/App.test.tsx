// T095 routing: `/` is the dashboard; every other path is the not-found page (filled in by US4,
// T100–T101). No router library: the path is read from location.pathname (research R11).
import { cleanup, render } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { App } from "../src/App";
import { controlledApi } from "./deferred";

afterEach(() => {
  cleanup();
  window.history.replaceState(null, "", "/");
});

describe("App", () => {
  it("renders the dashboard at /", () => {
    window.history.replaceState(null, "", "/");
    const c = controlledApi();
    const { container } = render(<App api={c.api} />);

    expect(container.querySelector("main.page > aside.rail")).not.toBeNull();
    expect(c.calls.fetchDashboard).toBe(1);
  });

  it("does not render the dashboard, or read it, anywhere else", () => {
    window.history.replaceState(null, "", "/nowhere");
    const c = controlledApi();
    const { container } = render(<App api={c.api} />);

    expect(container.querySelector("aside.rail")).toBeNull();
    expect(c.total()).toBe(0);
  });
});
