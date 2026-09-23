// T103: ContrastRatioTests.cs, PaletteContrastTests.cs and PaletteSlotTests.cs, ported rule for
// rule, over the copied stylesheets (008 FR-015, FR-020–FR-023, SC-005).
import { basename } from "node:path";
import { describe, expect, it } from "vitest";
import { contrastBetween } from "./contrast";
import { BroadsheetTokens, declaration, Stylesheet, StylesheetPath, stylesheetExists, type TokenSet } from "./tokens";

/** xUnit's Assert.Equal(expected, actual, precision): both rounded to that many places. */
const places = (value: number, precision: number) => Math.round(value * 10 ** precision) / 10 ** precision;

describe("ContrastRatio (the instrument)", () => {
  it("black against white is the maximum ratio", () => {
    expect(places(contrastBetween("#000000", "#FFFFFF"), 2)).toBe(21);
  });

  it("a colour against itself is one", () => {
    expect(places(contrastBetween("#00639B", "#00639B"), 2)).toBe(1);
  });

  it("the light primary against its background measures as planned", () => {
    expect(places(contrastBetween("#00639B", "#FDFCFF"), 2)).toBe(6.31);
  });

  it("the order of the two colours does not matter", () => {
    expect(places(contrastBetween("#1A1C1E", "#FDFCFF"), 10)).toBe(places(contrastBetween("#FDFCFF", "#1A1C1E"), 10));
  });

  it("a three-digit hex expands to its six-digit form", () => {
    expect(places(contrastBetween("#000", "#FFF"), 10)).toBe(places(contrastBetween("#000000", "#FFFFFF"), 10));
  });
});

const schemes = ["light", "dark"] as const;
const tokensFor = (scheme: (typeof schemes)[number]) => (scheme === "dark" ? BroadsheetTokens.dark : BroadsheetTokens.light);

/** A declared value, or a failure naming the rule. */
function rule(stylesheet: string, selector: string, property: string): string {
  const declared = declaration(stylesheet, selector, property);
  expect(
    declared,
    `'${selector} { ${property} }' is not declared in ${basename(stylesheet)}, so nothing pins the colour this pairing is supposed to measure.`,
  ).not.toBeNull();
  return declared!;
}

/** A bare token name is shorthand for reading it; anything else is an expression. */
const expression = (value: string) => (value.startsWith("--") ? `var(${value})` : value);

function assertAtLeast(minimum: number, tokens: TokenSet, foreground: string, background: string, what: string) {
  const ground = tokens.resolve(expression(background));
  const ink = tokens.resolve(expression(foreground), expression(background));
  const measured = contrastBetween(ink, ground);
  expect(
    measured,
    `In the ${tokens.scheme} scheme, ${what} measures ${String(places(measured, 2))}:1 (${ink} on ${ground}), below the required ${String(minimum)}:1.`,
  ).toBeGreaterThanOrEqual(minimum);
}

