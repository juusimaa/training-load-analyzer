# Phase 0 Research: Material Design Visual Refresh

**Feature**: 007-material-design-ui | **Date**: 2026-09-18

All Technical Context unknowns are resolved below. No `NEEDS CLARIFICATION` remains.

> **Revision note**: this research has been revised twice, both times by developer decision.
> 1. R1 was originally "no library". Reversed to MudBlazor; R10 reversed as a consequence.
> 2. R15 originally kept the SVG chart and the `<ul>/<li>` list. Reversed to full adoption of `MudChart` and `MudList`, which required **Amendment 1** to the spec. R5 and R15 below are rewritten; R16 is new.
>
> Costs are recorded as costs, not argued away.

## Baseline measured before planning

| Fact | Value | How established |
|------|-------|-----------------|
| Web test suite | 99 tests, all passing, 2.2s | `dotnet test --project tests/TrainingLoadAnalyzer.Web.Tests` |
| Styled surfaces today | 1 of 8 (`MetricsChartView.razor.css` only) | File survey of `src/TrainingLoadAnalyzer.Web` |
| CSS framework present | None | No Bootstrap/Tailwind reference in markup, `csproj` or `wwwroot` |
| Selectors the tests bind to | `polyline` ×3, `li` ×1, `button` ×1, `button.sync` ×1 | Grep of `Find(`/`FindAll(` across the test project |
| Latest MudBlazor | 9.10.0, targets `net10.0` with `Microsoft.AspNetCore.Components 10.0.1` | NuGet flat container + nuspec |

The 99-test baseline is the SC-002 gate: still 99 passing, no assertion weakened, when the feature is done.

---

## R1: Adopt MudBlazor 9.10.0

**Decision**: take a dependency on `MudBlazor` 9.10.0 and build the refresh from its components and theming system.

**Constitutional position**: the Technology Constraints require that a different frontend technology "MUST NOT be introduced without a documented reason". This section *is* that documented reason, and the Complexity Tracking table in [plan.md](plan.md) carries the formal justification. This is the escape hatch the constitution provides, used deliberately — not a silent deviation.

**Reason**: the feature's goal is that the application reads as authentically Material. A hand-authored token layer reaches that only as far as the author's eye for Material's proportions, states and elevation; MudBlazor encodes them. For a project whose stated purpose is evaluating spec-driven AI development rather than learning CSS, buying the visual language is a better use of the budget than reproducing it.

**What it buys**:

- A complete Material component set — surfaces, buttons, chips, typography — with hover/focus/disabled states already correct (FR-009).
- Light and dark palettes as one theme object, switched automatically (R3), which is most of FR-025 – FR-029.
- Correct touch targets and focus rings by default (FR-020, FR-021).

**What it costs**, stated plainly:

| Cost | Detail |
|------|--------|
| Dependency weight | A component framework plus its CSS and JS bundle, for a five-panel dashboard |
| Interop dependency | Dark mode runs through JS (R3). The pure-CSS alternative needed none |
| Prerender flash | A real regression against the CSS approach — see R14 |
| Test setup churn | Every existing bUnit test that renders `Dashboard` needs MudBlazor services registered — see R13 |
| Constitution III friction | ~80 components to style 5 panels. Acknowledged, accepted by the developer, justified in Complexity Tracking |

**Correction to the earlier analysis**: the first version of this research claimed a library would break SC-002 because tests bind to `button.sync`. That was wrong. MudBlazor components accept a `Class` parameter appended to the rendered root element, so `<MudButton Class="sync">` still renders `<button class="… sync">` and the selector still matches. The genuine test risks are narrower and are handled in R13 and R15.

**Alternatives considered**: hand-authored tokens (viable, lighter, no interop, no flash — rejected by the developer in favour of authentic Material); Fluent UI Blazor (not Material, fails FR-004); Material Web components (JS-first, weak Blazor story); Tailwind (needs a Node toolchain).

---

## R2: Where styling lives — theme object first, CSS last

**Decision**: four layers, in order of precedence:

1. **`Theme/TrainingLoadTheme.cs`** — a static `MudTheme` with `PaletteLight`, `PaletteDark`, `Typography`, `LayoutProperties`. This is the single definition site required by NFR-002.
2. **MudBlazor components** — carry their own styling; no CSS written for them.
3. **`wwwroot/app.css`** — reduced to what MudBlazor does not own: the reduced-motion rule, and nothing else once the dead rules go (R11).
4. **Scoped `*.razor.css`** — only `MetricsChartView.razor.css`, which styles markup MudBlazor does not produce.

