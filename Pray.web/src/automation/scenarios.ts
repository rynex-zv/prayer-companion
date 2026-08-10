import { ScenarioContext, delay, readValue, waitFor } from "./harness";
import { appClient } from "@/client/appClient";

export type ScenarioDefinition = {
  id: string;
  name: string;
  documentation: string;
  run: (context: ScenarioContext) => Promise<void>;
};

const applicationRoutes = [
  "/",
  "/calendar",
  "/qibla",
  "/tasbih",
  "/settings",
  "/settings/locations",
  "/settings/theme",
  "/settings/adhan",
  "/settings/notifications",
  "/settings/permissions",
  "/settings/alarms",
  "/settings/tasbih",
  "/settings/about",
  "/alarm",
];

export const automationScenarios: ScenarioDefinition[] = [
  {
    id: "01-page-contract",
    name: "Every page, text, control name, input value, and navigation",
    documentation: "01-page-contract.md",
    run: async (ctx) => {
      await ctx.navigate("/onboarding");
      ctx.validateVisibleTextAndNames("/onboarding step 1");
      await ctx.click("onboarding:next");
      await ctx.waitForSelector("onboarding:permissions-step");
      ctx.validateVisibleTextAndNames("/onboarding step 2");
      await ctx.click("onboarding:next");
      await ctx.waitForSelector("onboarding:location-step");
      ctx.validateVisibleTextAndNames("/onboarding step 3");
      await ctx.mutateEveryInput("/onboarding step 3");
      await ctx.click("onboarding:back");
      await ctx.waitForSelector("onboarding:permissions-step");
      await ctx.click("onboarding:next");
      await ctx.click("onboarding:next", 250);
      await waitFor(() => window.prayerCompanion?.currentRoute() === "/", "Onboarding did not finish and redirect to Today", 6000);
      ctx.step("Completed onboarding navigation and redirect");

      for (const route of applicationRoutes) {
        await ctx.navigate(route);
        ctx.validateVisibleTextAndNames(route);
        await ctx.mutateEveryInput(route);
      }
    },
  },
  {
    id: "02-today-calendar",
    name: "User checks prayer times and explores the calendar",
    documentation: "02-today-calendar.md",
    run: async (ctx) => {
      await ctx.navigate("/");
      const refresh = Array.from(document.querySelectorAll<HTMLButtonElement>("main button")).find((button) => button.getAttribute("aria-label"));
      ctx.assert(refresh, "Today refresh button is missing");
      refresh!.click();
      await delay(250);
      await ctx.click("tab:calendar");
      await waitFor(() => window.prayerCompanion?.currentRoute() === "/calendar", "Calendar tab did not navigate");
      for (const selector of ["calendar:previous", "calendar:next", "calendar:view:year", "calendar:view:month", "calendar:view:week", "calendar:view:day", "calendar:mode:hijri", "calendar:mode:gregorian", "calendar:today"]) {
        await ctx.click(selector, 180);
      }
      await ctx.click("calendar:view:month", 180);
      await ctx.click(ctx.findSelector("calendar:day:"));
    },
  },
  {
    id: "03-qibla-location",
    name: "User changes location and verifies Qibla modes",
    documentation: "03-qibla-location.md",
    run: async (ctx) => {
      await ctx.navigate("/settings/locations");
      const city = ctx.element("locations:city") as HTMLSelectElement;
      const alternateCity = Array.from(city.options).find((option) => option.value !== city.value)?.value;
      ctx.assert(alternateCity, "No alternate city is available");
      await ctx.setAndRestore("locations:city", alternateCity!);
      await ctx.setAndRestore("locations:qibla-reading-mode", alternateOption("locations:qibla-reading-mode"));
      await ctx.setAndRestore("locations:qibla-filter-mode", alternateOption("locations:qibla-filter-mode"));
      await ctx.navigate("/qibla");
      for (const prefix of ["qibla:heading:", "qibla:reading:", "qibla:filter:"]) {
        const controls = Array.from(document.querySelectorAll<HTMLElement>(`[data-selector-name^="${prefix}"]`));
        ctx.assert(controls.length >= 2, `Expected options for ${prefix}`);
        for (const control of controls) {
          control.click();
          await delay(120);
        }
      }
      const mapMode = Array.from(document.querySelectorAll<HTMLElement>('[data-selector-name^="qibla:reading:"]')).find((element) => /map/i.test(element.dataset.selectorName ?? ""));
      mapMode?.click();
      await delay(180);
      await ctx.click("qibla:map:zoom-in");
      await ctx.click("qibla:map:zoom-out");
    },
  },
  {
    id: "04-theme-localization",
    name: "User personalizes language, theme, accent, and text size",
    documentation: "04-theme-localization.md",
    run: async (ctx) => {
      await ctx.navigate("/settings/theme");
      await ctx.setAndRestore("theme:language", alternateOption("theme:language"));
      for (const selector of ["theme:mode:light", "theme:mode:dark", "theme:mode:system", "theme:accent:green", "theme:accent:teal", "theme:text-size:decrease", "theme:text-size:increase"]) {
        await ctx.click(selector, 160);
      }
      ctx.assert(document.documentElement.lang.length > 0, "Document language was not applied");
      ctx.assert(["ltr", "rtl"].includes(document.documentElement.dir), "Document direction is invalid");
    },
  },
  {
    id: "05-tasbih-workflow",
    name: "User creates, edits, orders, uses, and removes a Tasbih preset",
    documentation: "05-tasbih-workflow.md",
    run: async (ctx) => {
      await ctx.navigate("/settings/tasbih");
      await ctx.setValue("settings-tasbih:new-preset-name", "Automation preset");
      await ctx.click("settings-tasbih:add-preset-from-input", 350);
      const removeSelector = ctx.findSelector("settings-tasbih:remove-preset:");
      const id = removeSelector.split(":").at(-1)!;
      await ctx.setValue(`settings-tasbih:preset-name:${id}`, "Automation renamed");
      await ctx.setValue(`settings-tasbih:repeat:${id}`, "Reset");
      await ctx.setValue(`settings-tasbih:new-item-text:${id}`, "Automation item");
      await ctx.setValue(`settings-tasbih:new-item-count:${id}`, "7");
      await ctx.click(`settings-tasbih:add-item:${id}`, 300);
      await ctx.waitForSelector(`settings-tasbih:item-text:${id}:1`);
      await ctx.setValue(`settings-tasbih:item-text:${id}:1`, "Automation edited item");
      await ctx.setValue(`settings-tasbih:item-count:${id}:1`, "9");
      await ctx.click(`settings-tasbih:item-up:${id}:1`, 220);
      await ctx.click(`settings-tasbih:item-down:${id}:0`, 220);
      await ctx.click(`settings-tasbih:item-remove:${id}:1`, 250);
      await ctx.navigate("/tasbih");
      await ctx.click("tasbih:increment");
      await ctx.click("tasbih:increment");
      await ctx.click("tasbih:reset");
      await ctx.navigate("/settings/tasbih");
      await ctx.click(`settings-tasbih:remove-preset:${id}`, 300);
      ctx.assert(!document.body.innerText.includes("Automation renamed"), "Temporary Tasbih preset was not removed");
    },
  },
  {
    id: "06-notification-reminder",
    name: "User configures and removes an Adhan notification reminder",
    documentation: "06-notification-reminder.md",
    run: async (ctx) => {
      await ctx.navigate("/settings/notifications");
      await ctx.click("notifications:vibration", 220);
      await ctx.click("notifications:vibration", 220);
      await ctx.setAndRestore("notifications:minutes-before", String(Number(readValue("notifications:minutes-before") ?? 0) + 1));
      await ctx.click("notifications:reminder-add", 300);
      const valueSelector = ctx.findLastSelector("notifications:reminder-value:");
      const index = valueSelector.split(":").at(-1)!;
      await ctx.setValue(valueSelector, "12");
      await ctx.setValue(`notifications:reminder-unit:${index}`, alternateOption(`notifications:reminder-unit:${index}`));
      await ctx.setValue(`notifications:reminder-direction:${index}`, alternateOption(`notifications:reminder-direction:${index}`));
      await ctx.setValue(`notifications:reminder-alert:${index}`, alternateOption(`notifications:reminder-alert:${index}`));
      await ctx.click(`notifications:reminder-remove:${index}`, 300);
      ctx.assert(!document.querySelector(`[data-selector-name="notifications:reminder-value:${index}"]`), "Temporary notification reminder was not removed");
    },
  },
  {
    id: "07-alarm-reminder",
    name: "User creates, edits, toggles, and removes an alarm reminder",
    documentation: "07-alarm-reminder.md",
    run: async (ctx) => {
      await ctx.navigate("/settings/alarms");
      const builtIn = ctx.findSelector("alarms:built-in:");
      await ctx.click(builtIn, 220);
      await ctx.click(builtIn, 220);
      await ctx.setValue("alarms:new-reminder-text", "Automation alarm reminder");
      await ctx.click("alarms:add-reminder-from-input", 300);
      const toggle = ctx.findSelector("alarms:user-toggle:new-");
      const id = toggle.split(":").slice(2).join(":");
      await ctx.click(toggle, 220);
      await ctx.setValue(`alarms:reminder-text:${id}`, "Automation alarm edited");
      await ctx.click(`alarms:remove:${id}`, 300);
      ctx.assert(!document.body.innerText.includes("Automation alarm edited"), "Temporary alarm reminder was not removed");
    },
  },
  {
    id: "08-adhan-settings",
    name: "User adjusts Adhan calculation and fasting reminder settings",
    documentation: "08-adhan-settings.md",
    run: async (ctx) => {
      await ctx.navigate("/settings/locations");
      const country = document.querySelector<HTMLSelectElement>('[data-selector-name="locations:country"]');
      const netherlands = Array.from(country?.options ?? []).find((option) => option.value.toLowerCase() === "nl")?.value;
      if (netherlands) await ctx.setValue("locations:country", netherlands);
      await ctx.setValue("locations:city", "Amsterdam");
      ctx.assert(readValue("locations:latitude") === "52.3676", "Amsterdam latitude did not resolve to the shared catalog value");
      ctx.assert(readValue("locations:longitude") === "4.9041", "Amsterdam longitude did not resolve to the shared catalog value");
      await ctx.navigate("/settings/adhan");
      ctx.assert(ctx.element("adhan:calculation-engine").innerText.trim().length > 0, "Adhan page did not identify the shared calculation engine");
      await ctx.setValue("adhan:clock-format", "24h");
      const before = await queryTodayProjection();
      ctx.step("Shared prayer inputs: Amsterdam NL, 52.3676, 4.9041, Auto, Shafi, MiddleOfTheNight, 24h");
      ctx.step(`Shared prayer snapshot: ${before.todayTimings.map((item) => `${item.id}=${item.time}`).join(", ")}`);
      const originalMethod = readValue("adhan:method");
      ctx.assert(originalMethod, "Calculation method is empty");
      const alternateMethod = alternateOption("adhan:method");
      await ctx.setValue("adhan:method", alternateMethod);
      const after = await queryTodayProjection();
      ctx.assert(before.todayTimings.some((item, index) => item.time !== after.todayTimings[index]?.time), `Changing calculation method ${originalMethod} -> ${alternateMethod} did not change any prayer time`);
      await ctx.setValue("adhan:method", originalMethod!);
      const restored = await queryTodayProjection();
      ctx.assert(restored.calculation.selectedMethod === originalMethod, `Persisted calculation method was ${restored.calculation.selectedMethod}, expected ${originalMethod}`);
      ctx.assert(
        before.todayTimings.every((item, index) => item.time === restored.todayTimings[index]?.time),
        "Prayer times did not return to the original values after restoring the calculation method",
      );
      await ctx.setAndRestore("adhan:volume", alternateNumeric("adhan:volume"));
      for (const selector of ["adhan:madhhab", "adhan:high-latitude", "adhan:clock-format", "adhan:override-sound:Fajr", "adhan:override-vibration:Fajr"]) {
        if (document.querySelector(`[data-selector-name="${selector}"]`)) await ctx.setAndRestore(selector, alternateOption(selector));
      }
      await ctx.click("adhan:imsak-reminder:add", 300);
      const value = ctx.findLastSelector("adhan:imsak-reminder:value:");
      const index = value.split(":").at(-1)!;
      await ctx.setValue(value, "11");
      await ctx.setValue(`adhan:imsak-reminder:direction:${index}`, alternateOption(`adhan:imsak-reminder:direction:${index}`));
      await ctx.click(`adhan:imsak-reminder:remove:${index}`, 300);
      ctx.assert(!document.querySelector(`[data-selector-name="${value}"]`), "Temporary Imsak reminder was not removed");
    },
  },
  {
    id: "09-settings-about-navigation",
    name: "User opens every Settings page and saves the About URL",
    documentation: "09-settings-about-navigation.md",
    run: async (ctx) => {
      const rows: [string, string][] = [
        ["locations", "/settings/locations"], ["themeDiagnostics", "/settings/theme"], ["adhan", "/settings/adhan"],
        ["notifications", "/settings/notifications"], ["permissions", "/settings/permissions"], ["alarmReminders", "/settings/alarms"],
        ["tasbihSettings", "/settings/tasbih"], ["about", "/settings/about"],
      ];
      for (const [key, route] of rows) {
        await ctx.navigate("/settings");
        await ctx.click(`settings:row:${key}`);
        await waitFor(() => window.prayerCompanion?.currentRoute() === route, `${key} did not navigate to ${route}`);
      }
      await ctx.navigate("/settings/about");
      const original = readValue("about:remote-web-url");
      ctx.assert(original, "About remote URL is empty");
      await ctx.click("about:save-remote-web-url", 300);
      await ctx.waitForSelector("about:pull-remote-status");
      ctx.assert(ctx.element("about:pull-remote-status").innerText.trim().length > 0, "About URL save produced no status");
    },
  },
  {
    id: "10-platform-operations",
    name: "System operations acknowledge promptly and report truthful completion",
    documentation: "10-platform-operations.md",
    run: async (ctx) => {
      const operations: Array<{ name: string; domain: string; payload?: Record<string, unknown>; completion?: "completed" | "failed" }> = [
        { name: "external.openEmail", domain: "external", payload: { to: "rynex@rynex.nl" } },
        { name: "external.call", domain: "external", payload: { number: "+31610331734" } },
        { name: "external.openUrl", domain: "external", payload: { url: "https://pray.rynex.nl/" } },
        { name: "external.reportIssue", domain: "external" },
        { name: "adhan.sound.addCustom", domain: "adhan" },
        { name: "alarm.test", domain: "alarm" },
        { name: "notification.test", domain: "notification" },
        { name: "permissions.request", domain: "permissions", payload: { id: "not-a-permission" }, completion: "failed" },
      ];
      for (const operation of operations) {
        const operationId = crypto.randomUUID();
        let completion: { type: string; payload?: unknown } | undefined;
        const unsubscribe = appClient.subscribe((event) => {
          const payload = event.payload as { operationId?: string } | undefined;
          if (payload?.operationId === operationId) completion = event;
        });
        const started = performance.now();
        const result = await appClient.command<{ accepted: boolean; status: string; operationId: string }>({
          name: operation.name,
          domain: operation.domain,
          payload: { ...operation.payload, operationId },
        });
        const acknowledgedMs = performance.now() - started;
        ctx.assert(result.ok, `${operation.name} acknowledgement failed`);
        if (!result.ok) {
          unsubscribe();
          continue;
        }
        ctx.assert(result.data.accepted === true && result.data.status === "pending", `${operation.name} returned a fake success instead of a pending acknowledgement`);
        ctx.assert(result.data.operationId === operationId, `${operation.name} changed the operation id`);
        ctx.assert(acknowledgedMs < 300, `${operation.name} acknowledgement took ${Math.round(acknowledgedMs)} ms`);
        await waitFor(() => completion !== undefined, `${operation.name} did not publish completion`, 4000);
        const expectedType = operation.completion === "failed" ? "platform.operation.failed" : "platform.operation.completed";
        ctx.assert(completion?.type === expectedType, `${operation.name} completion was ${completion?.type ?? "missing"}`);
        unsubscribe();
        ctx.step(`${operation.name} acknowledged in ${Math.round(acknowledgedMs)} ms and completed asynchronously`);
      }
    },
  },
];

