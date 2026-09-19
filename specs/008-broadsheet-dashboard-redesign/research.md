# Phase 0 Research: Broadsheet Dashboard Redesign

**Feature**: 008-broadsheet-dashboard-redesign | **Date**: 2026-09-19

All decisions below were taken before any code was written. Three of them (R1, R4, R2) were put to
the developer explicitly rather than decided by the agent, per Constitution Principle VII.

---

## R1 — MudBlazor is removed entirely

**Decision**: Remove the MudBlazor dependency. Every athlete-facing surface becomes plain HTML
elements carrying classes from the vendored Broadsheet stylesheet.

**Rationale**: The reference design is defined by whitespace, rules and a serif type scale, not by
cards and elevation. Its README states the intent directly: "Plain CSS on plain HTML: no
JavaScript, no build step." Retheming a Material component library toward that would mean fighting
the library's surface treatments at every point, and `MudChart` still could not draw the
reference's neutral load bars behind the three metric lines with a zero rule.

The removal is smaller than it sounds. The measured surface is 9 `.razor` files, ~15 distinct
component types (mostly `MudPaper`/`MudText`/`MudChip`), **10** `--mud-palette-*` CSS references in
2 files, 1 theme class, 1 service registration and 2 asset links. The five dashboard `.razor.css`
files and `wwwroot/app.css` reference zero palette variables — they are pure layout and largely
survive.

**What this costs, accepted knowingly**: this reverses Amendment 1 of feature 007.

1. The chart loses the hover tooltips and the clickable legend that came free with `MudChart`.
   Feature 006 had deliberately chosen a legend *instead of* a tooltip, so this returns the chart
   to its originally specified behaviour rather than removing something specified.
2. The chart **regains** per-series dash patterns. Amendment 1 recorded losing them as "a real
   accessibility regression, not a wording problem", because `MudChart` draws every series with a
   solid stroke. The reference design dashes the Form line (`stroke-dasharray="3 4"`). Reinstating
   dash differentiation is a net accessibility gain from this feature.
3. Line interpolation returns to exact: the drawn polyline passes through every plotted point
   again.

**Alternatives considered**: Retheme MudBlazor in place (rejected — fights the library, and the
chart still cannot match the reference). Hybrid, keeping `MudChart` only (rejected — still ships
`MudBlazor.min.css`, the theme provider and its dark-mode JS round-trip, and leaves the chart as
the one region that does not match the design).

---

## R2 — The vendored stylesheet is a subset, and it lives under a `Theme` path segment

**Decision**: Vendor the token block and the base, `.btn`, `.tag`, `.table` and `.seg` rules from
`docs/ui/styles.css` into `src/TrainingLoadAnalyzer.Web/wwwroot/Theme/broadsheet.css`. Omit the
print-treatment section (`.halftone`, `.cmyk`, `.cmyk-num`, `.cmyk-head`) and the unused `.nav`,
`.card`, `.dialog` and `.input` component classes.

**Rationale**: `docs/ui/README.md` says the stylesheet is "vendored unchanged", but roughly 150 of
its 394 lines are CMYK print-plate, halftone and plate-numeral machinery whose `filter: url(#sep-c)`
references resolve against an SVG defs file (`print-plates.js`) this application will never ship.
Those rules are inert on arrival. Constitution Principle III treats carried weight with no current
need as a defect, and the constitution outranks a design document. Nothing about the rendered look
changes.

**The path is load-bearing.** `tests/TrainingLoadAnalyzer.Web.Tests/Theme/ColourDisciplineTests.cs`
scans every `*.css` and `*.razor` file under `src/TrainingLoadAnalyzer.Web` and fails on any colour
literal whose path does not contain a `Theme` directory segment. A vendored stylesheet is, by
definition, where colour is allowed to be named — so placing it under `wwwroot/Theme/` is
semantically correct rather than a dodge, and the existing test then passes unmodified.

Two mechanical notes: the segment must be spelled `Theme` exactly (the test's `.Contains("Theme")`
is an ordinal string comparison, and a case-insensitive filesystem will not save a lowercase
`theme/`), and the C# `Theme/` source folder keeps its current meaning for the tests that remain.

**Alternatives considered**: Vendor all 394 lines unchanged (rejected per above). Put the sheet at
`wwwroot/broadsheet.css` and widen the ColourDiscipline allowance (rejected — weakening the one
test that stops a stray `#666` from silently breaking the dark scheme, to avoid choosing a
directory name, is a bad trade).

---