**Rationale**: putting colour in C# rather than CSS is what makes SC-005 cheaply testable — the contrast test reads the theme object directly instead of parsing a stylesheet (R6). It also means one file answers "what colour is this application", which is what NFR-002 asks for.

**Consequence**: the amount of hand-written CSS in this feature approaches zero. That is the point of R1, and if implementation finds itself writing component CSS, it is fighting the library and should reconsider the component choice instead.

---

## R3: Dark scheme — half automatic, corrected during implementation

**Decision**: `<MudThemeProvider @ref="themeProvider" Theme="TrainingLoadTheme.Instance" @bind-IsDarkMode="isDarkMode" />` in `MainLayout`, **plus** an explicit `GetSystemDarkModeAsync()` call on the first interactive render, **plus** `<Routes @rendermode="InteractiveServer" />` in `App.razor`.

> **Corrected 2026-09-18, during implementation.** This section previously concluded that a bare
> `<MudThemeProvider />` follows the device preference with no code written. That was wrong, and the
> page rendered light under an emulated dark preference until it was fixed. The original reasoning
> and the two things it missed are recorded below, because this project exists to evaluate the
> process and a silently corrected research error teaches nothing.

**What the original research got right**: `ObserveSystemDarkModeChange` is `[Parameter]` with default `true`, and `OnAfterRenderAsync(firstRender)` does call `WatchDarkMode()`.

**What it missed — the library's JavaScript**:

```js
watchDarkMode(e){ Y = e, W.addEventListener("change", pe) }
```

That is the whole implementation. It registers a listener for the preference **changing** and never reads what the preference already is. `isDarkMode()` exists as a separate function that nobody calls on the component's behalf. So the flag covers only half of what its name suggests: a device that was already dark when the page loaded stays light until the setting is toggled twice.

The error was reading the C# far enough to see a watcher being wired up, then inferring the behaviour from the parameter's name instead of following the call through to the JavaScript it invokes. The fix is one call in `OnAfterRenderAsync`:

```csharp
if (firstRender)
{
    isDarkMode = await themeProvider.GetSystemDarkModeAsync();
    StateHasChanged();
}
```

**What it also missed — the render mode**: `MudThemeProvider` lives in `MainLayout`, and a layout in a Blazor Web App renders as static SSR by default even when the page it wraps is interactive. `OnAfterRenderAsync` never runs there, so no interop happens at all and every page stays light regardless of the device setting. The fix is `<Routes @rendermode="InteractiveServer" />` in `App.razor`.

A render mode **cannot** be put on the layout itself: `@rendermode` on `MainLayout` compiles, and then silently renders nothing — its `@Body` is a `RenderFragment`, which cannot cross a render-mode boundary. That was tried first and produced a page with no app bar and no theme at all.

**How both were caught**: by emulating `prefers-color-scheme: dark` over CDP and reading the computed `--mud-palette-*` variables back out of the page. Chrome's `--force-dark-mode` flag is useless here — it applies a rendering filter that makes any page look dark, and the app looked convincingly dark under it while the theme was still entirely light. The giveaway was the app bar still painting the light scheme's blue.

**Verified working**, both schemes, after the fix:

| | Light | Dark |
|---|---|---|
| `--mud-palette-background` | `#FDFCFF` | `#111318` |
| `--mud-palette-surface` | `#F1F3F9` | `#1E2025` |
| `--mud-palette-appbar-background` | `#00639B` | `#1E2025` |
| Chart strokes | `#0072B2` `#D55E00` `#009E73` | `#56B4E9` `#E69F00` `#009E73` |

FR-027 is still satisfied by simply not adding a toggle.

## R4: Palette — the measured values, expressed as a MudTheme

**Decision**: carry the measured palette into `PaletteLight` / `PaletteDark`. The values are unchanged from the original analysis; only their home changes from CSS custom properties to theme properties.

