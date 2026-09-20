#!/usr/bin/env bash
# Feature 008 compliance checks (tasks.md T066). Every check prints its name and either "ok" or
# the offending lines, and the script exits non-zero if any check finds something.
#
# These are the claims a test cannot make: that something is ABSENT from the repository. A unit
# test can only assert over what it can reach, and "MudBlazor is gone" is a statement about every
# file, including the ones no test renders.
set -uo pipefail
cd "$(dirname "$0")/.."

W=src/TrainingLoadAnalyzer.Web
T=tests/TrainingLoadAnalyzer.Web.Tests
failed=0

check() {
  local name="$1"; shift
  local out
  out="$("$@" 2>/dev/null)"
  if [ -z "$out" ]; then
    printf '  ok    %s\n' "$name"
  else
    printf '  FAIL  %s\n' "$name"
    printf '%s\n' "$out" | sed 's/^/          /'
    failed=1
  fi
}

# Comments explaining what was removed, and why, are kept deliberately — the history is the reason
# several of these decisions look the way they do, and a test in ReconnectModalTests asserts the
# absence of "--mud-palette" by naming it. So every pattern below is CODE-SHAPED: a var() call, a
# declaration, an element, an attribute. Prose that mentions MudBlazor does not match, and is not
# supposed to.
# -e, so a pattern starting with "--" is read as a pattern and not as an option terminator.
src_grep() {
  grep -rn -e "$1" "$W" "$T" \
    --include="*.cs" --include="*.razor" --include="*.css" --include="*.csproj" \
    --exclude-dir=obj --exclude-dir=bin
}

echo "Feature 008 compliance"

# T060-T064 / R1 - the dependency is gone, not merely unused.
check "R1: no MudBlazor package reference"       src_grep 'PackageReference Include="MudBlazor'
check "R1: no MudBlazor namespace imported"      src_grep '^@using MudBlazor\|^using MudBlazor'
check "R1: no MudBlazor service registration"    src_grep 'AddMudServices'
check "R1: no MudBlazor component in markup"     src_grep '<Mud[A-Z]'
check "R1: no mud- class in markup or CSS"       src_grep 'class="[^"]*\bmud-\|^\s*\.mud-'
check "R1: no MudBlazor palette variable in use" \
  grep -rn -e 'var(--mud-palette\|var(--mud-elevation' "$W" \
    --include="*.css" --include="*.razor" --exclude-dir=obj --exclude-dir=bin
check "R1: no MudBlazor asset link"              src_grep '_content/MudBlazor'
check "R1: the MudChart JSInterop fake is gone"  src_grep 'MudChartBounds'
check "T063: app.css carries no dead .mud-button-root rule" \
  grep -n 'mud-button-root' "$W/wwwroot/app.css"

# T063 - the two theme classes the library needed.
check "T063: the MudTheme and MudChart palette classes are deleted" \
  ls "$W/Theme/TrainingLoadTheme.cs" "$W/Theme/ChartPalette.cs"

# R5 - colour reaches the plot through a class, never an SVG presentation attribute. An attribute
# would fail ColourDisciplineTests AND, worse, could not respond to prefers-color-scheme, leaving
# the chart as the one region that ignores a dark device. fill="none" is not a colour.
check "R5: no SVG colour literal in any .razor" \
  grep -rnE '(stroke|fill)="[^"]*(#|rgb|hsl)' "$W" --include="*.razor" --exclude-dir=obj --exclude-dir=bin

# R2 - the vendored sheet lives under a Theme path segment, which ColourDisciplineTests exempts by
# an ordinal string match. A lowercase theme/ would pass on this filesystem and fail the test.
check "R2: the vendored stylesheet is under a 'Theme' segment" \
  sh -c "test -f $W/wwwroot/Theme/broadsheet.css || echo 'wwwroot/Theme/broadsheet.css is missing'"

# R2 - a subset, still. The print-treatment machinery resolves filters against an SVG defs file
# this application never ships, and would be inert weight (Principle III).
check "R2: the print-treatment rules stayed out of the vendored subset" \
  grep -nE '^\.(halftone|cmyk)|^\s*--color-process-yellow:' "$W/wwwroot/Theme/broadsheet.css"

# Principle II - this feature is presentation-only. Neither library should have changed at all.
for project in Domain Infrastructure; do
  check "Principle II: src/TrainingLoadAnalyzer.$project unchanged since main" \
    git diff --stat main -- "src/TrainingLoadAnalyzer.$project"
done

# Principle III - the dependencies research declined, and the ones 006 declined before it.
check "Principle III: no declined dependency crept in" \
  grep -rnE "ChartJs|ApexCharts|Plotly|Blazorise|Moq|NSubstitute|FakeItEasy|UseInMemoryDatabase|Playwright|Selenium" \
    --include="*.cs" --include="*.razor" --include="*.csproj" --exclude-dir=obj --exclude-dir=bin "$W" "$T"

# R3 - the appearance is the platform's media query now: no JS round-trip, no cascaded flag, no
# stored preference and no toggle. Scoped to code, because the media query itself lives in the
# vendored stylesheet and is exactly what is supposed to be there.
check "R3: no JavaScript scheme detection survives" \
  grep -rnE 'GetSystemDarkModeAsync\(|<MudThemeProvider|Name="IsDarkMode"|localStorage|sessionStorage' "$W" "$T" \
    --include="*.cs" --include="*.razor" --exclude-dir=obj --exclude-dir=bin
check "R3: the appearance is a media query in the vendored sheet" \
  sh -c "grep -q 'prefers-color-scheme: dark' $W/wwwroot/Theme/broadsheet.css || echo 'broadsheet.css declares no dark media query'"

echo
if [ "$failed" -eq 0 ]; then
  echo "All checks passed."
else
  echo "One or more checks FAILED."
fi
exit "$failed"
