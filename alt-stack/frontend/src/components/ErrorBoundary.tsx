// The React stand-in for MainLayout.razor's #blazor-error-ui (research R4): the same words, the
// same markup and the copied rules, now under .error-ui (theme/layout.css).
//
// The copied rule hides the notice with display:none; in the reference, blazor.web.js reveals it
// by setting style.display = "block" on an unhandled error and hides it again on dismiss. An
// inline style would break the rule that inline styles carry only the chart's positions, so the
// reveal is a class instead, defined in ErrorBoundary.css (the copied stylesheet is not edited).
import { Component, type ErrorInfo, type ReactNode } from "react";
import "./ErrorBoundary.css";

export interface ErrorBoundaryProps {
  children: ReactNode;
  /** What Reload does; location.reload() unless a test passes its own. */
  reload?: () => void;
}

interface ErrorBoundaryState {
  failed: boolean;
  dismissed: boolean;
}

export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  override state: ErrorBoundaryState = { failed: false, dismissed: false };

  static getDerivedStateFromError(): Partial<ErrorBoundaryState> {
    return { failed: true };
  }

  override componentDidCatch(error: Error, info: ErrorInfo) {
    console.error(error, info.componentStack);
  }

  override render() {
    if (!this.state.failed) return this.props.children;

    const reload = this.props.reload ?? (() => window.location.reload());
    return (
      <div className={this.state.dismissed ? "error-ui" : "error-ui error-ui-shown"} data-nosnippet="">
        An unhandled error has occurred.{" "}
        <a
          href="."
          className="reload"
          onClick={(event) => {
            event.preventDefault();
            reload();
          }}
        >
          Reload
        </a>{" "}
        <span className="dismiss" onClick={() => this.setState({ dismissed: true })}>
          🗙
        </span>
      </div>
    );
  }
}