| Role | Light | Dark | Mud palette slot |
|------|-------|------|------------------|
| Page background | `#FDFCFF` | `#111318` | `Background` |
| Card surface | `#F1F3F9` | `#1E2025` | `Surface` |
| Primary text | `#1A1C1E` | `#E2E2E6` | `TextPrimary` |
| Secondary text | `#43474E` | `#C3C7CF` | `TextSecondary` |
| Primary | `#00639B` | `#96CCFF` | `Primary` |
| Divider | `#C3C7CF` | `#43474E` | `Divider` |
| Error | `#BA1A1A` | `#FFB4AB` | `Error` |

Measured contrast, both schemes, all passing:

| Pair | Light | Dark | Required |
|------|-------|------|----------|
| Primary text on background | 16.72 | 14.38 | 4.5 |
| Primary text on surface | 15.40 | 12.62 | 4.5 |
| Secondary text on surface | 8.41 | 9.62 | 4.5 |
| Primary on background | 6.31 | 10.95 | 4.5 |
| Error on background | 6.32 | 10.94 | 4.5 |

**Declared exclusion**: `Divider` measures 1.53 (light) / 1.75 (dark) against the surface. It is decorative — grouping is already carried by spacing and elevation — and WCAG 1.4.11 scopes its 3:1 requirement to indicators needed to identify components or understand content. Excluded **by name** in the contrast test, not omitted silently. Any boundary that becomes the sole carrier of meaning must use a colour that passes.

**Note**: MudBlazor computes hover/ripple/disabled variants from these. Check `TextDisabled` and `ActionDisabled` against the disabled sync button during review — MudBlazor's defaults are not guaranteed to clear 4.5:1 and the disabled button still carries the word `Syncing…` (FR-002).

---

## R5: Chart colours under MudChart

**Decision**: pass the Wong palette to `MudChart` as its `ChartPalette`, selecting the light or dark trio from the `IsDarkMode` value `MainLayout` binds from the theme provider. Light `#0072B2` / `#D55E00` / `#009E73`; dark `#56B4E9` / `#E69F00` / `#009E73`.

**Why the dark trio differs**: `#0072B2` measures ~2.5:1 against the dark plot surface, below the 3:1 FR-019 and the amended FR-029 require. `#009E73` already measures 4.76:1 on dark and is unchanged.

Measured against the card surface: light 4.67 / 3.49 / 3.08; dark 7.06 / 7.24 / 4.76. All ≥ 3.0.

**The dash patterns are gone.** `MudChart`'s line renderer emits `stroke`, `stroke-opacity` and `stroke-width` on each `<path>` and offers no dash option — verified by reading `Line.razor` at `v9.10.0`, which contains no occurrence of `stroke-dasharray`. A CSS override selecting `.mud-chart-line path.mud-chart-serie:nth-of-type(n)` would restore them and was offered; the developer chose to accept the loss instead, so **no such override is to be added** — someone will otherwise "helpfully" add one and reopen a settled decision.

This is the accessibility regression recorded in Amendment 1. Series identification now rests on the legend and hover labels (FR-013a), not on the shape of the line.

**Do not "fix" the line-to-line contrast.** The three series have near-identical luminance by design (dark: 0.405 / 0.416 / 0.257). WCAG asks each line to contrast with its **background**, not its neighbours.

## R6: Contrast as a test, made easier by the theme object

**Decision**: a test reads `TrainingLoadTheme.Instance` directly, walks a declared table of (foreground, background, minimum) triples, and computes WCAG relative-luminance ratios for `PaletteLight` and `PaletteDark` in turn. Same for `ChartPalette`.

**Rationale**: Principle I. SC-005 would otherwise be eyeballed and would rot on the first colour tweak. R1's move of colour from CSS into C# makes this strictly better than the original plan: no CSS parsing, no file-location logic, no regex — just read the object the application actually uses. The luminance maths lives in `tests/`, since it verifies the product rather than shipping in it (Principle III).

---

## R7: Keeping colour discipline (FR-005, SC-009)

**Decision**: a test scans `.razor` and `.css` files under the Web project for colour literals — hex, `rgb(`, `hsl(`, CSS named colours — and asserts they appear only in the theme files.

**Rationale**: with a component library the failure mode shifts but does not disappear. Instead of a stray `#666` in a stylesheet it becomes a stray `Style="color:#666"` or a hard-coded `Color="Color.Warning"` standing in for a real decision. The scan catches the first; review catches the second. The cost is a few lines and it makes the erosion impossible to land unnoticed.

---

