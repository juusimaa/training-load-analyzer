# Data Model: Material Design Visual Refresh

**Feature**: 007-material-design-ui

## No persisted data

This feature introduces **no** entity, no table, no migration, no domain type and no change to any existing one. FR-001 forbids changing computation or stored data, and the implementation is confined to the theme, component markup and one added package.

Stated explicitly because the absence is itself a design constraint: if planning or implementation produces a migration, a new record type or a change under `src/TrainingLoadAnalyzer.Domain`, that is a scope breach, not progress.

Entities this feature **reads through unchanged**, all owned by features 001–006: `DashboardView`, `DailyTrainingMetrics`, `WeeklyTrainingLoad`, `WeeklyLoadTrend`, `RecentActivity`, `SyncStatus`, `LoadBasis`, `LoadProvenance`, `TrendClassification`. None of their shapes, names or values change.

## The structured model this feature does introduce

### Theme model

A three-level hierarchy, defined once in `Theme/TrainingLoadTheme.cs` and detailed in [contracts/design-tokens.md](contracts/design-tokens.md):

```
MudTheme
├── PaletteLight / PaletteDark   (identical slot sets)
├── Typography                   (5 roles used of Mud's set)
└── LayoutProperties             (radius, container width)

ChartPalette                     (3 series × 2 schemes — passed to MudChart; MudTheme has no slot)
```

Rules that hold over this model, each one testable:

| Rule | Enforced by |
|------|-------------|
| Both palettes populate the identical set of slots | Set-equality test over the two palette objects |
| No colour literal appears outside the theme files | Literal-scan test (R7) |
| Every declared contrast pair meets its minimum in both palettes | Contrast test reading the theme object (R6) |

### Surface × state matrix

The verification surface of this feature. Every cell must render in both schemes (FR-028, SC-003, SC-010).

| Surface | States |
|---------|--------|
| Dashboard | loading, populated, empty-unconnected, empty-connected, data-unavailable |
| Sync panel | never synced, running, succeeded, failed, reconnection-required |
| Metric tile | value present, value absent (`—`), still-settling, estimated, partly-estimated, two qualifiers at once |
| Weekly panel | trend present, no previous week, week-in-progress |
| Chart | plotted, insufficient history |
| Recent activities | empty, 1 row, 7 rows, long activity type |
| Error notice | `#blazor-error-ui` shown |
| Standalone pages | `/Error`, not-found |

8 surfaces × 2 schemes is the SC-003 / SC-010 checklist. The rows with more than one state are where the edge cases in the spec live.

### State transitions

Only one, and it is not application state: the **scheme transition**, driven by the device preference and observed automatically by `MudThemeProvider` (FR-026, R3).

```
light ──(OS preference → dark)──▶ dark
dark  ──(OS preference → light)─▶ light
```

Held across this transition, per FR-028 and SC-011: no reload, no loss or change of displayed content, and no stored preference or toggle (FR-027). The provider's `ObserveSystemDarkModeChange` defaults to `true`, so the browser's `matchMedia` listener invokes `SystemDarkModeChangedAsync` and the palette swaps — no code of ours participates.

One wrinkle this model must acknowledge: the transition also fires **once on every first load**, because prerendering paints before interop resolves. `IsDarkMode` starts `false`, so a dark-mode user sees light briefly. This is accepted, with the alternatives weighed, in [research.md](research.md) R14.
