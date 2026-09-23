// T089: the MetricRow cases of DashboardComponentTests.cs and InformationPreservationTests.cs.
import { cleanup, render } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { MetricRow } from "../src/components/MetricRow";
import type { Current } from "../src/types";
import { historyNames, loadHistory, normalisedText } from "./golden";

afterEach(cleanup);

const h3f = loadHistory("H3f").view;

function inOrder(text: string, parts: string[]) {
  let from = 0;
  for (const part of parts) {
    const at = text.indexOf(part, from);
    expect(at, `"${part}" after position ${from} in "${text}"`).toBeGreaterThanOrEqual(0);
    from = at + part.length;
  }
}

describe("MetricRow", () => {
  it("shows the labels, values and qualifiers in order", () => {
    const { container } = render(<MetricRow current={h3f.current} week={h3f.week} />);

    inOrder(normalisedText(container), [
      "Fitness", "24.3", "still settling", "partly estimated",
      "Fatigue", "68.2", "still settling", "partly estimated",
      "Form", "-43.8", "still settling", "partly estimated",
      "This week", "417.0",
    ]);
  });

  it("shows four figures as peers, colouring only Fitness and Fatigue", () => {
    const { container } = render(<MetricRow current={h3f.current} week={h3f.week} />);

    expect(container.querySelectorAll(".metrics .figure .tile-value")).toHaveLength(4);
    expect(container.querySelector(".series-fitness.tile-value")?.textContent).toBe("24.3");
    expect(container.querySelector(".series-fatigue.tile-value")?.textContent).toBe("68.2");
    expect(container.querySelectorAll(".series-form")).toHaveLength(0);
  });

  it("words every qualifier as a tag", () => {
    const current: Current = {
      fitness: { value: "2.8", qualifiers: ["still settling"] },
      fatigue: { value: "0.0", qualifiers: ["estimated"] },
      form: { value: "2.8", qualifiers: ["partly estimated"] },
    };
    const { container } = render(<MetricRow current={current} week={h3f.week} />);
    const tags = [...container.querySelectorAll(".notes .tag.tag-neutral")].map((t) => t.textContent);

    expect(tags).toEqual(["still settling", "estimated", "partly estimated"]);
  });

  it("captions the week with its change, percent and judgement", () => {
    const { container } = render(<MetricRow current={h3f.current} week={h3f.week} />);

    expect(normalisedText(container.querySelector(".note"))).toBe("+57.0 (+16%)");
    expect(container.querySelector(".figure:last-child .tag.tag-neutral")?.textContent).toBe("Week in progress");
  });

  it("shows a dash when there is no trend, and the week's total still stands", () => {
    const { container } = render(<MetricRow current={h3f.current} week={{ points: "480.0", trend: null }} />);
    const week = container.querySelector(".figure:last-child");

    expect(normalisedText(week)).toBe("This week 480.0 —");
    expect(week?.querySelector(".note")?.textContent).toBe("—");
    expect(week?.querySelector(".tag")).toBeNull();
  });

  it("keeps four positions of em dashes when there is nothing to show", () => {
    const empty = loadHistory("empty").view;
    const { container } = render(<MetricRow current={empty.current} week={empty.week} />);
    const values = [...container.querySelectorAll(".tile-value")].map((v) => v.textContent?.trim());

    expect(values).toEqual(["—", "—", "—", "—"]);
    expect(normalisedText(container)).not.toContain("0.0");
  });

  it.each(historyNames())("renders the golden text of %s", (name) => {
    const { view, renderedText } = loadHistory(name);
    const { container } = render(<MetricRow current={view.current} week={view.week} />);

    expect(normalisedText(container)).toBe(renderedText.metricRow);
  });
});
