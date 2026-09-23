// Port of Components/Dashboard/MetricRow.razor: the four display figures as one row of peers
// (008 FR-004, FR-008). Every string arrives formatted from the API.
//
// The {" "} separators stand where the Razor markup has whitespace between elements, so the
// rendered text matches the reference's exactly (009 SC-002); adjacent tags have none.
import type { Current, Figure, Week } from "../types";
import "./MetricRow.css";

export interface MetricRowProps {
  current: Current | null;
  week: Week;
}

/** Display.Missing: nothing to show is a dash, never a confident zero. */
const Missing = "—";

function Qualifiers({ figure }: { figure: Figure | undefined }) {
  return (
    <div className="notes">
      {figure?.qualifiers.map((qualifier) => (
        <span key={qualifier} className="tag tag-neutral">
          {qualifier}
        </span>
      ))}
    </div>
  );
}

function MetricFigure({ label, figure, valueClass }: { label: string; figure: Figure | undefined; valueClass: string }) {
  return (
    <div className="figure">
      <div className="kicker">{label}</div>{" "}
      <div className={valueClass}>{figure?.value ?? Missing}</div>{" "}
      <Qualifiers figure={figure} />
    </div>
  );
}

export function MetricRow({ current, week }: MetricRowProps) {
  const trend = week.trend;
  return (
    <div className="metrics">
      <MetricFigure label="Fitness" figure={current?.fitness} valueClass="tile-value series-fitness" />{" "}
      <MetricFigure label="Fatigue" figure={current?.fatigue} valueClass="tile-value series-fatigue" />{" "}
      <MetricFigure label="Form" figure={current?.form} valueClass="tile-value" />{" "}
      <div className="figure">
        <div className="kicker">This week</div>{" "}
        <div className="tile-value">{week.points}</div>{" "}
        <div className="notes">
          {trend === null ? (
            <span className="note">{Missing}</span>
          ) : (
            <>
              <span className="note">{`${trend.change} (${trend.percent})`}</span>{" "}
              <span className="tag tag-neutral">{trend.judgement}</span>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
