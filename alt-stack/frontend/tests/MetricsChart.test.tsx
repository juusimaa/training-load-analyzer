// T091: the chart cases of DashboardComponentTests.cs and InformationPreservationTests.cs.
// The no-handler / no-request-on-hover case renders the whole Dashboard and lives in
// Dashboard.test.tsx.
import { cleanup, render } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { MetricsChart } from "../src/components/MetricsChart";
import { windowed } from "../src/window";
import { expectCoordsClose, historyNames, loadHistory, normalisedText } from "./golden";

afterEach(cleanup);

const long = loadHistory("consecutive-200");
const h3f = loadHistory("H3f");

function chart(window: 30 | 90 | 180, golden = long) {
  return render(
    <MetricsChart
      days={windowed(golden.view.days, window)}
      hasEnoughHistory={golden.view.hasEnoughHistoryForChart}
      windowDays={window}
    />,
  ).container;
}

describe("MetricsChart", () => {
  it("names its window in the heading", () => {
    expect(chart(30).querySelector(".chart-head h2")?.textContent).toBe("Daily load and metrics · last 30 days");
    cleanup();
    expect(chart(30, h3f).querySelector(".chart-head h2")?.textContent).toBe("Daily load and metrics · last 30 days");
  });

  it("draws an svg plot with the reference's view box and accessible name", () => {
    const svg = chart(180).querySelector("svg.plot");

    expect(svg?.getAttribute("viewBox")).toBe("0 0 1000 300");
    expect(svg?.getAttribute("preserveAspectRatio")).toBe("none");
    expect(svg?.getAttribute("role")).toBe("img");
    expect(svg?.getAttribute("aria-label")).toBe("Daily training load with fitness, fatigue and form");
  });

  it("draws the bars behind a zero rule and the lines, Fitness painted last", () => {
    const found = chart(180).querySelector("svg.plot");
    expect(found).not.toBeNull();
    const svg = found!;
    const geometry = long.geometry["180"];

    const bars = [...svg.querySelectorAll("g.bars > rect.load-bar")];
    expect(bars).toHaveLength(geometry.bars.length);
    bars.forEach((bar, i) =>
      expectCoordsClose(["x", "y", "width", "height"].map((a) => bar.getAttribute(a)).join(" "), geometry.bars[i]!.join(" ")),
    );

    const rule = svg.querySelector("line.zero-rule")!;
    expect(rule.getAttribute("x1")).toBe("0");
    expect(rule.getAttribute("x2")).toBe("1000");
    expectCoordsClose(rule.getAttribute("y1")!, geometry.zeroRule!);
    expectCoordsClose(rule.getAttribute("y2")!, geometry.zeroRule!);
    expect(rule.getAttribute("vector-effect")).toBe("non-scaling-stroke");

    const drawn = [...svg.querySelectorAll("rect.load-bar, line.zero-rule, polyline")].map((e) => e.getAttribute("class"));
    expect(drawn.slice(bars.length)).toEqual(["zero-rule", "series-form", "series-fatigue", "series-fitness"]);

    const lines = [...svg.querySelectorAll("polyline")];
    for (const line of lines) {
      expect(line.getAttribute("fill")).toBe("none");
      expect(line.getAttribute("vector-effect")).toBe("non-scaling-stroke");
    }
    expectCoordsClose(lines[2]!.getAttribute("points")!, geometry.plot["Fitness"]!);
    expectCoordsClose(lines[1]!.getAttribute("points")!, geometry.plot["Fatigue"]!);
    expectCoordsClose(lines[0]!.getAttribute("points")!, geometry.plot["Form"]!);
  });

  it("names each line in the legend", () => {
    const legend = chart(90).querySelector(".chart-head .legend");

    expect([...(legend?.children ?? [])].map((s) => [s.className, s.textContent])).toEqual([
      ["legend-fitness", "Fitness"],
      ["legend-fatigue", "Fatigue"],
      ["legend-form", "Form"],
    ]);
  });

  it("renders one hover slot per day, each with its readout already in place", () => {
    const container = chart(30);
    const days = windowed(long.view.days, 30);
    const geometry = long.geometry["30"];
    const slots = [...container.querySelectorAll(".plot-frame > .hover-layer > .day")];

    expect(slots).toHaveLength(30);
    slots.forEach((slot, i) => {
      const day = days[i]!;
      const expected = geometry.slots[i]!;
      expect(slot.querySelectorAll(".point")).toHaveLength(3);
      expect(slot.querySelector(".guide")).not.toBeNull();
      expect(slot.querySelector(".readout-day")?.textContent).toBe(day.day);
      expect([...slot.querySelectorAll(".readout-label")].map((l) => l.textContent)).toEqual(["Fitness", "Fatigue", "Form", "Load"]);
      expect([...slot.querySelectorAll(".readout-value")].map((v) => v.textContent)).toEqual([
        day.display.fitness, day.display.fatigue, day.display.form, day.display.load,
      ]);
      expect(slot.querySelector(".readout")?.classList.contains("opens-left")).toBe(expected.opensLeft);
    });
  });

  it("positions each slot, its bar emphasis and its points with percentages only", () => {
    const container = chart(30);
    const geometry = long.geometry["30"];
    const slots = [...container.querySelectorAll<HTMLElement>(".hover-layer > .day")];

    expect(slots).toHaveLength(30);
    slots.forEach((slot, i) => {
      const expected = geometry.slots[i]!;
      expectCoordsClose(slot.style.left.replace("%", ""), expected.left);
      expectCoordsClose(slot.style.width.replace("%", ""), expected.width);
      const focus = slot.querySelector<HTMLElement>(".bar-focus");
      expect(focus === null).toBe(expected.barTop === null);
      if (focus) {
        expectCoordsClose(focus.style.top.replace("%", ""), expected.barTop!);
        expectCoordsClose(focus.style.height.replace("%", ""), expected.barHeight!);
      }
      const points = [...slot.querySelectorAll<HTMLElement>(".point")];
      expect(points.map((p) => p.getAttribute("class"))).toEqual(["point series-fitness", "point series-fatigue", "point series-form"]);
      points.forEach((p, j) => expectCoordsClose(p.style.top.replace("%", ""), expected.points[j]![2]));
    });
    for (const styled of container.querySelectorAll<HTMLElement>("[style]")) {
      const properties = [...Array(styled.style.length).keys()].map((k) => styled.style.item(k));
      for (const property of properties) expect(["left", "width", "top", "height"]).toContain(property);
      expect(styled.getAttribute("style")).not.toContain(",");
    }
  });

  it("marks a rest day with no bar emphasis", () => {
    const days = windowed(long.view.days, 30).map((d, i) =>
      i === 0 ? { ...d, load: 0, display: { ...d.display, load: "0.0" } } : d,
    );
    const { container } = render(<MetricsChart days={days} hasEnoughHistory windowDays={30} />);
    const slots = container.querySelectorAll(".hover-layer > .day");

    expect(slots).toHaveLength(30);
    expect(slots[0]!.querySelector(".bar-focus")).toBeNull();
    expect(slots[1]!.querySelector(".bar-focus")).not.toBeNull();
  });

  it("labels the axis with six ticks", () => {
    const ticks = [...chart(180).querySelectorAll(".chart-axis > span")].map((s) => s.textContent);

    expect(ticks).toEqual(long.geometry["180"].ticks);
  });

  it("explains itself, and draws nothing, without enough history", () => {
    const container = chart(180, h3f);

    expect(container.querySelector("p.chart-empty")?.textContent).toBe("Not enough data to show trends (30+ days required)");
    expect(container.querySelector("svg")).toBeNull();
    expect(container.querySelector(".hover-layer")).toBeNull();
    expect(container.querySelector(".chart-axis")).toBeNull();
  });

  const windows = [30, 90, 180] as const;
  const cases = historyNames().flatMap((name) => windows.map((w) => [name, w] as const));

  it.each(cases)("renders the golden text of %s for the %i-day window", (name, w) => {
    const golden = loadHistory(name);

    expect(normalisedText(chart(w, golden))).toBe(golden.renderedText[`chart${w}`]);
  });
});
