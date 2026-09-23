/// <reference types="vitest/config" />
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Development: the browser only ever sees :5173, and the backend builds the Strava callback URI
// from the Host header it is given, so the proxy keeps the original Host (research R15).
const backend = { target: "http://localhost:8000", changeOrigin: false };

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": backend,
      "/connect": backend,
      "/strava/callback": backend,
    },
  },
  test: {
    environment: "jsdom",
    include: ["tests/**/*.test.{ts,tsx}"],
  },
});
