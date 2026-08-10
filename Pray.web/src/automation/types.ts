import type { AutomationPlatform } from "./config";

export type RpcTiming = {
  event: string;
  method?: string;
  elapsedMs?: number;
  source?: string;
  requestId?: string;
};

export type ScenarioResult = {
  id: string;
  name: string;
  documentation: string;
  passed: boolean;
  durationMs: number;
  assertions: number;
  steps: string[];
  warnings: string[];
  failedAssertion?: string;
  stack?: string;
};

export type AutomationRunResult = {
  runId: string;
  platform: AutomationPlatform;
  startedAt: string;
  completedAt: string;
  passed: ScenarioResult[];
  failed: ScenarioResult[];
  passedMarkdown: string;
  failedMarkdown: string;
  reportPaths?: { passedPath?: string; failedPath?: string };
};

declare global {
  interface Window {
    __prayAutomationResult?: AutomationRunResult;
    __prayAutomationScenario?: string;
    __prayRpcPendingCalls?: Set<number>;
  }
}
