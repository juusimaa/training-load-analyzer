// Port of Components/Pages/Error.razor, reached only by navigating to /Error (Amendment 1(c)).
// No Request ID line: the reference omits it whenever it has no identifier, and the SPA never has
// one. The "Development Mode" paragraph is not ported: it names another stack's configuration.
import { useEffect } from "react";
import "./ErrorPage.css";

export function ErrorPage() {
  useEffect(() => {
    document.title = "Error";
  }, []);

  return (
    <main className="page">
      <h1 className="alarm">Error.</h1>{" "}
      <div className="state state-alarm">
        <h2 className="alarm">An error occurred while processing your request.</h2>
      </div>
    </main>
  );
}
