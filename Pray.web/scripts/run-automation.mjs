import { spawn } from "node:child_process";
import { mkdir, writeFile } from "node:fs/promises";
import { appendFileSync, existsSync, mkdirSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";
import { chromium } from "playwright-core";

const root = process.cwd();
const skipBuild = process.argv.includes("--skip-build");
const resultsRoot = resolve(root, "automation-results");
mkdirSync(resultsRoot, { recursive: true });
const liveLogPath = resolve(resultsRoot, "browser-live.log");
writeFileSync(liveLogPath, "", "utf8");
const logBrowser = (line) => {
  appendFileSync(liveLogPath, `${line}\n`, "utf8");
  if (!process.stdout.destroyed) process.stdout.write(`${line}\n`);
};
const port = Number(process.env.PRAY_AUTOMATION_PORT ?? 4179);
const baseUrl = `http://127.0.0.1:${port}`;
const scenario = process.env.PRAY_AUTOMATION_SCENARIO;
const automationUrl = scenario ? `${baseUrl}/test?automationScenario=${encodeURIComponent(scenario)}` : `${baseUrl}/test`;
const automationEnvironment = {
  ...process.env,
  PRAY_AUTOMATION: "true",
  PRAY_AUTOMATION_WEB: process.env.PRAY_AUTOMATION_WEB ?? "true",
  PRAY_AUTOMATION_WINDOWS: process.env.PRAY_AUTOMATION_WINDOWS ?? "false",
  PRAY_AUTOMATION_ANDROID: process.env.PRAY_AUTOMATION_ANDROID ?? "false",
};

if (!skipBuild) {
  await run(process.execPath, [resolve(root, "scripts", "build.mjs")], automationEnvironment);
}

const preview = spawn(process.execPath, [resolve(root, "node_modules", "vite", "bin", "vite.js"), "preview", "--host", "127.0.0.1", "--port", String(port)], {
  cwd: root,
  env: automationEnvironment,
  stdio: ["ignore", "pipe", "pipe"],
});
preview.stdout.on("data", (chunk) => process.stdout.write(chunk));
preview.stderr.on("data", (chunk) => process.stderr.write(chunk));

let browser;
try {
  await waitForServer(baseUrl);
  logBrowser(`[runner] url=${automationUrl}`);
  browser = await chromium.launch({ executablePath: findBrowser(), headless: true });
  const context = await browser.newContext({ acceptDownloads: true });
  const page = await context.newPage();
  if (scenario) await page.addInitScript((scenarioId) => { window.__prayAutomationScenario = scenarioId; }, scenario);
  page.on("console", (message) => {
    const text = message.text();
    if (message.type() === "error" || text.includes("[pray.automation]") || text.includes("[pray.bridge]") || text.includes("budget_exceeded")) logBrowser(`[browser:${message.type()}] ${text}`);
  });
  page.on("pageerror", (error) => logBrowser(`[browser:pageerror] ${error.stack ?? error.message}`));
  await page.goto(automationUrl, { waitUntil: "domcontentloaded" });
  await page.waitForFunction(() => ["passed", "failed"].includes(document.body.dataset.automationStatus ?? ""), null, { timeout: 300_000 });
  const result = await page.evaluate(() => window.__prayAutomationResult);
  if (!result) throw new Error("Automation completed without exposing a result.");

  const runRoot = resolve(resultsRoot, result.runId);
  await mkdir(runRoot, { recursive: true });
  await writeFile(resolve(runRoot, "passed.md"), result.passedMarkdown, "utf8");
  await writeFile(resolve(runRoot, "failed.md"), result.failedMarkdown, "utf8");
  await writeFile(resolve(runRoot, "result.json"), JSON.stringify(result, null, 2), "utf8");
  await writeFile(resolve(resultsRoot, "latest-run.txt"), result.runId, "utf8");
  process.stdout.write(`Automation reports: ${runRoot}\n`);
  process.stdout.write(`Passed=${result.passed.length} Failed=${result.failed.length}\n`);
  if (result.failed.length > 0) process.exitCode = 1;
} finally {
  if (browser) await Promise.race([browser.close(), delay(10_000)]);
  await stopPreview();
}

async function stopPreview() {
  if (!preview.pid || preview.exitCode !== null) return;
  if (process.platform === "win32") {
    await new Promise((resolvePromise) => {
      const stop = spawn("taskkill", ["/pid", String(preview.pid), "/T", "/F"], { stdio: "ignore" });
      stop.once("exit", resolvePromise);
      stop.once("error", resolvePromise);
    });
    return;
  }
  preview.kill("SIGTERM");
}

function delay(milliseconds) {
  return new Promise((resolvePromise) => setTimeout(resolvePromise, milliseconds));
}

function run(command, args, env) {
  return new Promise((resolvePromise, rejectPromise) => {
    const child = spawn(command, args, { cwd: root, env, stdio: "inherit" });
    child.on("error", rejectPromise);
    child.on("exit", (code) => code === 0 ? resolvePromise() : rejectPromise(new Error(`${command} exited with ${code}`)));
  });
}

async function waitForServer(url) {
  const started = Date.now();
  while (Date.now() - started < 30_000) {
    try {
      const response = await fetch(url);
      if (response.ok) return;
    } catch {
    }
    await new Promise((resolvePromise) => setTimeout(resolvePromise, 100));
  }
  throw new Error(`Preview server did not start at ${url}`);
}

function findBrowser() {
  const candidates = process.platform === "win32"
    ? [
        "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
        "C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe",
        "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
      ]
    : process.platform === "darwin"
      ? ["/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge", "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"]
      : ["/usr/bin/microsoft-edge", "/usr/bin/google-chrome", "/usr/bin/chromium"];
  const browserPath = candidates.find(existsSync);
  if (!browserPath) throw new Error("No supported installed Chromium browser was found.");
  return browserPath;
}
