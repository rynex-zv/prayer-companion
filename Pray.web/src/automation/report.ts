import { isBridgeReady, mauiCall } from "@/native/mauiWebberClient";
import type { AutomationRunResult, ScenarioResult } from "./types";

export function buildPassedMarkdown(run: Omit<AutomationRunResult, "passedMarkdown" | "failedMarkdown">): string {
  return [
    "# Passed automation scenarios",
    "",
    `- Run: \`${run.runId}\``,
    `- Platform: \`${run.platform}\``,
    `- Passed: **${run.passed.length}**`,
    `- Failed: **${run.failed.length}**`,
    "",
    ...run.passed.flatMap(formatPassed),
  ].join("\n");
}

export function buildFailedMarkdown(run: Omit<AutomationRunResult, "passedMarkdown" | "failedMarkdown">): string {
  const body = run.failed.length === 0
    ? ["No failed scenarios."]
    : run.failed.flatMap(formatFailed);
  return [
    "# Failed automation scenarios",
    "",
    `- Run: \`${run.runId}\``,
    `- Platform: \`${run.platform}\``,
    `- Failed: **${run.failed.length}**`,
    `- Passed: **${run.passed.length}**`,
    "",
    ...body,
  ].join("\n");
}

export async function persistReports(result: AutomationRunResult): Promise<{ passedPath?: string; failedPath?: string } | undefined> {
  if (isBridgeReady()) {
    const response = await mauiCall<{ passedPath?: string; failedPath?: string }>("automation.writeReports", {
      runId: result.runId,
      passedMarkdown: result.passedMarkdown,
      failedMarkdown: result.failedMarkdown,
    });
    if (!response.ok) throw new Error(`Native automation report write failed: ${response.error}`);
    return response.data;
  }

  downloadMarkdown("passed.md", result.passedMarkdown);
  downloadMarkdown("failed.md", result.failedMarkdown);
  return undefined;
}

function formatPassed(result: ScenarioResult): string[] {
  return [
    `## ${result.id} — ${result.name}`,
    "",
    `- Documentation: \`${result.documentation}\``,
    `- Duration: ${result.durationMs} ms`,
    `- Assertions: ${result.assertions}`,
    `- Warnings: ${result.warnings.length}`,
    ...(result.warnings.map((warning) => `  - ⚠️ ${warning}`)),
    "",
  ];
}

function formatFailed(result: ScenarioResult): string[] {
  return [
    `## ${result.id} — ${result.name}`,
    "",
    `- Documentation: \`${result.documentation}\``,
    `- Duration: ${result.durationMs} ms`,
    `- Assertions completed: ${result.assertions}`,
    `- Failed assertion: **${result.failedAssertion ?? "Unknown failure"}**`,
    ...(result.warnings.map((warning) => `- ⚠️ ${warning}`)),
    "",
    "### Completed steps",
    "",
    ...(result.steps.length ? result.steps.map((step) => `- ${step}`) : ["- None"]),
    "",
    ...(result.stack ? ["### Stack", "", "```text", result.stack, "```", ""] : []),
  ];
}

function downloadMarkdown(name: string, content: string): void {
  const link = document.createElement("a");
  link.href = URL.createObjectURL(new Blob([content], { type: "text/markdown;charset=utf-8" }));
  link.download = name;
  link.hidden = true;
  document.body.appendChild(link);
  link.click();
  window.setTimeout(() => {
    URL.revokeObjectURL(link.href);
    link.remove();
  }, 1000);
}
