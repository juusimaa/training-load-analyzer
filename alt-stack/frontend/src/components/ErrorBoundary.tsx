import { Component, type ReactNode } from "react";

export interface ErrorBoundaryProps {
  children: ReactNode;
  reload?: () => void;
}

export class ErrorBoundary extends Component<ErrorBoundaryProps> {
  override render() {
    return this.props.children;
  }
}
