import { appClient, type AppError } from "./appClient";
import { isBridgeReady, mauiTrace } from "@/native/mauiWebberClient";

export type ClientResponse<T> =
  | { ok: true; data: T }
  | { ok: false; error: string; errorInfo: AppError };

export async function executeCommand<T = unknown>(name: string, payload?: unknown): Promise<ClientResponse<T>> {
  const result = await appClient.command<T>({ name, payload, domain: name.split(".", 1)[0] });
  return result.ok
    ? { ok: true, data: result.data }
    : { ok: false, error: result.error.message, errorInfo: result.error };
}

export function nativeBackendReady(): boolean {
  return isBridgeReady();
}

export function traceClientEvent(name: string, payload?: Record<string, unknown>): void {
  mauiTrace(name, payload);
}

export function updateSettingsSection<TResult, TValue = TResult>(section: string, value: TValue): Promise<ClientResponse<TResult>> {
  return executeCommand<TResult>("settings.update", { section, field: "value", value });
}

export const platformIntents = {
  requestPermission: (id: string) => executeCommand("permissions.request", { id }),
  requestAllPermissions: () => executeCommand("permissions.requestAll"),
  refreshLocation: <T>() => executeCommand<T>("location.refresh"),
  reverseGeocode: <T>(latitude: number, longitude: number) => executeCommand<T>("location.reverseGeocode", { latitude, longitude }),
  addCustomAdhanSound: () => executeCommand("adhan.sound.addCustom"),
  previewAdhanSound: (id: string) => executeCommand("adhan.sound.preview", { id }),
  removeCustomAdhanSound: (id: string) => executeCommand("adhan.sound.removeCustom", { id }),
  testAlarm: () => executeCommand("alarm.test"),
  testNotification: () => executeCommand("notification.test"),
  openEmail: (to: string) => executeCommand("external.openEmail", { to }),
  call: (number: string) => executeCommand("external.call", { number }),
  openUrl: (url: string) => executeCommand("external.openUrl", { url }),
  reportIssue: () => executeCommand("external.reportIssue"),
};
