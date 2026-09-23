// T105: ResponsiveRulesTests.cs, ported over the copied stylesheets (008 FR-011, FR-013, FR-014),
// plus T105's own additions: nothing fixed-width overflows 320px, and light/dark is a media query
// with no script (008 FR-021, 009 US4 scenario 3).
import { readdirSync, readFileSync } from "node:fs";
import { join, relative } from "node:path";
import { describe, expect, it } from "vitest";
import { declaration, declarationIn, mediaQuery, Stylesheet, underFrontend } from "./tokens";

/** The single breakpoint, stated once so a drifting copy is obvious. */
const Breakpoint = "max-width: 60rem";

function narrow(stylesheet: string): string {
  const rules = mediaQuery(stylesheet, Breakpoint);
  expect(rules, `${stylesheet} declares no '${Breakpoint}' media query, so nothing makes it stack.`).not.toBeNull();
  return rules!;
}

function files(directory: string): string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) =>
    entry.isDirectory() ? files(join(directory, entry.name)) : [join(directory, entry.name)],
  );
}

const src = underFrontend("src");
const stylesheets = files(src).filter((f) => f.endsWith(".css"));
const sources = files(src).filter((f) => f.endsWith(".ts") || f.endsWith(".tsx"));

describe("Responsive rules (ported)", () => {
  it("below the breakpoint the page stacks and the rail stops sticking", () => {
    const rules = narrow(Stylesheet.Dashboard);

    expect(declarationIn(rules, ".page", "grid-template-columns")).toBe("1fr");
    expect(declarationIn(rules, ".rail", "position")).toBe("static");
  });

  it("below the breakpoint the metric row becomes two columns", () => {
    expect(declarationIn(narrow(Stylesheet.MetricRow), ".metrics", "grid-template-columns")).toBe("repeat(2, 1fr)");
  });

  it("on a wide display the page and its prose are both capped", () => {
    expect(declaration(Stylesheet.Dashboard, ".page", "max-width")).toBe("1240px");
    expect(declaration(Stylesheet.Dashboard, ".state", "max-width")?.trim() || null).not.toBeNull();
  });

  it("the chart scales to its container", () => {
    expect(declaration(Stylesheet.MetricsChart, ".plot", "width")).toBe("100%");
  });
});

describe("Nothing overflows a 320px screen", () => {
  const withoutComments = (css: string) => css.replace(/\/\*[\s\S]*?\*\//g, "");

  it("no width, min-width or flex-basis is fixed wider than 320px", () => {
    const offenders: string[] = [];
    for (const file of stylesheets) {
      for (const m of withoutComments(readFileSync(file, "utf-8")).matchAll(/(?:^|[;{\s])((?:min-)?width|flex-basis)\s*:\s*(\d+(?:\.\d+)?)px/g)) {
        if (Number(m[2]) > 320) offenders.push(`${relative(src, file)}: ${m[1]!}: ${m[2]!}px`);
      }
    }
    expect(offenders).toEqual([]);
  });

  it("every fixed-width grid track is released below the breakpoint", () => {
    let checked = 0;
    for (const file of stylesheets) {
      const css = withoutComments(readFileSync(file, "utf-8"));
      for (const rule of css.matchAll(/([^{}]+)\{([^{}]*grid-template-columns\s*:[^;]*\d+px[^{}]*)\}/g)) {
        const selector = rule[1]!.trim();
        if (selector.startsWith("@")) continue;
        const released = mediaQuery(file, Breakpoint);
        expect(released, `${relative(src, file)} '${selector}' has a px grid track and no narrow rule`).not.toBeNull();
        expect(declarationIn(released!, selector, "grid-template-columns"), `${selector} in ${relative(src, file)}`).not.toMatch(/\d+px/);
        checked += 1;
      }
    }
    // The rail's 250px track is the one this exists for.
    expect(checked).toBeGreaterThanOrEqual(1);
  });
});

describe("Light and dark follow the device, with no script", () => {
  it("the dark palette is a prefers-color-scheme media query in the copied theme", () => {
    expect(mediaQuery(Stylesheet.Broadsheet, "prefers-color-scheme: dark")).toContain(":root");
  });

  it("no source file reads or sets the appearance", () => {
    for (const file of sources) {
      const source = readFileSync(file, "utf-8");
      expect(source, relative(src, file)).not.toMatch(/prefers-color-scheme|matchMedia|data-theme|colorScheme|color-scheme/);
    }
  });

  it("index.html has no inline script, so the right appearance applies from the first paint", () => {
    const html = readFileSync(underFrontend("index.html"), "utf-8");
    const scripts = [...html.matchAll(/<script\b([^>]*)>([\s\S]*?)<\/script>/g)];

    expect(scripts).toHaveLength(1);
    expect(scripts[0]![1]).toContain('src="/src/main.tsx"');
    expect(scripts[0]![2]!.trim()).toBe("");
  });
});
