// Port of Components/Dashboard/MetricsChartView.razor: daily load and the three metrics over the
// selected window (008 FR-005, FR-006), with the same markup and classes so the copied stylesheet
// applies unchanged (dashboard-ui.md §4).
//
// No stroke or fill colour is written here; every one resolves from a class. The hover readout is
// rendered once for every day and revealed by CSS :hover, so there is no pointer handler, no state
// and no request per mouse movement (009 FR-015). The only inline styles are the slots'
// percentages, as in the reference.
//
// The {" "} separators stand where the Razor markup has whitespace between elements, so the
// rendered text matches the reference's exactly (009 SC-002).
import { axisTicks, hoverSlots, loadBars, plot, viewBox, zeroRule } from "../chart/geometry";
import type { Day } from "../types";
import "./MetricsChart.css";

export interface MetricsChartProps {
  /** The days to plot, already sliced to the selected window. */
  days: Day[];
  /** False under 30 days of stored history: a property of the history, never of the window. */
  hasEnoughHistory: boolean;
  windowDays: number;
}

const Width = 1000;
const Height = 300;
const AxisTickCount = 6;

function Plot({ days }: { days: Day[] }) {
  const zero = zeroRule(days, Width, Height);
  const slots = hoverSlots(days);
  return (
    <div className="plot-frame">
      {" "}
      <svg
        className="plot"
        viewBox={viewBox(Width, Height)}
        preserveAspectRatio="none"
        role="img"
        aria-label="Daily training load with fitness, fatigue and form"
      >
        {/* Back to front: the bars are a backdrop, the rule a reference, the lines the message. */}
        <g className="bars">
          {loadBars(days, Width, Height).map((bar, i) => (
            <rect key={i} className="load-bar" x={bar.x} y={bar.y} width={bar.width} height={bar.height} />
          ))}
        </g>
        {zero !== null && (
          <line className="zero-rule" x1="0" y1={zero} x2={String(Width)} y2={zero} vectorEffect="non-scaling-stroke" />
        )}
        {/* Reversed, so Fitness is painted last and sits on top of the other two. */}
        {plot(days, Width, Height)
          .reverse()
          .map((series) => (
            <polyline
              key={series.cssClass}
              className={series.cssClass}
              fill="none"
              points={series.points}
              vectorEffect="non-scaling-stroke"
            />
          ))}
      </svg>
      <div className="hover-layer">
        {slots.map((slot) => (
          <div key={slot.day} className="day" style={{ left: `${slot.left}%`, width: `${slot.width}%` }}>
            {slot.barTop !== null && <i className="bar-focus" style={{ top: `${slot.barTop}%`, height: `${slot.barHeight}%` }}></i>}
            <i className="guide"></i>
            {slot.points.map((point) => (
              <i key={point.cssClass} className={`point ${point.cssClass}`} style={{ top: `${point.top}%` }}></i>
            ))}
            <div className={`readout ${slot.opensLeft ? "opens-left" : ""}`}>
              <span className="readout-day">{slot.day}</span>
              {slot.points.map((point) => (
                <Reading key={point.cssClass} label={point.label} value={point.value} labelClass={`legend-${point.label.toLowerCase()}`} />
              ))}
              {slot.load !== null && <Reading label="Load" value={slot.load} />}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function Reading({ label, value, labelClass }: { label: string; value: string; labelClass?: string }) {
  return (
    <>
      <span className={labelClass ? `readout-label ${labelClass}` : "readout-label"}>{label}</span>{" "}
      <span className="readout-value">{value}</span>
    </>
  );
}

export function MetricsChart({ days, hasEnoughHistory, windowDays }: MetricsChartProps) {
  return (
    <section className="chart">
      <div className="chart-head">
        <h2>Daily load and metrics · last {windowDays} days</h2>{" "}
        {/* The legend names every series; the dash patterns answer "which line" a second way. */}
        <div className="legend">
          <span className="legend-fitness">Fitness</span>{" "}
          <span className="legend-fatigue">Fatigue</span>{" "}
          <span className="legend-form">Form</span>
        </div>
      </div>
      {!hasEnoughHistory ? (
        <p className="chart-empty">Not enough data to show trends (30+ days required)</p>
      ) : (
        <>
          <Plot days={days} />
          <div className="chart-axis">
            {axisTicks(days, AxisTickCount).map((tick, i) => (
              <span key={i}>{tick}</span>
            ))}
          </div>
        </>
      )}
    </section>
  );
}