## R8: Proving nothing was lost

Three layers, in order of strength:

1. **The existing 99 tests.** SC-002, the primary gate. Note R13: their *setup* changes, their assertions do not.
2. **Text-content characterization per state.** Render each dashboard state, strip tags, assert every expected string survives: the figures, `still settling`, `estimated`, `measured`, `Connect Strava`, the timestamp, the legend wording. This is SC-001 made executable, and it is what catches a `MudChip` that swallowed its text.
3. **Rendered-output smoke test.** Assert the served page carries the MudBlazor stylesheet and the theme's own `<style>` block, guarding the class of bug commit `4c22009` fixed — CSS present on disk, absent from the browser.

**Rejected**: markup snapshot tests. A redesign changes markup by definition, so the snapshot fails on every intended change and trains everyone to re-approve blindly. Text content is the invariant worth freezing.

---

## R9: Layout

**Decision**: `MudContainer` with `MaxWidth.Large` for the reading width (FR-018); `MudGrid`/`MudItem` with `xs="12" sm="4"` for the three metric cards, which collapses to one column on a phone (FR-016); `MudStack` for vertical rhythm (FR-006).

**Rationale**: MudBlazor's grid is a 12-column responsive system with the breakpoints already defined. Writing `repeat(auto-fit, minmax(…))` by hand alongside it would mean two layout systems in one page.

**Watch**: FR-017 forbids horizontal scrolling from 320px. MudBlazor's container has default gutters that can overflow at the narrow end — verify at 320px specifically, since it is below the `xs` breakpoint most examples assume.

---

## R10: Top app bar — reversed, now included

**Decision**: use `MudAppBar` in `MainLayout`, carrying the product name.

**Reversal**: the earlier decision rejected an app bar. Two of the three reasons were about hand-rolled cost and are void under R1 — MudBlazor gives the bar for free and it is a large part of why a Material app looks finished. The third reason was real and still is, so it is handled rather than ignored:

`Routes.razor` sets `<FocusOnNavigate Selector="h1" />`. The page `<h1>` must therefore survive and stay unique. The app bar title is rendered as `<MudText Typo="Typo.h6" HtmlTag="span">` — Material's title styling on a `<span>`, contributing no heading to the document outline. The page keeps exactly one `<h1>`, focus management keeps working, and heading order stays clean.

**Accepted wart**: the product name then appears in both the bar and the `<h1>`. This is the ordinary pattern (tab title, app bar, page heading) and costs one line of duplicated text. Alternatives — moving the `<h1>` into the layout, or hiding it — break the error pages or remove visible text that FR-002 requires kept.

---

## R11: Dead CSS removal

**Decision**: delete `.valid` / `.invalid` / `.validation-message`, `.blazor-error-boundary`, `.darker-border-checkbox` and `.form-floating` from `app.css`. Restyle `#blazor-error-ui` onto theme colours.

**Rationale**: verified by grep — no `EditForm`, no `ErrorBoundary`, no Bootstrap anywhere in the project. Two of them reference `--bs-*` variables that are defined nowhere, so they are inert. Under R1 this matters more, not less: leaving dead CSS beside MudBlazor's own reset invites cascade conflicts nobody can attribute.

---

## R12: Motion

**Decision**: rely on MudBlazor's own transitions; add one global `@media (prefers-reduced-motion: reduce)` rule in `app.css` reducing transition and animation durations to near-zero.

**Rationale**: FR-024. MudBlazor animates ripples and state layers and does not suppress them on its own, so the escape hatch has to be ours. One rule, globally, rather than a per-component discipline that will be forgotten.

---

## R13: bUnit and MudBlazor — the main risk to the 99 tests

**Decision**: register MudBlazor in the bUnit context — `Services.AddMudServices()` and `JSInterop.Mode = JSRuntimeMode.Loose` — in the existing `SeedAndRegister` helper and anywhere else a component under test now contains MudBlazor markup.

**Why this is compatible with SC-002**: SC-002 forbids *weakening or removing an assertion*. Adding service registration to a test's arrange step changes no assertion. Every `Assert` in the existing 99 stays exactly as written.

**Why it is nonetheless the biggest risk in the feature**: MudBlazor components call JS on render. With bUnit's default strict interop they throw. Loose mode makes unplanned calls return default instead — which is right for tests that are about content, not about interop, but it does mean interop failures stop being visible in tests. That is a real reduction in what the suite would catch, and it is the price of R1.

