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
import { automationEnabled, latchAutomationRuntime } from "./automation/config";
import { preloadBrowserBackend } from "./native/browserAppBackend";
import { completeWebBoot, reportWebBoot } from "./native/webBoot";

const root = document.getElementById("app");

if (!root) {
  throw new Error("Missing #app root element.");
}

async function startApplication() {
  if (!window.mauiWebber && window.location.protocol !== "file:" && window.location.hostname !== "app.prayadfree.local") {
    reportWebBoot("loading", undefined, "Initializing shared calculation engine");
    await preloadBrowserBackend();
  }

  const shouldRunAutomation = import.meta.env.VITE_PRAY_AUTOMATION === "true" && automationEnabled();
  if (shouldRunAutomation) {
    latchAutomationRuntime();
  }

  createRoot(root!).render(
    <StrictMode>
      <RouterProvider router={getRouter()} />
    </StrictMode>,
  );
  completeWebBoot();

  if (shouldRunAutomation) {
    const { startAutomationRun } = await import("./automation/runner");
    await startAutomationRun();
  }
}

void startApplication().catch((error) => {
  const message = error instanceof Error ? error.message : String(error);
  console.error("Application startup failed", error);
  reportWebBoot(navigator.onLine ? "failed" : "offline", undefined, message);
});
