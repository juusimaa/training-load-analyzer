// Port of Components/Pages/NotFound.razor: the same visual language as everything else, so a
// wrong address does not land on a page that looks like another application (008 FR-010).
import { useEffect } from "react";
import "./NotFound.css";

export function NotFound() {
  useEffect(() => {
    document.title = "Not found";
  }, []);

  return (
    <main className="page">
      <h1>Not Found</h1>{" "}
      <div className="state">
        <p>Sorry, the content you are looking for does not exist.</p>
      </div>
    </main>
  );
}
