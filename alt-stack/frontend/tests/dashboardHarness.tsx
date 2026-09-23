// Shared by the Dashboard tests: render it over a controlled API and settle its first reads.
import { act, render } from "@testing-library/react";
import { Dashboard } from "../src/components/Dashboard";
import type { DashboardView, SyncStatusView } from "../src/types";
import { controlledApi } from "./deferred";
import { never } from "./syncStatus";

/** Lets every settled promise's continuation run, so a follow-up call would have been made. */
export async function settle() {
  await act(async () => {
    for (let i = 0; i < 5; i += 1) await new Promise((r) => setTimeout(r, 0));
  });
}

/** The Dashboard after its sync status and view have both answered. */
export async function loaded(view: DashboardView, status: SyncStatusView = never) {
  const c = controlledApi();
  const { container } = render(<Dashboard api={c.api} />);
  await act(async () => {
    c.status[0]?.resolve(status);
    c.dashboard[0]?.resolve(view);
  });
  return { ...c, container };
}

export const content = (container: HTMLElement) => container.querySelector("main.page > section.content");
export const rail = (container: HTMLElement) => container.querySelector("main.page > aside.rail");