## R3 — Dark mode becomes pure CSS

**Decision**: Author a dark token set as a `@media (prefers-color-scheme: dark)` block redefining
the same custom properties in `broadsheet.css`. Delete `MudThemeProvider`, the `isDarkMode` field,
the `GetSystemDarkModeAsync()` first-render read and the `<CascadingValue Name="IsDarkMode">`.

**Rationale**: The vendored stylesheet ships **no dark scheme at all** — `:root` defines only a
light palette. The spec requires both appearances (FR-020..FR-023), so a dark token set has to be
authored either way; it is not inherited from the design docs. Given that, the cheapest correct
mechanism is the platform's own: a media query needs no JavaScript, no round-trip, and no
first-render special case.

This also retires a documented workaround. `MainLayout.razor` currently carries a comment
explaining that MudBlazor's `ObserveSystemDarkModeChange` only attaches a `matchMedia` *change*
listener and never reads the current value, which is why an explicit first-render read was needed.
A media query has no such gap and follows preference changes natively, which is what FR-021
requires.

The stylesheet's own token comment anticipates this: elevation is described as "soft ink-tinted
shadows on a light theme, a hairline edge + ambient darkness on a dark one."

**Consequence to watch**: `@rendermode="InteractiveServer"` sits on `<Routes>` in `App.razor`
*specifically* so `MudThemeProvider` would get an `OnAfterRenderAsync`. That reason disappears.
`Dashboard.razor` declares its own `@rendermode InteractiveServer`, so the sync button and the new
window selector keep working; the root-level render mode can be narrowed. Narrowing it is optional
and must not be bundled in silently — it is listed as its own task.

**Alternatives considered**: Keep `MudThemeProvider` purely for scheme detection (rejected — keeps
the whole dependency for one boolean). A stored athlete preference or in-app toggle (rejected —
FR-021 specifies following the device preference, and 007 FR-027 forbade a toggle as new
functionality).

---

## R4 — Measured contrast failures in the reference design, and their remedies

**Decision**: Step the failing values down their existing ramps as tabulated below. Exempt the
chart's daily-load bars from FR-015 via a recorded spec amendment; darken the zero rule to comply.

**Rationale**: The reference design and the vendored tokens were audited against WCAG 2.1 before
planning, not after. Measured ratios against `--color-bg` (`#f3f2f2`):

| Element | Value as drawn | Ratio | Needs | Remedy |
| --- | --- | --- | --- | --- |
| `.btn-primary` label, 14px | on `--color-accent` | 3.65:1 | 4.5:1 | background → `--color-accent-700` (5.72:1) |
| Links, `.btn-ghost`, 14px | `--color-accent` | 3.65:1 | 4.5:1 | → `--color-accent-700` (5.72:1) |
| `.table th`, 11px | 60% ink | 4.23:1 | 4.5:1 | → 70% ink (5.79:1) |
| `.text-muted`, `figcaption` | 55% ink | 3.66:1 | 4.5:1 | → 70% ink (5.79:1) |
| Chart date axis, 14px | `--color-neutral-600` | 3.85:1 | 4.5:1 | → `--color-neutral-700` (5.83:1) |
| Legend "Form" label, 14px | `--color-neutral-600` | 3.85:1 | 4.5:1 | → `--color-neutral-700` (5.83:1) |
| `.tag-outline` text, 11px | `--color-accent` | 3.65:1 | 4.5:1 | → `--color-accent-700` (5.72:1) |
| Chart zero rule | `#bab6b6` (neutral-400) | 1.80:1 | 3:1 | → `--color-neutral-600` (3.85:1) |
| **Chart load bars** | `#d7d3d3` (neutral-300) | **1.33:1** | 3:1 | **exempted — see below** |

Values that already pass and are left alone: body ink (14.86:1), the Fitness and Fatigue display
figures at `-700` (5.72:1 and 6.50:1 — the reference page already steps these down), the three
chart strokes as non-text indicators (3.65 / 4.61 / 3.85:1), `.tag-accent` (9.10:1), and the
`.input` placeholder (4.78:1).

**The load bars are the one case with no painless fix.** The full neutral ramp against the page
background is 1.02 / 1.10 / 1.33 / 1.80 / 2.59 / 3.85 / 5.83 / 9.04 / 12.60:1 for steps 100..900 —
so the lightest step clearing 3:1 is `neutral-600`, which is the same value as the Form line.
Complying would give the backdrop the same visual weight as the data lines and collapse the
figure-and-ground separation the design is built on.

