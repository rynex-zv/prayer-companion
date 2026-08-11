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

type PlatformAck = { accepted?: boolean; operationId?: string };
type PlatformEventPayload<T> = { operationId?: string; data?: T; error?: string };

async function executeInteractiveCommand<T = unknown>(name: string, payload?: Record<string, unknown>): Promise<ClientResponse<T>> {
  const operationId = globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  let dispose = () => {};
  const completion = new Promise<ClientResponse<T>>((resolve) => {
    const timer = globalThis.setTimeout(() => {
      dispose();
      resolve({ ok: false, error: `${name} did not complete.`, errorInfo: { code: "platform_timeout", message: `${name} did not complete.`, retryable: true } });
    }, 30000);
    dispose = appClient.subscribe((event) => {
      const eventPayload = event.payload as PlatformEventPayload<T> | undefined;
      if (eventPayload?.operationId !== operationId || !event.type.startsWith("platform.operation.")) return;
      globalThis.clearTimeout(timer);
      dispose();
      if (event.type === "platform.operation.completed") resolve({ ok: true, data: eventPayload.data as T });
      else resolve({ ok: false, error: eventPayload.error ?? `${name} failed.`, errorInfo: { code: "platform_operation_failed", message: eventPayload.error ?? `${name} failed.`, retryable: false } });
    });
  });
  const response = await executeCommand<T | PlatformAck>(name, { ...(payload ?? {}), operationId });
  if (!response.ok) {
    dispose();
    return response;
  }
  const ack = response.data as PlatformAck;
  if (ack?.accepted !== true) {
    dispose();
    return response as ClientResponse<T>;
  }
  return completion;
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

export type ConfirmedSettingsSection<T> = {
  ok?: boolean;
  section: string;
  field: string;
  value: T;
  projection: T;
  calculated?: T;
};

export function patchSettingsSection<T>(section: string, value: T): Promise<ClientResponse<ConfirmedSettingsSection<T>>> {
  return executeCommand<ConfirmedSettingsSection<T>>("settings.update", { section, field: "value", value });
}

export const platformIntents = {
  requestPermission: (id: string) => executeInteractiveCommand("permissions.request", { id }),
  requestAllPermissions: () => executeInteractiveCommand("permissions.requestAll"),
  refreshLocation: <T>(payload?: { source?: "gps" | "ip" }) => executeInteractiveCommand<T>("location.refresh", payload),
  reverseGeocode: <T>(latitude: number, longitude: number) => executeInteractiveCommand<T>("location.reverseGeocode", { latitude, longitude }),
  addCustomAdhanSound: () => executeInteractiveCommand("adhan.sound.addCustom"),
  previewAdhanSound: (id: string) => executeInteractiveCommand("adhan.sound.preview", { id }),
  removeCustomAdhanSound: (id: string) => executeCommand("adhan.sound.removeCustom", { id }),
  testAlarm: () => executeInteractiveCommand("alarm.test"),
  testNotification: () => executeInteractiveCommand("notification.test"),
  openEmail: (to: string) => executeInteractiveCommand("external.openEmail", { to }),
  call: (number: string) => executeInteractiveCommand("external.call", { number }),
  openUrl: (url: string) => executeInteractiveCommand("external.openUrl", { url }),
  reportIssue: () => executeInteractiveCommand("external.reportIssue"),
};
