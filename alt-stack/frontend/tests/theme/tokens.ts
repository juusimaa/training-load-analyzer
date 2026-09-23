// Port of tests/TrainingLoadAnalyzer.Web.Tests/Theme/BroadsheetTokens.cs: the design tokens read
// out of the stylesheet the application actually ships (the copied src/theme/broadsheet.css), and
// a small reader for declared property values in the copied component stylesheets.
import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const frontend = join(dirname(fileURLToPath(import.meta.url)), "..", "..");

/** A path under alt-stack/frontend/. */
export function underFrontend(...segments: string[]): string {
  return join(frontend, ...segments);
}

export const StylesheetPath = underFrontend("src", "theme", "broadsheet.css");

export const Stylesheet = {
  Broadsheet: StylesheetPath,
  MetricsChart: underFrontend("src", "components", "MetricsChart.css"),
  Dashboard: underFrontend("src", "components", "Dashboard.css"),
  MetricRow: underFrontend("src", "components", "MetricRow.css"),
  SyncPanel: underFrontend("src", "components", "SyncPanel.css"),
  App: underFrontend("src", "theme", "app.css"),
};

export function stylesheetExists(): boolean {
  return existsSync(StylesheetPath);
}

function withoutComments(css: string): string {
  return css.replace(/\/\*[\s\S]*?\*\//g, "");
}

/** The body of the brace-delimited block opening at `open`. */
function blockAt(css: string, open: number): string {
  if (open < 0) return "";
  let depth = 0;
  for (let i = open; i < css.length; i += 1) {
    if (css[i] === "{") depth += 1;
    else if (css[i] === "}" && --depth === 0) return css.slice(open + 1, i);
  }
  return "";
}

/** The first top-level `:root { … }`, which is the light token block. */
function rootBlock(css: string): string {
  const start = css.indexOf(":root");
  return start < 0 ? "" : blockAt(css, css.indexOf("{", start));
}

/** The `:root` inside the dark media query, or empty when there is none. */
function darkBlock(css: string): string {
  const query = /@media[^{]*prefers-color-scheme\s*:\s*dark[^{]*/.exec(css);
  if (!query) return "";
  const media = blockAt(css, css.indexOf("{", query.index + query[0].length - 1));
  const root = media.indexOf(":root");
  return root < 0 ? "" : blockAt(media, media.indexOf("{", root));
}

function declarationsIn(block: string): Map<string, string> {
  const declarations = new Map<string, string>();
  for (const match of withoutComments(block).matchAll(/(--[A-Za-z0-9-]+)\s*:\s*([^;]+);/g)) {
    declarations.set(match[1]!, match[2]!.trim());
  }
  return declarations;
}

function read(scheme: "light" | "dark"): TokenSet {
  const css = readFileSync(StylesheetPath, "utf-8");
  const declarations = declarationsIn(rootBlock(css));
  if (scheme === "dark") for (const [token, value] of declarationsIn(darkBlock(css))) declarations.set(token, value);
  return new TokenSet(scheme, declarations);
}

export const BroadsheetTokens = {
  /** The tokens under :root. */
  get light(): TokenSet {
    return read("light");
  },
  /** The light set with the prefers-color-scheme: dark block applied over it, as the browser does. */
  get dark(): TokenSet {
    return read("dark");
  },
  /** Only what the dark media query itself redefines. */
  get darkOverrides(): Map<string, string> {
    return declarationsIn(darkBlock(readFileSync(StylesheetPath, "utf-8")));
  },
};

interface Rgba {
  red: number;
  green: number;
  blue: number;
  alpha: number;
}

/** One scheme's resolved custom properties. */
export class TokenSet {
  constructor(
    readonly scheme: string,
    private readonly declarations: Map<string, string>,
  ) {}

  get names(): string[] {
    return [...this.declarations.keys()];
  }

  declares(token: string): boolean {
    return this.declarations.has(token);
  }

  raw(token: string): string {
    const value = this.declarations.get(token);
    if (value === undefined) throw new Error(`'${token}' is not declared in the ${this.scheme} scheme.`);
    return value;
  }

  /**
   * A CSS colour expression as the opaque #rrggbb a reader would see, with any transparency
   * composited over `over` (the scheme's page background by default).
   */
  resolve(expression: string, over: string = this.raw("--color-bg")): string {
    const colour = this.parse(expression);
    if (colour.alpha >= 0.999) return hex(colour);
    const ground = this.parse(over);
    return hex({
      red: colour.red * colour.alpha + ground.red * (1 - colour.alpha),
      green: colour.green * colour.alpha + ground.green * (1 - colour.alpha),
      blue: colour.blue * colour.alpha + ground.blue * (1 - colour.alpha),
      alpha: 1,
    });
  }

  /** Ink at a stated opacity, the form this sheet's muted text takes. */
  ink(percent: number): string {
    return this.resolve(`color-mix(in srgb, var(--color-text) ${String(percent)}%, transparent)`);
  }

  private parse(expression: string): Rgba {
    const value = expression.trim();
    if (value.startsWith("var(")) return this.parse(this.raw(value.slice(4, value.lastIndexOf(")")).trim()));
    if (value.toLowerCase() === "transparent") return { red: 0, green: 0, blue: 0, alpha: 0 };
    if (value.startsWith("color-mix(")) return this.mix(value.slice(10, value.lastIndexOf(")")));
    if (value.startsWith("#")) return fromHex(value);
    throw new Error(`'${expression}' is not a colour this test can read.`);
  }

  /** color-mix(in srgb, …), mixed in premultiplied alpha as the specification says. */
  private mix(args: string): Rgba {
    const parts = splitTopLevel(args);
    if (parts.length !== 3 || parts[0]!.trim().toLowerCase() !== "in srgb") {
      throw new Error(`color-mix(${args}) is not a form this test can read.`);
    }
    const [first, firstGiven] = this.component(parts[1]!);
    const [second, secondGiven] = this.component(parts[2]!);
    const firstShare = firstGiven ?? 1 - (secondGiven ?? 0.5);
    const secondShare = secondGiven ?? 1 - firstShare;
    const alpha = first.alpha * firstShare + second.alpha * secondShare;
    if (alpha === 0) return { red: 0, green: 0, blue: 0, alpha: 0 };
    const channel = (of: (c: Rgba) => number) =>
      (of(first) * first.alpha * firstShare + of(second) * second.alpha * secondShare) / alpha;
    return { red: channel((c) => c.red), green: channel((c) => c.green), blue: channel((c) => c.blue), alpha };
  }

  private component(argument: string): [Rgba, number | null] {
    const text = argument.trim();
    const percent = /\s(\d+(?:\.\d+)?)%$/.exec(text);
    return percent
      ? [this.parse(text.slice(0, percent.index)), Number(percent[1]) / 100]
      : [this.parse(text), null];
  }
}

/** Splits on commas that are not inside a nested function call. */
function splitTopLevel(args: string): string[] {
  const parts: string[] = [];
  let depth = 0;
  let start = 0;
  for (let i = 0; i < args.length; i += 1) {
    if (args[i] === "(") depth += 1;
    else if (args[i] === ")") depth -= 1;
    else if (args[i] === "," && depth === 0) {
      parts.push(args.slice(start, i));
      start = i + 1;
    }
  }
  parts.push(args.slice(start));
  return parts;
}

function fromHex(hexValue: string): Rgba {
  let value = hexValue.replace(/^#+/, "");
  if (value.length === 3 || value.length === 4) value = [...value].map((c) => c + c).join("");
  if (value.length !== 6 && value.length !== 8) throw new Error(`'${hexValue}' is not a colour this test can read.`);
  const channel = (offset: number) => parseInt(value.slice(offset, offset + 2), 16) / 255;
  return { red: channel(0), green: channel(2), blue: channel(4), alpha: value.length === 8 ? channel(6) : 1 };
}

function hex(colour: Rgba): string {
  const byte = (c: number) => Math.round(Math.min(Math.max(c, 0), 1) * 255).toString(16).padStart(2, "0");
  return `#${byte(colour.red)}${byte(colour.green)}${byte(colour.blue)}`;
}

function normalisedSelector(selector: string): string {
  return selector.trim().replace(/\s*,\s*/g, ",").replace(/\n/g, " ").replace(/\s+/g, " ");
}

function escapeRegex(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/** The value of `property` in the rule whose selector is exactly `selector`, over CSS in hand. */
export function declarationIn(stylesheet: string, selector: string, property: string): string | null {
  const css = withoutComments(stylesheet);
  for (const rule of css.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
    if (normalisedSelector(rule[1]!) !== normalisedSelector(selector)) continue;
    const declaration = new RegExp(`(?:^|;)\\s*${escapeRegex(property)}\\s*:\\s*([^;]+)`).exec(rule[2]!);
    if (declaration) return declaration[1]!.trim();
  }
  return null;
}

/** The same read, from a file; null when the file, rule or property is missing. */
export function declaration(stylesheet: string, selector: string, property: string): string | null {
  return existsSync(stylesheet) ? declarationIn(readFileSync(stylesheet, "utf-8"), selector, property) : null;
}

/** The body of the @media rule whose condition contains `condition`, or null. */
export function mediaQuery(stylesheet: string, condition: string): string | null {
  if (!existsSync(stylesheet)) return null;
  const css = withoutComments(readFileSync(stylesheet, "utf-8"));
  const query = new RegExp(`@media[^{]*${escapeRegex(condition)}[^{]*\\{`).exec(css);
  if (!query) return null;
  const body = blockAt(css, query.index + query[0].length - 1);
  return body === "" ? null : body;
}