**First implementation task is a spike**: add the package, register the services, convert one component, run the suite. If the 99 do not come back green, that is a go/no-go on the approach and must be raised before the rest of the work proceeds. This is the single unverified assumption in the plan.

---

## R14: Prerender flash — an accepted regression

**Problem**: the app is Blazor Interactive **Server** with prerendering. On first paint the server has not run JS, so `IsDarkMode` is `false` and the page renders light. When interop resolves, the theme switches. A dark-mode user sees a light flash on every first load.

**Decision**: accept it for now, and record it so it is a known trade-off rather than a bug report later.

**Why accept**: the fixes each cost more than the flash. Disabling prerender slows first paint for everyone. A cookie carrying the preference is stored client state and new behaviour, which FR-003 and FR-027 forbid. An inline pre-interop script that sets the background early is possible but duplicates the theme in a second place, against NFR-002.

**Honest comparison**: the rejected CSS-only approach had no flash, because a media query is evaluated before first paint. This is the clearest concrete thing given up by choosing R1.

---

## R15: Full adoption — MudChart and MudList

**Decision**: replace the hand-written SVG chart with `MudChart` (line type) and the `<ul>/<li>` activity list with `MudList`. Reversed from the original decision to keep both; required spec Amendment 1.

### What `MudChart` actually renders

Verified from `Line.razor` at `v9.10.0`, not assumed:

| Fact | Consequence |
|------|-------------|
| Series are `<path>` elements, not `<polyline>` | The three `FindAll("polyline")` assertions no longer apply |
| Geometry is computed by the component | `MetricsChart.Plot` becomes dead code; its 4 tests go with it |
| `ShouldInterpolate => true` | The drawn curve need not pass exactly through every point |
| Each path carries `onclick`; invisible `<circle>` elements carry `onmouseover`/`onmouseout` | Hover tooltips and clickable series — new interaction (FR-003, relaxed by Amendment 1) |
| `Legend` supports `CanHideSeries` | Clicking a legend entry can hide a series |
| No `stroke-dasharray` anywhere | Series are distinguished in the plot by colour alone (R5) |

**What is deleted**: `MetricsChart.cs` and `MetricsChartTests.cs` (4 tests), plus the 3 `polyline` assertions in `DashboardComponentTests`. Roughly 7 tests. Amendment 1 permits this and requires each removal to be **recorded in the completion review** — SC-002 still forbids silent removal.

**What is kept around the chart**: the date-range line and the "Not enough data to show trends (30+ days required)" message are the application's own markup, outside the chart component, and are unchanged in wording (FR-002).

**Invariant that survives**: the legend must still read Fitness, Fatigue, Form. `MudChart` derives legend labels from `ChartSeries` names, so passing the existing names preserves the wording FR-002 requires and satisfies FR-013a.

### `MudList`

**Decision**: render the recent activities with `MudList`, and **rewrite** the one `FindAll("li")` test rather than leave it.

`MudList` renders `<div>`s, so `Assert.Empty(list.FindAll("li"))` on an empty list would keep passing — vacuously, having stopped testing anything. That is the trap: green, and meaningless. Amendment 1 and SC-002 as amended both forbid leaving an assertion vacuous, so the test is rewritten to assert the empty state against the new markup — the "Nothing recorded yet." message, which is the behaviour the original test was reaching for anyway.

`MudList` also brings ripple and hover affordances on read-only content. Permitted by the amended FR-003 as inherent to the component, but worth checking in review that a row does not *look* clickable when nothing happens.

## R16: What Amendment 1 does not license

Recorded because an amendment tends to be read more broadly than it was written.

Amendment 1 concedes exactly four things: the chart's dash patterns, the chart's inherent interactions, the chart's geometry code, and the list's element type. It concedes **nothing** about:

- **FR-001 and FR-002** — no computation, stored value, wording or number changes. Both stand unamended.
- **The chart's data** — the same three series over the same range from the same stored metrics.
- **Every other non-colour distinction** — `still settling`, `estimated`, `measured` and the trend judgement remain **text**, not colour or icon. FR-022's carve-out is for the chart plot alone, and this is where an over-broad reading would do the most damage.
- **Silent test removal** — every removed test is named in the completion review.
