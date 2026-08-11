import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { RouterProvider } from "@tanstack/react-router";

import "@fontsource/inter/400.css";
import "@fontsource/inter/500.css";
import "@fontsource/inter/600.css";
import "@fontsource/inter/700.css";
import "@fontsource/amiri/400.css";
import "@fontsource/amiri/700.css";
import "./styles.css";
import "@/state/appStore";

import { getRouter } from "./router";
import { automationEnabled } from "./automation/config";
import { preloadBrowserBackend } from "./native/browserAppBackend";

const root = document.getElementById("app");

if (!root) {
  throw new Error("Missing #app root element.");
}

async function startApplication() {
  if (!window.mauiWebber && window.location.protocol !== "file:" && window.location.hostname !== "app.prayadfree.local") {
    await preloadBrowserBackend();
  }

  createRoot(root!).render(
    <StrictMode>
      <RouterProvider router={getRouter()} />
    </StrictMode>,
  );

  if (import.meta.env.VITE_PRAY_AUTOMATION === "true" && automationEnabled()) {
    const { startAutomationRun } = await import("./automation/runner");
    await startAutomationRun();
  }
}

void startApplication();
