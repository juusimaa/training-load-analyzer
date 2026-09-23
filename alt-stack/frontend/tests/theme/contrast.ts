// Port of tests/TrainingLoadAnalyzer.Web.Tests/Theme/ContrastRatio.cs: WCAG 2.1 relative
// luminance and contrast ratio. Test-side only, as in the reference.

/** The ratio between two colours, 1 (identical) to 21 (black on white); order does not matter. */
export function contrastBetween(firstHex: string, secondHex: string): number {
  const first = relativeLuminance(firstHex);
  const second = relativeLuminance(secondHex);
  const lighter = Math.max(first, second);
  const darker = Math.min(first, second);
  return (lighter + 0.05) / (darker + 0.05);
}

function relativeLuminance(hex: string): number {
  const [red, green, blue] = parse(hex);
  return 0.2126 * channel(red) + 0.7152 * channel(green) + 0.0722 * channel(blue);
}

function channel(value: number): number {
  return value <= 0.03928 ? value / 12.92 : Math.pow((value + 0.055) / 1.055, 2.4);
}

/** #RGB, #RRGGBB or #RRGGBBAA; an alpha channel is ignored, as in the reference. */
function parse(hex: string): [number, number, number] {
  let value = hex.replace(/^#+/, "");
  if (value.length === 3) value = [...value].map((c) => c + c).join("");
  if (value.length !== 6 && value.length !== 8) throw new Error(`'${hex}' is not a colour this test can read.`);
  const component = (offset: number) => parseInt(value.slice(offset, offset + 2), 16) / 255;
  return [component(0), component(2), component(4)];
}
