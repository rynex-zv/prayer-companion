import { automationThresholds } from "./config";
import type { RpcTiming } from "./types";

export class AutomationAssertionError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "AutomationAssertionError";
  }
}

export class ScenarioContext {
  readonly steps: string[] = [];
  readonly warnings: string[] = [];
  assertions = 0;
  private timingStart = 0;

  constructor(private readonly timings: RpcTiming[], includeStartupTimings = false) {
    this.timingStart = includeStartupTimings ? 0 : timings.length;
  }

  assert(condition: unknown, message: string): void {
    this.assertions += 1;
    if (!condition) throw new AutomationAssertionError(message);
  }

  step(message: string): void {
    this.steps.push(message);
    console.info(`[pray.automation] ${JSON.stringify({ event: "step", message })}`);
  }

  async navigate(route: string): Promise<void> {
    const api = await waitForAutomationApi();
    this.assert(await api.navigate(route), `Navigation rejected route ${route}`);
    await waitFor(() => window.prayerCompanion?.currentRoute() === route, `Route did not become ${route}`, 12000);
    await waitFor(() => Boolean(document.querySelector(`[data-selector-name="route:${CSS.escape(route)}"]`)), `Route root missing for ${route}`, 12000);
    await waitFor(() => {
      const routeRoot = document.querySelector<HTMLElement>(`[data-selector-name="route:${CSS.escape(route)}"]`);
      return Boolean(routeRoot?.textContent?.trim());
    }, `Route ${route} did not finish rendering visible content`, 12000);
    this.step(`Navigated to ${route}`);
    await delay(120);
  }

  element(selectorName: string): HTMLElement {
    const element = document.querySelector<HTMLElement>(`[data-selector-name="${CSS.escape(selectorName)}"]`);
    if (!element) throw new AutomationAssertionError(`Missing selector: ${selectorName}`);
    return element;
  }

  async click(selectorName: string, waitMs = 120): Promise<void> {
    const api = await waitForAutomationApi();
    this.assert(api.click(selectorName), `Could not click selector: ${selectorName}`);
    this.step(`Clicked ${selectorName}`);
    // React handlers may dispatch after the injected click returns. Give them one
    // turn, then wait for the confirmed backend projection before continuing.
    await delay(25);
    await waitForRpcIdle();
    await delay(Math.max(0, waitMs - 25));
  }

  async setValue(selectorName: string, value: string | number | boolean): Promise<void> {
    const api = await waitForAutomationApi();
    this.assert(await api.setValue(selectorName, value), `Could not set ${selectorName} to ${String(value)}`);
    await waitFor(() => valueMatches(selectorName, value), `${selectorName} did not become ${String(value)}`);
    await waitForRpcIdle();
    await waitFor(() => valueMatches(selectorName, value), `${selectorName} was overwritten after backend confirmation`);
    this.step(`Set ${selectorName}=${String(value)}`);
  }

  async setAndRestore(selectorName: string, value: string | number | boolean): Promise<void> {
    const original = readValue(selectorName);
    if (original === undefined) throw new AutomationAssertionError(`Cannot read original value for ${selectorName}`);
    await this.setValue(selectorName, value);
    this.assert(valueMatches(selectorName, value), `${selectorName} before/after assertion failed`);
    await this.setValue(selectorName, original);
    this.assert(valueMatches(selectorName, original), `${selectorName} restore assertion failed`);
  }

  async waitForSelector(selectorName: string, timeoutMs = 12000): Promise<HTMLElement> {
    await waitFor(() => Boolean(document.querySelector(`[data-selector-name="${CSS.escape(selectorName)}"]`)), `Timed out waiting for ${selectorName}`, timeoutMs);
    return this.element(selectorName);
  }

  findSelector(prefix: string): string {
    const element = document.querySelector<HTMLElement>(`[data-selector-name^="${CSS.escape(prefix)}"]`);
    if (!element?.dataset.selectorName) throw new AutomationAssertionError(`No selector starts with ${prefix}`);
    return element.dataset.selectorName;
  }

  findLastSelector(prefix: string): string {
    const elements = Array.from(document.querySelectorAll<HTMLElement>(`[data-selector-name^="${CSS.escape(prefix)}"]`));
    const element = elements.at(-1);
    if (!element?.dataset.selectorName) throw new AutomationAssertionError(`No selector starts with ${prefix}`);
    return element.dataset.selectorName;
  }

  validateVisibleTextAndNames(route: string): void {
    const main = document.querySelector<HTMLElement>("main");
    this.assert((main?.innerText.trim().length ?? 0) > 0, `${route} has no visible text`);
    const controls = Array.from(main?.querySelectorAll<HTMLElement>("button, a[href], input, select, textarea") ?? [])
      .filter((element) => !element.hidden && element.getAttribute("aria-hidden") !== "true");
    this.assert(controls.length > 0 || route === "/alarm", `${route} has no interactive controls`);
    for (const control of controls) {
      this.assert(accessibleName(control).length > 0, `${route} control ${describe(control)} has no accessible name/text`);
      if (control instanceof HTMLInputElement || control instanceof HTMLSelectElement || control instanceof HTMLTextAreaElement) {
        this.assert(Boolean(control.dataset.selectorName), `${route} input ${describe(control)} lacks data-selector-name`);
      }
    }
    this.step(`Validated ${controls.length} control names on ${route}`);
  }

  async mutateEveryInput(route: string): Promise<void> {
    const names = Array.from(document.querySelectorAll<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>("main input[data-selector-name], main select[data-selector-name], main textarea[data-selector-name]"))
      .filter((element) => !element.disabled && element.type !== "hidden" && element.type !== "file")
      .map((element) => element.dataset.selectorName!)
      .filter((name, index, all) => all.indexOf(name) === index);

    for (const name of names) {
      const element = this.element(name) as HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement;
      const alternate = alternateValue(element);
      if (alternate === undefined || alternate === element.value) continue;
      await this.setAndRestore(name, alternate);
      await delay(80);
    }
    this.step(`Changed and restored ${names.length} inputs on ${route}`);
  }