describe.each(schemes)("Palette contrast, %s scheme", (scheme) => {
  const tokens = tokensFor(scheme);

  it("body text clears 4.5:1 on the page and on a surface", () => {
    assertAtLeast(4.5, tokens, "--color-text", "--color-bg", "body text on the page");
    assertAtLeast(4.5, tokens, "--color-text", "--color-surface", "body text on a surface");
  });

  it("the primary button label clears 4.5:1 in every state", () => {
    for (const [selector, state] of [
      [".btn-primary", "at rest"],
      [".btn-primary:hover", "on hover"],
      [".btn-primary:active", "when pressed"],
    ] as const) {
      const background = rule(Stylesheet.Broadsheet, selector, "background");
      const label = rule(Stylesheet.Broadsheet, ".btn-primary", "color");
      assertAtLeast(4.5, tokens, label, background, `the primary button's label ${state}`);
    }
  });

  it("accent text clears 4.5:1 against the page", () => {
    assertAtLeast(4.5, tokens, rule(Stylesheet.Broadsheet, "a", "color"), "--color-bg", "a link");
    assertAtLeast(4.5, tokens, rule(Stylesheet.Broadsheet, ".btn-ghost", "color"), "--color-bg", "a ghost button");
    assertAtLeast(4.5, tokens, rule(Stylesheet.Broadsheet, ".tag-outline", "color"), "--color-bg", "an outline tag");
  });

  it("muted text clears 4.5:1 against the page", () => {
    assertAtLeast(4.5, tokens, rule(Stylesheet.Broadsheet, ".table th", "color"), "--color-bg", "a table heading");
    assertAtLeast(4.5, tokens, rule(Stylesheet.Broadsheet, ".text-muted", "color"), "--color-bg", "muted text");
    assertAtLeast(4.5, tokens, rule(Stylesheet.Broadsheet, "figcaption", "color"), "--color-bg", "a caption");
  });

  it("the display figures clear 3:1 against the page", () => {
    assertAtLeast(3, tokens, "--color-accent-700", "--color-bg", "the fitness figure");
    assertAtLeast(3, tokens, "--color-accent-2-700", "--color-bg", "the fatigue figure");
    assertAtLeast(3, tokens, "--color-text", "--color-bg", "the form and week figures");
  });

  it("every chart line clears 3:1 against the plot", () => {
    assertAtLeast(3, tokens, rule(Stylesheet.MetricsChart, ".series-fitness", "stroke"), "--color-bg", "the fitness line");
    assertAtLeast(3, tokens, rule(Stylesheet.MetricsChart, ".series-fatigue", "stroke"), "--color-bg", "the fatigue line");
    assertAtLeast(3, tokens, rule(Stylesheet.MetricsChart, ".series-form", "stroke"), "--color-bg", "the form line");
    assertAtLeast(3, tokens, rule(Stylesheet.MetricsChart, ".zero-rule", "stroke"), "--color-bg", "the zero rule");
  });

  it("the chart's own text clears 4.5:1", () => {
    assertAtLeast(4.5, tokens, rule(Stylesheet.MetricsChart, ".chart-axis", "color"), "--color-bg", "the date axis");
    assertAtLeast(4.5, tokens, rule(Stylesheet.MetricsChart, ".legend-form", "color"), "--color-bg", "the Form legend label");
  });

  it("the hover readout sits on ground this audit already measures", () => {
    const ground = rule(Stylesheet.MetricsChart, ".readout", "background");

    expect(tokens.resolve(ground)).toBe(tokens.resolve("var(--color-bg)"));
    assertAtLeast(4.5, tokens, "--color-text", ground, "a readout figure");
    assertAtLeast(4.5, tokens, rule(Stylesheet.MetricsChart, ".legend-form", "color"), ground, "a readout label");
  });

  it("the load bars are a backdrop and are excluded by name (Amendment 1(b))", () => {
    const bars = declaration(Stylesheet.MetricsChart, ".load-bar", "fill");
    expect(bars, "The load-bar exemption names a rule that no longer exists.").not.toBeNull();

    const measured = contrastBetween(tokens.resolve(bars!), tokens.resolve("var(--color-bg)"));
    expect(
      measured,
      `In the ${scheme} scheme the load bars now measure ${String(places(measured, 2))}:1 against the page; if darkened deliberately, withdraw Amendment 1(b) rather than update this exclusion.`,
    ).toBeLessThan(3);
  });
});

/** Every token group in 008's data model, as PaletteSlotTests lists them. */
const RequiredTokens = [
  "--color-bg", "--color-surface", "--color-text", "--color-divider", "--color-scrim",
  "--color-accent", "--color-accent-2",
  "--color-neutral-100", "--color-neutral-200", "--color-neutral-300",
  "--color-neutral-400", "--color-neutral-500", "--color-neutral-600",
  "--color-neutral-700", "--color-neutral-800", "--color-neutral-900",
  "--color-accent-100", "--color-accent-200", "--color-accent-300",
  "--color-accent-400", "--color-accent-500", "--color-accent-600",
  "--color-accent-700", "--color-accent-800", "--color-accent-900",
  "--color-accent-2-100", "--color-accent-2-200", "--color-accent-2-300",
  "--color-accent-2-400", "--color-accent-2-500", "--color-accent-2-600",
  "--color-accent-2-700", "--color-accent-2-800", "--color-accent-2-900",
  "--font-heading", "--font-heading-weight", "--font-body",
  "--space-1", "--space-2", "--space-3", "--space-4", "--space-6", "--space-8",
  "--radius-sm", "--radius-md", "--radius-lg",
  "--shadow-sm", "--shadow-md", "--shadow-lg",
];

const ColourTokens = RequiredTokens.filter((t) => t.startsWith("--color-") || t.startsWith("--shadow-"));

function assertTheSheetExists() {
  expect(stylesheetExists(), `The copied stylesheet is missing: ${StylesheetPath}`).toBe(true);
}

function assertComplete(tokens: TokenSet) {
  const missing = RequiredTokens.filter((token) => !tokens.declares(token));
  expect(missing, `The ${tokens.scheme} scheme does not declare: ${missing.join(", ")}`).toEqual([]);
}

describe("Palette slots", () => {
  it("the light scheme declares every required token", () => {
    assertTheSheetExists();
    assertComplete(BroadsheetTokens.light);
  });

  it("the dark scheme declares every required token", () => {
    assertTheSheetExists();
    assertComplete(BroadsheetTokens.dark);
  });

  it("every colour is re-decided by the dark scheme rather than inherited", () => {
    assertTheSheetExists();
    const overrides = BroadsheetTokens.darkOverrides;
    const inherited = ColourTokens.filter((token) => !overrides.has(token));

    expect(inherited, `These colours fall through from the light block into the dark scheme: ${inherited.join(", ")}`).toEqual([]);
  });

  it("no colour carries the same value in both schemes", () => {
    assertTheSheetExists();
    const light = BroadsheetTokens.light;
    const dark = BroadsheetTokens.dark;

    for (const token of ColourTokens) {
      expect(light.raw(token).toLowerCase(), `'${token}' is ${light.raw(token)} in both schemes.`).not.toBe(dark.raw(token).toLowerCase());
    }
  });
});