The bars are therefore treated as a contextual backdrop rather than an indicator required to
understand the content: the three metric lines carry the chart's message, the chart is titled with
its window, and the underlying daily figures are also published as text in the recent-sessions
table. This is recorded as Amendment 1 to the spec rather than left as a silent divergence.

**Alternatives considered**: Darken the bars to `neutral-600` (rejected by the developer — flattens
the design). Ship as drawn with FR-015 unamended (rejected — leaves the spec asserting a bar the
interface knowingly misses, which is exactly what the constitution's compliance review exists to
catch).

**Dark scheme**: the same audit must be re-run against the dark tokens once authored. R7 makes it
executable rather than a review note.

---

## R5 — The chart returns to hand-drawn SVG, recovered from history

**Decision**: Revive `src/TrainingLoadAnalyzer.Web/Features/Dashboard/MetricsChart.cs` and
`tests/TrainingLoadAnalyzer.Web.Tests/MetricsChartTests.cs` from commit `b1e8726^`, then extend
them for the bars, the zero rule and the date axis.

**Rationale**: The deleted code is recoverable and carries a hard-won lesson worth not relearning.
Its doc-comment records the bug it exists to prevent:

> On a machine set to Finnish the obvious rendering of the points (0, 45.3) and (1.5, 12.25)
> produces `points="0,45,3 1,5,12,25"` — valid-looking markup, silently wrong geometry, no
> exception, and perfectly correct on an en-US machine.

Every coordinate goes through one `CultureInfo.InvariantCulture` path. Reimplementing the plot from
scratch would risk reintroducing exactly that. The existing single-shared-scale logic (so a
negative Form sits inside the band rather than clipped at zero) also carries over unchanged.

Extensions needed: load bars as `<rect>` geometry on their own scale, a zero rule positioned on the
shared scale, and evenly spaced date-axis ticks.

**Colour must come from CSS classes, not SVG presentation attributes.** The reference page writes
`stroke="#d6006c"` and `fill="#d7d3d3"` inline. Ported as-is into a `.razor` file those literals
would fail `ColourDisciplineTests`, and — more importantly — would not respond to the dark scheme.
The SVG elements get classes (`.series-fitness`, `.series-fatigue`, `.series-form`, `.load-bar`,
`.zero-rule`) and the `stroke`/`fill` CSS properties resolve from tokens. `ChartSeries` already
carries a `CssClass` field for this.