  finalizeTimings(): string | undefined {
    const timings = this.timings.slice(this.timingStart)
      .filter((timing) => timing.event === "success" && typeof timing.elapsedMs === "number");
    for (const timing of timings) {
      if (timing.elapsedMs! > automationThresholds.warningMs) {
        this.warnings.push(`${timing.method ?? "unknown"} took ${timing.elapsedMs} ms (${timing.source ?? "unknown"})`);
      }
    }
    const overBudget = timings.filter((timing) => timing.elapsedMs! > automationThresholds.failureMs);
    for (const timing of overBudget) {
      const warning = `300 ms ceiling exceeded: ${timing.method ?? "unknown"}=${timing.elapsedMs}ms`;
      if (!this.warnings.includes(warning)) this.warnings.push(warning);
    }
    if (overBudget.length === 0) return undefined;
    return `300 ms data-call ceiling exceeded: ${overBudget
      .map((timing) => `${timing.method ?? "unknown"}=${timing.elapsedMs}ms`)
      .join(", ")}`;
  }
}

export function collectRpcTimings(): { timings: RpcTiming[]; dispose: () => void } {
  const timings: RpcTiming[] = [];
  const listener = (event: Event) => timings.push((event as CustomEvent<RpcTiming>).detail);
  window.addEventListener("pray:rpc-timing", listener);
  return { timings, dispose: () => window.removeEventListener("pray:rpc-timing", listener) };
}

export async function waitForAutomationApi(timeoutMs = 10000): Promise<NonNullable<Window["prayerCompanion"]>> {
  await waitFor(() => Boolean(window.prayerCompanion), "prayerCompanion automation API did not initialize", timeoutMs);
  return window.prayerCompanion!;
}

export function readValue(selectorName: string): string | undefined {
  const element = document.querySelector<HTMLElement>(`[data-selector-name="${CSS.escape(selectorName)}"]`);
  return element instanceof HTMLInputElement || element instanceof HTMLSelectElement || element instanceof HTMLTextAreaElement
    ? element.value
    : element?.getAttribute("aria-checked") ?? undefined;
}

function valueMatches(selectorName: string, expected: string | number | boolean): boolean {
  const actual = readValue(selectorName);
  const wanted = String(expected);
  const element = document.querySelector<HTMLElement>(`[data-selector-name="${CSS.escape(selectorName)}"]`);
  return element instanceof HTMLSelectElement
    ? actual?.localeCompare(wanted, undefined, { sensitivity: "accent" }) === 0
    : actual === wanted;
}

export async function waitFor(predicate: () => boolean, message: string, timeoutMs = 4000): Promise<void> {
  const started = performance.now();
  while (!predicate()) {
    if (performance.now() - started > timeoutMs) throw new AutomationAssertionError(message);
    await delay(25);
  }
}

export function delay(milliseconds: number): Promise<void> {
  return new Promise((resolve) => window.setTimeout(resolve, milliseconds));
}

async function waitForRpcIdle(timeoutMs = 15000): Promise<void> {
  await waitFor(() => (window.__prayRpcPendingCalls?.size ?? 0) === 0, "Timed out waiting for application data calls", timeoutMs);
}

function alternateValue(element: HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement): string | undefined {
  if (element instanceof HTMLSelectElement) {
    return Array.from(element.options).find((option) => !option.disabled && option.value !== element.value)?.value;
  }
  if (element instanceof HTMLInputElement && element.type === "checkbox") return element.checked ? "false" : "true";
  if (element instanceof HTMLInputElement && element.type === "range") {
    const min = Number(element.min || 0);
    const max = Number(element.max || 100);
    const current = Number(element.value);
    return String(current < max ? Math.min(max, current + 1) : Math.max(min, current - 1));
  }
  if (element instanceof HTMLInputElement && ["number", "time"].includes(element.type)) {
    if (element.type === "time") return element.value === "09:15" ? "10:20" : "09:15";
    const min = element.min === "" ? -100000 : Number(element.min);
    const max = element.max === "" ? 100000 : Number(element.max);
    const current = Number(element.value || 0);
    return String(Math.min(max, Math.max(min, current + 1)));
  }
  if (element instanceof HTMLInputElement && element.type === "url") return "https://automation.invalid/bundle";
  if (element instanceof HTMLInputElement && /^-?\d+(?:\.\d+)?$/.test(element.value.trim())) {
    return String(Number(element.value) + 1);
  }
  return element.value ? `${element.value} automation` : "automation value";
}

function accessibleName(element: HTMLElement): string {
  const aria = element.getAttribute("aria-label")?.trim();
  if (aria) return aria;
  const labelledBy = element.getAttribute("aria-labelledby");
  if (labelledBy) {
    const value = labelledBy.split(/\s+/).map((id) => document.getElementById(id)?.textContent?.trim() ?? "").join(" ").trim();
    if (value) return value;
  }
  if (element.id) {
    const label = document.querySelector<HTMLLabelElement>(`label[for="${CSS.escape(element.id)}"]`)?.innerText.trim();
    if (label) return label;
  }
  const wrappingLabel = element.closest("label")?.innerText.trim();
  if (wrappingLabel) return wrappingLabel;
  return element.innerText?.trim() || element.getAttribute("placeholder")?.trim() || element.getAttribute("title")?.trim() || "";
}

function describe(element: HTMLElement): string {
  return element.dataset.selectorName ?? element.getAttribute("aria-label") ?? element.tagName.toLowerCase();
}
