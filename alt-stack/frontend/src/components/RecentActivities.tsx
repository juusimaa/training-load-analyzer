// Port of Components/Dashboard/RecentActivityList.razor: the most recent sessions, newest first,
// as a plain table (008 FR-007, FR-018).
//
// React may not put whitespace text directly inside <table>/<tr>, so the separators the Razor
// markup has between cells are carried as a trailing space inside each cell instead. The rendered
// text is the same (009 SC-002), and the space is invisible at the end of a cell.
import type { Recent } from "../types";
import "./RecentActivities.css";

export interface RecentActivitiesProps {
  activities: Recent[];
}

export function RecentActivities({ activities }: RecentActivitiesProps) {
  return (
    <section className="recent">
      <h2>Recent activities</h2>
      {activities.length === 0 ? (
        <p className="recent-empty">Nothing recorded yet.</p>
      ) : (
        <table className="table">
          <thead>
            <tr>
              <th>Day </th>
              <th>Type </th>
              <th>Moving time </th>
              <th>Basis </th>
              <th className="load">Load </th>
            </tr>
          </thead>
          <tbody>
            {activities.map((activity, index) => {
              const measured = activity.provenance === "measured";
              return (
                <tr key={index} className="recent-row">
                  <td className="recent-day num">{activity.day} </td>
                  <td className="recent-type">{activity.type} </td>
                  <td className="recent-duration num">{activity.movingTime} </td>
                  {/* FR-018 as content, not as styling: the tag carries its word. */}
                  <td>
                    <span className={`tag ${measured ? "tag-accent" : "tag-outline"}`}>{measured ? "measured" : "estimated"}</span>{" "}
                  </td>
                  <td className="recent-load num load">{activity.load}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </section>
  );
}