**Alternatives considered**: Vendor a JavaScript charting library (rejected — a build step and a
dependency for one plot, against Principle III, and against the design's "no JavaScript" intent).

---

## R6 — The window selector slices data already in the view

**Decision**: Hold the selected window (30/90/180) as component state on `Dashboard.razor` and take
the trailing slice of `DashboardView.Metrics`. No new query, no new read-model call, no round-trip
to storage.

**Rationale**: `DashboardView.Metrics` is already capped at 180 days, which is the largest window
offered, so every selection is a slice of data already loaded. `Dashboard.razor` already declares
`@rendermode InteractiveServer`, so a Blazor event handler suffices.

`HasEnoughHistoryForChart` stays as-is (`Metrics.Count >= 30`): it describes whether there is
enough *history* to plot at all, which is a property of the stored data, not of the chosen window.
Selecting a 30-day window on 200 days of history is not an insufficient-history state.

---

## R7 — The palette tests move from MudBlazor types to CSS custom properties

**Decision**: Keep `Theme/ContrastRatio.cs` unchanged. Rewrite `Theme/PaletteContrastTests.cs` and
`Theme/PaletteSlotTests.cs` to parse the custom properties out of `wwwroot/Theme/broadsheet.css`
and assert ratios over both the light and dark token sets. Keep `Theme/ColourDisciplineTests.cs`
unmodified. Delete `Fakes/MudChartBounds.cs`.

**Rationale**: `ContrastRatio.Between(string, string)` takes hex strings and has no MudBlazor
dependency — only its XML comment mentions `MudColor`. It survives intact. Only
`PaletteContrastTests` and `PaletteSlotTests` reach into `MudColor`/`Palette` off
`TrainingLoadTheme.Instance`; pointing them at the stylesheet instead preserves what they were
actually for. Their own doc-comment states the stake: "SC-005 would otherwise be eyeballed, and
would rot the first time a colour was nudged. With this, the criterion has a RED state."

This is what makes R4 a TDD story rather than a review checklist: each measured failure in that
table is a test that goes RED first, including against the dark tokens that do not exist yet.

---

## R8 — Markup hooks the existing tests bind to, which must survive

**Decision**: Preserve these exact hooks through the rewrite.

| Hook | Asserted by | Note |
| --- | --- | --- |
| `.recent-row` | `DashboardComponentTests:365` | rows in the recent-sessions table |
| `.tile-value` | `DashboardComponentTests:200` | asserted *absent* in the empty state |
| `button.sync` | `DashboardComponentTests:377,415` | must be a real `<button>`, `disabled` while running |
| exactly one `<h1>` | `InformationPreservationTests` | also required by `FocusOnNavigate Selector="h1"` |
| `<h2>` per region | `InformationPreservationTests` | region headings, read in order |
| no `<svg>` under 30 days | `DashboardComponentTests:294` | insufficient-history branch renders none |

**Rationale**: Feature 007 wrote its characterization tests to survive exactly this kind of
redesign — `InformationPreservationTests` strips tags and asserts on extracted text, and its class
doc says so outright: "They assert on extracted text, not on markup. A redesign changes markup by
definition." No test anywhere references a MudBlazor component type or a `mud-*` class. So the
suite is a genuine safety net for FR-002 and SC-001, and the handful of structural assertions above
are the only coupling points to honour.

The single-`h1` constraint outlives the app bar that motivated it: `Routes.razor` uses
`FocusOnNavigate Selector="h1"`, so the new rail must not introduce a second `<h1>`. The reference
page sets its "Training Load" masthead as a styled `<div>`, which is consistent with this.

---

## R9 — Touch targets go to 48px, not the reference's 44px

**Decision**: Give the window-selector options a 48px minimum height.

**Rationale**: The reference page sets `.seg-opt { min-height: 44px }` and `.btn-primary
{ min-height: 48px }`. FR-017 requires 48×48 for every interactive control, and `app.css` already
carries a 48px minimum for the current buttons. 44px would miss it. The 4px difference is
imperceptible against the reference and keeps a requirement that is already met today from
regressing.

---

## R10 — The webfont stays a CDN import with a system fallback

**Decision**: Keep the stylesheet's `@import` of Source Serif 4 from Google Fonts. Do not
self-host.

**Rationale**: The token block already declares `"Source Serif 4", system-ui, sans-serif`, so a
blocked or offline font request degrades to the system serif stack rather than breaking the page.
Self-hosting means adding font binaries and a build step for no current need (Principle III), and
the spec puts offline appearance out of scope. Worth revisiting if the application is ever deployed
beyond local use, which it is not.

---

## R11 — Extract the duplicated bUnit setup

**Decision**: Extract the ~15-line `SeedAndRegister`/`RenderDashboard` DI block duplicated across
`DashboardComponentTests` and `InformationPreservationTests` into one shared test base.

**Rationale**: A concrete current need, not speculation (Principle III): the block already exists
twice, both copies must change together when `AddMudServices()` and the `MudChartBounds` JSInterop
fake are removed, and the new tests for the rail, the window selector and the restyled states would
make it a third and fourth copy. The extraction is test-side only and adds no production surface.

---

## R12 — Three deliberate functional additions, and the spec conflict they expose

**Decision**: Record Amendment 1 against the spec, carving these out of FR-001's scope guard.

**Finding**: FR-001 states the system "MUST NOT change any computation, stored data, imported data,
navigation destination, or the conditions under which any message, value or state is shown. Only
the presentation of existing content may change." Three requirements in the same specification
require exactly that:

1. **The chart window control (FR-005)** — new athlete-facing behaviour. The chart currently has a
   fixed range.
2. **The configured maximum heart rate in the rail (FR-003)** — new displayed content.
   `AthleteSettings.MaximumHeartRate` exists and feeds load estimation in `DashboardReader`, but it
   is not on `DashboardView` and is displayed nowhere today. This adds a read-model member.
3. **The ISO week designation in the rail (FR-003)** — new displayed content. The weekly panel
   currently reads "This week" with no week identifier; the reference rail shows "ISO week
   2026-W38".

All three come from the reference design, which the spec's own Assumptions already make the source
of truth for the target design. The conflict is wording: FR-001's scope guard was carried over
verbatim from feature 007, where the instruction really was "change nothing but the appearance",
and it was never narrowed to admit the additions FR-003 and FR-005 introduce.

Resolving this in the spec rather than in code is required by Principle VII. No further scope is
added: the amendment narrows FR-001 to exactly these three, and FR-002's guarantee that nothing
existing is lost stands unamended.
