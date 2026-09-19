# Contract: Theme

**Feature**: 007-material-design-ui

The single definition site required by NFR-002. Every colour, type step and layout constant in the application comes from here; no component, stylesheet or `Style=` attribute may introduce one (FR-005, FR-006, SC-009, enforced by the colour-literal scan).

**Definition site**: `src/TrainingLoadAnalyzer.Web/Theme/TrainingLoadTheme.cs` — a static `MudTheme` — and `Theme/ChartPalette.cs` for the three series colours MudBlazor has no slot for.

Colour lives in C# rather than CSS deliberately: it lets the contrast test read the object the application actually renders with, instead of parsing a stylesheet ([../research.md](../research.md) R6).

## Palette

`MudTheme.PaletteLight` and `MudTheme.PaletteDark` declare the identical set of slots (FR-025, FR-028).

| Mud slot | Light | Dark | Used for |
|----------|-------|------|----------|
| `Background` | `#FDFCFF` | `#111318` | Page background |
| `Surface` | `#F1F3F9` | `#1E2025` | `MudPaper` / card surfaces |
| `TextPrimary` | `#1A1C1E` | `#E2E2E6` | Headings, metric values, body text |
| `TextSecondary` | `#43474E` | `#C3C7CF` | Labels, qualifiers, timestamps |
| `Primary` | `#00639B` | `#96CCFF` | Filled buttons, focus ring, links |
| `AppbarBackground` | `#00639B` | `#1E2025` | `MudAppBar` |
| `Divider` | `#C3C7CF` | `#43474E` | Decorative separation only |
| `Error` | `#BA1A1A` | `#FFB4AB` | Data-unavailable notice, error page |

MudBlazor derives hover, ripple and disabled variants from these. `TextDisabled` and `ActionDisabled` are **not** set explicitly and must be checked during review against the disabled sync button, which still carries the word `Syncing…` (FR-002, R4).

## Chart series

`ChartPalette` — passed to `MudChart` as its `ChartOptions.ChartPalette`, selected by the `IsDarkMode` value bound from `MudThemeProvider` ([../research.md](../research.md) R5).

| Index | Light | Dark | Series |
|-------|-------|------|--------|
| 0 | `#0072B2` | `#56B4E9` | Fitness |
| 1 | `#D55E00` | `#E69F00` | Fatigue |
| 2 | `#009E73` | `#009E73` | Form |

All six are Wong colour-blind-safe palette entries. Palette **order matters** — it is positional, matched to the order the series are supplied, and the legend derives its labels from the series names.

**The dash patterns are gone.** MudChart draws every series with a solid stroke and exposes no dash option. Feature 006's colour-blind differentiation in the static plot is lost; identification now rests on the legend and hover labels (FR-013a). Accepted in Amendment 1 — see [../research.md](../research.md) R5 before considering a CSS override.

## Typography

`MudTheme.Typography`, mapped to the five roles the application uses.

| Mud typo | Size / weight | Used for |
|----------|---------------|----------|
| `H4` | `1.75rem / 400` | The page `<h1>` |
| `H6` | `1.125rem / 500` | Section headings, app bar title |
| `H3` | `2.5rem / 400` | Metric values and the weekly total |
| `Body1` | `0.9375rem / 400` | Body text, list rows |
| `Caption` | `0.8125rem / 500` | Chips, qualifiers, timestamps |

Mud's typo names are presentational, not semantic: `Typo="Typo.h6" HtmlTag="span"` gives title styling without adding a heading to the outline, which is how the app bar keeps the page `<h1>` unique (R10).

## Layout

`MudTheme.LayoutProperties`, plus the container choice.

| Constant | Value | Serves |
|----------|-------|--------|
| `MudContainer MaxWidth` | `Large` | FR-018, comfortable reading width |
| `DefaultBorderRadius` | `12px` | Card and panel shape |
| Touch target | `48px` minimum | FR-021 — MudBlazor's default button sizing meets this; verify, do not assume |

## Contrast obligations

Asserted against the theme object in **both** palettes. Measured values in [../research.md](../research.md) R4 and R5.

| Foreground | Background | Minimum |
|------------|------------|---------|
| `TextPrimary` | `Background` | 4.5 |
| `TextPrimary` | `Surface` | 4.5 |
| `TextSecondary` | `Background` | 4.5 |
| `TextSecondary` | `Surface` | 4.5 |
| `Primary` | `Background` | 4.5 |
| `Error` | `Background` | 4.5 |
| `TextDisabled` | `Surface` | 4.5 |
| `chart-fitness` | `Surface` | 3.0 |
| `chart-fatigue` | `Surface` | 3.0 |
| `chart-form` | `Surface` | 3.0 |

### Declared exclusion

`Divider` against `Surface` measures 1.53 (light) / 1.75 (dark) and is **excluded by name**, with the rationale recorded in the test: it is decorative, and WCAG 1.4.11 scopes its 3:1 requirement to indicators needed to identify components or understand content. Any boundary that becomes the sole carrier of meaning must use a colour that passes.

The exclusion is an explicit entry, not an omission, so removing it is a deliberate act.
