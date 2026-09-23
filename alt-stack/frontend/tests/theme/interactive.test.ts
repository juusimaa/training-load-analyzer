// T105: InteractiveControlTests.cs, ported over the copied stylesheets (008 FR-016, FR-017,
// FR-024). The reference's fourth control, the reconnect dialog's buttons, is not ported
// (Amendment 1(c)): ReconnectModal.razor.css was not copied and no such control exists here.
import { describe, expect, it } from "vitest";
import { declaration, mediaQuery, Stylesheet } from "./tokens";

describe("Interactive controls", () => {
  it.each([
    [Stylesheet.Dashboard, ".seg-opt", "the window options"],
    [Stylesheet.Dashboard, ".connect", "the connect action"],
    [Stylesheet.SyncPanel, ".sync-panel .btn", "the sync and reconnect actions"],
  ])("every control clears 48px: %s %s", (stylesheet, selector, what) => {
    const declared = declaration(stylesheet, selector, "min-height");

    expect(declared, `${what} declare min-height: ${declared ?? "nothing"}, not the 48px FR-017 requires.`).toBe("48px");
  });

  it("keyboard focus draws a visible ring", () => {
    const outline = declaration(Stylesheet.Broadsheet, ":focus-visible", "outline");

    expect(outline?.trim() || null, "Nothing draws a focus ring (FR-016).").not.toBeNull();
    expect(declaration(Stylesheet.Broadsheet, ":focus", "outline")).toBe("none");
    expect(outline).toContain("var(--color-accent)");
  });

  it("the reduced-motion reset is still in place", () => {
    const reset = mediaQuery(Stylesheet.App, "prefers-reduced-motion: reduce");

    expect(reset, "The global reduced-motion reset has been removed (007 FR-024).").not.toBeNull();
    expect(reset).toContain("animation-duration");
    expect(reset).toContain("transition-duration");
  });
});
