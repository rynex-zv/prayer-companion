import { automationEnabled, automationPlatform, latchAutomationRuntime } from "./config";
import { collectRpcTimings, ScenarioContext, waitFor, waitForAutomationApi } from "./harness";
import { buildFailedMarkdown, buildPassedMarkdown, persistReports } from "./report";
import { automationScenarios } from "./scenarios";
import type { AutomationRunResult, ScenarioResult } from "./types";
import { executeCommand } from "@/client/applicationClient";
import { appClient } from "@/client/appClient";
import { setOnboardingCompleted } from "@/state/appStore";
import { describeRuntimeValue, observeRuntimeValue } from "./runtimeDefects";

let started = false;

export async function startAutomationRun(): Promise<void> {
  if (started || window.__prayAutomationRunnerStarted === true || !automationEnabled()) return;
  started = true;
  window.__prayAutomationRunnerStarted = true;
  latchAutomationRuntime();
  document.body.dataset.automationStatus = "running";
  const startedAt = new Date().toISOString();
  const runId = `${automationPlatform()}-${startedAt.replace(/[:.]/g, "-")}`;
  const collector = collectRpcTimings();
  const runtimeDefects: string[] = [];
  const externalRuntimeWarnings: string[] = [];
  const observe = (value: unknown, sourceUrl?: string) => {
    const observation = observeRuntimeValue(value, sourceUrl);
    (observation.source === "application" ? runtimeDefects : externalRuntimeWarnings).push(observation.message);
  };
  const originalConsoleError = console.error;
  console.error = (...args: unknown[]) => {
    observe(args.map(describeRuntimeValue).join(" "));
    originalConsoleError(...args);
  };
  const onWindowError = (event: ErrorEvent) => observe(event.error?.stack ?? event.message, event.filename);
  const onUnhandledRejection = (event: PromiseRejectionEvent) => observe(`Unhandled rejection: ${describeRuntimeValue(event.reason)}`);
  window.addEventListener("error", onWindowError);
  window.addEventListener("unhandledrejection", onUnhandledRejection);
  const passed: ScenarioResult[] = [];
  const failed: ScenarioResult[] = [];

  try {
    await waitForAutomationApi();
    await waitFor(() => window.prayerCompanion?.isReady() === true, "Application bootstrap did not become ready", 15000);
    const requestedScenario = window.__prayAutomationScenario ?? new URLSearchParams(window.location.search).get("automationScenario");
    const scenarios = requestedScenario
      ? automationScenarios.filter((scenario) => scenario.id === requestedScenario)
      : automationScenarios;
    if (requestedScenario && scenarios.length === 0) throw new Error(`Unknown automation scenario: ${requestedScenario}`);
    for (const [scenarioIndex, scenario] of scenarios.entries()) {
      await prepareDeterministicLocation();
      if (scenario.id !== "01-page-contract") {
        const completion = await executeCommand("onboarding.complete");
        if (!completion.ok) throw new Error(`Could not prepare completed onboarding state: ${completion.error}`);
        setOnboardingCompleted(true);
      }
      const context = new ScenarioContext(collector.timings, scenario.id === "01-page-contract");
      const runtimeDefectStart = runtimeDefects.length;
      const externalWarningStart = externalRuntimeWarnings.length;
      const scenarioStarted = performance.now();
      let error: unknown;
      try {
        await withTimeout(scenario.run(context), 180_000, `${scenario.id} exceeded its 180 second safety timeout`);
      } catch (caught) {
        error = caught;
      } finally {
        const timingFailure = context.finalizeTimings();
        if (!error && timingFailure) error = new Error(timingFailure);
        const scenarioRuntimeDefects = runtimeDefects.slice(runtimeDefectStart);
        for (const warning of externalRuntimeWarnings.slice(externalWarningStart)) {
          context.warnings.push(`External browser extension runtime warning: ${warning}`);
        }
        if (!error && scenarioRuntimeDefects.length > 0) {
          error = new Error(`Runtime error: ${scenarioRuntimeDefects.join(" | ")}`);
        }
      }

      const result: ScenarioResult = {
        id: scenario.id,
        name: scenario.name,
        documentation: scenario.documentation,
        passed: !error,
        durationMs: Math.round(performance.now() - scenarioStarted),
        assertions: context.assertions,
        steps: [...context.steps],
        warnings: [...context.warnings],
        failedAssertion: error instanceof Error ? error.message : error ? String(error) : undefined,
        stack: error instanceof Error ? error.stack : undefined,
      };
      (error ? failed : passed).push(result);
      console.info(`[pray.automation] ${JSON.stringify({ event: error ? "scenario-failed" : "scenario-passed", id: scenario.id, durationMs: result.durationMs, assertion: result.failedAssertion })}`);
    }
  } catch (error) {
    failed.push({
      id: "runner-initialization",
      name: "Automation runner initialization",
      documentation: "README.md",
      passed: false,
      durationMs: 0,
      assertions: 0,
      steps: [],
      warnings: [],
      failedAssertion: error instanceof Error ? error.message : String(error),
      stack: error instanceof Error ? error.stack : undefined,
    });
  } finally {
    collector.dispose();
    console.error = originalConsoleError;
    window.removeEventListener("error", onWindowError);
    window.removeEventListener("unhandledrejection", onUnhandledRejection);
  }

  const completedAt = new Date().toISOString();
  const base = { runId, platform: automationPlatform(), startedAt, completedAt, passed, failed };
  const result: AutomationRunResult = {
    ...base,
    passedMarkdown: buildPassedMarkdown(base),
    failedMarkdown: buildFailedMarkdown(base),
  };
  window.__prayAutomationResult = result;

  try {
    result.reportPaths = await persistReports(result);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    result.failed.push({
      id: "report-persistence",
      name: "Automation report persistence",
      documentation: "README.md",
      passed: false,
      durationMs: 0,
      assertions: 1,
      steps: [],
      warnings: [],
      failedAssertion: message,
    });
    result.failedMarkdown = buildFailedMarkdown(result);
  }

  document.body.dataset.automationStatus = result.failed.length === 0 ? "passed" : "failed";
  document.body.dataset.automationPassed = String(result.passed.length);
  document.body.dataset.automationFailed = String(result.failed.length);
  window.dispatchEvent(new CustomEvent("pray:automation-complete", { detail: result }));
  console.info(`[pray.automation] ${JSON.stringify({ event: "complete", runId, passed: result.passed.length, failed: result.failed.length, reportPaths: result.reportPaths })}`);
}

