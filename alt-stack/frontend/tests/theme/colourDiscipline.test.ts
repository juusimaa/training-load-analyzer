// T104: ColourDisciplineTests.cs, ported. Colour lives in the copied theme and nowhere else
// (008 FR-005, SC-009); the reference's regex, named-colour list and comment handling are kept
// as they are. The reference exempts its Theme directory, which holds only broadsheet.css; here
// that one file is exempt, and the other files under src/theme/ are scanned.
import { readdirSync, readFileSync } from "node:fs";
import { join, relative, sep } from "node:path";
import { describe, expect, it } from "vitest";
import { underFrontend } from "./tokens";

/** Hex literals, the functional notations, and the CSS named colours worth catching. */
const ColourLiteral =
  /#[0-9a-fA-F]{3,8}\b|\brgba?\s*\(|\bhsla?\s*\(|\b(?:red|blue|green|yellow|orange|purple|pink|brown|grey|gray|black|white|lightyellow|lightgrey|lightgray|darkgrey|darkgray|silver|gold|teal|navy|olive|maroon|lime|aqua|fuchsia)\b/;

/**
 * The task (T104) also names oklch( and hsl(; hsl( is in the reference's pattern, oklch( is not,
 * and is caught separately so the reference's list stays as it is.
 */
const Oklch = /\boklch\s*\(/;

const src = underFrontend("src");
const exempt = join("theme", "broadsheet.css");

function files(directory: string): string[] {
  return readdirSync(directory, { withFileTypes: true })
    .flatMap((entry) => (entry.isDirectory() ? files(join(directory, entry.name)) : [join(directory, entry.name)]))
    .sort();
}

const scanned = files(src).filter((f) => (f.endsWith(".css") || f.endsWith(".tsx")) && relative(src, f) !== exempt);

/** The lines that declare something, with comments stripped, as the reference's CodeLines does. */
function* codeLines(lines: string[]): Generator<[string, number]> {
  let inBlock = false;
  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index]!;
    const trimmed = line.trimStart();
    if (inBlock) {
      if (trimmed.includes("*/") || trimmed.includes("*@")) inBlock = false;
      continue;
    }
    if (trimmed.startsWith("/*") || trimmed.startsWith("@*")) {
      if (!trimmed.includes("*/") && !trimmed.includes("*@")) inBlock = true;
      continue;
    }
    if (trimmed.startsWith("//") || trimmed.startsWith("*")) continue;
    yield [line, index + 1];
  }
}

/** The body of every `style={{ … }}` object literal, read with brace matching. */
function inlineStyles(source: string): string[] {
  const bodies: string[] = [];
  for (const start of source.matchAll(/style=\{\{/g)) {
    const open = start.index + "style={".length;
    let depth = 0;
    for (let i = open; i < source.length; i += 1) {
      if (source[i] === "{") depth += 1;
      else if (source[i] === "}" && --depth === 0) {
        bodies.push(source.slice(open + 1, i));
        break;
      }
    }
  }
  return bodies;
}

describe("Colour discipline", () => {
  it("scans every stylesheet and component", () => {
    const names = scanned.map((f) => relative(src, f).split(sep).join("/"));
    expect(names).toContain("components/MetricsChart.css");
    expect(names).toContain("components/MetricsChart.tsx");
    expect(names).toContain("theme/layout.css");
    expect(names).not.toContain("theme/broadsheet.css");
  });

  it("no stylesheet or component names a colour outside the theme", () => {
    const offenders: string[] = [];
    for (const file of scanned) {
      for (const [line, number] of codeLines(readFileSync(file, "utf-8").split(/\r?\n/))) {
        const match = ColourLiteral.exec(line) ?? Oklch.exec(line);
        if (match) offenders.push(`${relative(src, file)}:${String(number)}  ${match[0]}  |  ${line.trim()}`);
      }
    }
    expect(offenders, `Colour must be defined in src/theme/broadsheet.css and nowhere else. Found:\n${offenders.join("\n")}`).toEqual([]);
  });

  it("inline styles carry only the chart's positions (left, width, top, height)", () => {
    const allowed = new Set(["left", "width", "top", "height"]);
    const found: string[] = [];
    for (const file of scanned.filter((f) => f.endsWith(".tsx"))) {
      const source = readFileSync(file, "utf-8");
      for (const body of inlineStyles(source)) {
        // Template literals are dropped first, so only the object's own keys remain.
        for (const property of body.replace(/`[^`]*`/g, "").matchAll(/([A-Za-z]+)\s*:/g)) {
          found.push(property[1]!);
          expect(allowed.has(property[1]!), `${relative(src, file)} sets inline '${property[1]!}'`).toBe(true);
        }
      }
      expect(/style=\{(?!\{)/.test(source), `${relative(src, file)} passes a style object not written inline`).toBe(false);
      expect(/style="/.test(source), `${relative(src, file)} has a string style attribute`).toBe(false);
    }
    expect(new Set(found)).toEqual(allowed);
  });
});
