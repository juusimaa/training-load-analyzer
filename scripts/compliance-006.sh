#!/usr/bin/env bash
# Feature 006 compliance checks (tasks.md Phase 8). Every check prints its name and either
# "ok" or the offending lines, and the script exits non-zero if any check finds something.
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

echo "Feature 006 compliance"

# T115/T116 - Principle II. The two libraries end the feature byte-identical.
for project in Domain Infrastructure; do
  check "Principle II: src/TrainingLoadAnalyzer.$project unchanged since main" \
    git diff --stat main -- "src/TrainingLoadAnalyzer.$project"
done

# T117 / C85 - no Strava TYPE in the read model. Checked by imports rather than by the word
# "Strava": DashboardView.IsStravaConnected is a bool, and the dashboard legitimately knows
# whether the athlete's account is connected (FR-017 says so in those words). A Strava type
# cannot appear in a file that imports no Strava namespace.
check "C85: no Strava namespace imported into Features/Dashboard" \
  grep -rn "using TrainingLoadAnalyzer.Infrastructure.Strava\|using TrainingLoadAnalyzer.Infrastructure.Sync" "$W/Features/Dashboard"
check "C85: the read model imports only the domain" \
  grep -hn "^using " "$W/Features/Dashboard/DashboardView.cs" "$W/Features/Dashboard/RecentActivity.cs" \
    --include=* -e "using TrainingLoadAnalyzer.Infrastructure"

# T118 / C102 - components take a view and a status, and reach for nothing.
check "C102: components do not reach past the reader" \
  grep -rn "ImportDbContext\|ActivityStore\|StravaActivitySync\|TimeProvider" "$W/Components"

# T119 / C103 - no training arithmetic in a component.
check "C103: components compute no training figures" \
  grep -rn "TrainingLoadAggregator\|TrainingMetricsCalculator\|TrainingLoadTrendCalculator" "$W/Components"

# T120 / Principle III - the dependencies research declined. AddInMemoryCollection is
# IConfiguration's in-memory provider and is NOT EF Core's InMemory database provider, which is
# what 005 R8 rejected; the pattern names the database one specifically.
check "Principle III: no declined dependency crept in" \
  grep -rnE "ChartJs|ApexCharts|Plotly|Blazorise|Moq|NSubstitute|FakeItEasy|UseInMemoryDatabase|EntityFrameworkCore.InMemory|Playwright|Selenium" \
    --include="*.cs" --include="*.razor" --include="*.csproj" "$W" "$T"

# T122a / FR-012a - a reload is a new circuit, so the state is the application's.
check "FR-012a: no state lives in the browser" \
  grep -rni "localStorage\|sessionStorage\|IJSRuntime" "$W"

# T122 / C76, C78 - no credential on a page or in a tracked settings file.
check "C76: no credential in a component or in appsettings.json" \
  grep -rni "client_secret\|AccessToken\|RefreshToken" "$W/Components" "$W/appsettings.json"

# T123 / SC-007 - no third-party JavaScript. Blazor's own blazor.web.js and the template's
# ReconnectModal script are framework assets, not a charting library; they are what research R8
# declined to add to.
check "SC-007: no JavaScript beyond the framework's own" \
  bash -c "grep -rn '<script' '$W' --include='*.razor' | grep -v 'blazor.web.js' | grep -v 'ReconnectModal.razor.js'"

exit "$failed"