async function queryTodayProjection(): Promise<{ todayTimings: { id: string; time: string }[]; calculation: { selectedMethod: string; effectiveMethod: string } }> {
  const result = await appClient.query<{ todayTimings: { id: string; time: string }[]; calculation: { selectedMethod: string; effectiveMethod: string } }>({
    name: "today.getSnapshot",
    domain: "today",
    projectionKey: "today.snapshot",
    ifRevision: 0,
  });
  if (!result.ok) throw new Error(`Could not read Today projection: ${result.error.message}`);
  return result.data;
}

function alternateOption(selectorName: string): string {
  const element = document.querySelector<HTMLSelectElement>(`[data-selector-name="${CSS.escape(selectorName)}"]`);
  if (!element) throw new Error(`Missing select ${selectorName}`);
  const option = Array.from(element.options).find((item) => !item.disabled && item.value !== element.value);
  if (!option) throw new Error(`No alternate option for ${selectorName}`);
  return option.value;
}

function alternateNumeric(selectorName: string): string {
  const element = document.querySelector<HTMLInputElement>(`[data-selector-name="${CSS.escape(selectorName)}"]`);
  if (!element) throw new Error(`Missing numeric input ${selectorName}`);
  const current = Number(element.value || 0);
  const max = element.max === "" ? current + 1 : Number(element.max);
  return String(current < max ? current + 1 : current - 1);
}