async function prepareDeterministicLocation(): Promise<void> {
  const snapshot = await appClient.query<Record<string, unknown>>({
    name: "settings.getSnapshot",
    payload: { section: "locations" },
    domain: "settings",
    projectionKey: "automation.seed.locations",
    ifRevision: 0,
  });
  if (!snapshot.ok) throw new Error(`Could not read location for automation seed: ${snapshot.error.message}`);
  const saved = await executeCommand("settings.update", {
    section: "locations",
    field: "value",
    value: {
      ...snapshot.data,
      useGps: false,
      country: "NL",
      countryName: "Netherlands",
      city: "Amsterdam",
      latitude: 52.3676,
      longitude: 4.9041,
      timeZoneId: "Europe/Amsterdam",
      locationSource: "manual",
    },
  });
  if (!saved.ok) throw new Error(`Could not seed a deterministic automation location: ${saved.error}`);
  const today = await appClient.command({
    name: "today.refresh",
    domain: "today",
    projectionKey: "today.snapshot",
  });
  if (!today.ok) throw new Error(`Could not refresh Today after the automation location seed: ${today.error.message}`);
}

function withTimeout<T>(promise: Promise<T>, milliseconds: number, message: string): Promise<T> {
  return new Promise((resolve, reject) => {
    const timeout = window.setTimeout(() => reject(new Error(message)), milliseconds);
    promise.then(
      (value) => { window.clearTimeout(timeout); resolve(value); },
      (error) => { window.clearTimeout(timeout); reject(error); },
    );
  });
}
