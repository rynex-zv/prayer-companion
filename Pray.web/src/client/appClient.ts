import { mauiCall, type TransportError } from "@/native/mauiWebberClient";
import { installConfirmed, setRequest } from "./clientStore";

export type AppError = { code: string; message: string; retryable: boolean; details?: unknown };
export type QueryResult<T> = { ok: true; requestId: string; revision: number; data: T; notModified?: boolean } | { ok: false; requestId: string; error: AppError };
export type CommandResult<T> = { ok: true; requestId: string; commandId: string; revision: number; changedDomains: string[]; data: T } | { ok: false; requestId: string; commandId: string; error: AppError };
export type AppEvent = { sequence: number; eventId: string; timestamp: string; domain: string; type: string; revision: number; causeRequestId?: string; payload?: unknown; invalidationKey?: string };

export type AppQuery<T> = { name: string; payload?: unknown; domain: string; projectionKey: string; ifRevision?: number; signal?: AbortSignal };
export type AppCommand<T> = { name: string; payload?: unknown; domain: string; projectionKey?: string; expectedRevision?: number; signal?: AbortSignal };

export interface AppClient {
  bootstrap<T>(request: Omit<AppQuery<T>, "name">): Promise<QueryResult<T>>;
  query<T>(query: AppQuery<T>): Promise<QueryResult<T>>;
  command<T>(command: AppCommand<T>): Promise<CommandResult<T>>;
  subscribe(listener: (event: AppEvent) => void): () => void;
}

const inFlightQueries = new Map<string, Promise<QueryResult<unknown>>>();
const eventListeners = new Set<(event: AppEvent) => void>();

class DefaultAppClient implements AppClient {
  bootstrap<T>(request: Omit<AppQuery<T>, "name">): Promise<QueryResult<T>> {
    // Phase 2 compatibility mapping; Phase 3 replaces this with app.bootstrap.
    return this.query({ ...request, name: "app.getShellSnapshot" });
  }

  query<T>(query: AppQuery<T>): Promise<QueryResult<T>> {
    const key = queryIdentity(query.name, query.payload, query.ifRevision);
    let shared = inFlightQueries.get(key) as Promise<QueryResult<T>> | undefined;
    if (!shared) {
      shared = this.executeQuery(query, key);
      inFlightQueries.set(key, shared as Promise<QueryResult<unknown>>);
      void shared.finally(() => inFlightQueries.delete(key));
    }
    return withCancellation(shared, query.signal);
  }

  async command<T>(command: AppCommand<T>): Promise<CommandResult<T>> {
    const requestId = createId();
    const commandId = createId();
    const requestKey = `command:${command.name}`;
    setRequest(requestKey, { status: "pending", requestId, startedAt: Date.now() });
    if (command.signal?.aborted) return cancelledCommand(requestId, commandId);
    const response = await mauiCall<T>(command.name, command.payload, { requestId, commandId });
    if (!response.ok) {
      const error = normalizeError(response.error, response.errorInfo);
      setRequest(requestKey, { status: error.code === "cancelled" ? "cancelled" : "error", requestId, error: error.message, completedAt: Date.now() });
      return { ok: false, requestId, commandId, error };
    }
    const revision = command.projectionKey ? installConfirmed(command.projectionKey, command.domain, response.data) : installConfirmed(`command:${command.name}`, command.domain, response.data);
    setRequest(requestKey, { status: "success", requestId, completedAt: Date.now() });
    return { ok: true, requestId, commandId, revision, changedDomains: [command.domain], data: response.data };
  }

  subscribe(listener: (event: AppEvent) => void): () => void {
    eventListeners.add(listener);
    return () => eventListeners.delete(listener);
  }

  private async executeQuery<T>(query: AppQuery<T>, key: string): Promise<QueryResult<T>> {
    const requestId = createId();
    const requestKey = `query:${key}`;
    setRequest(requestKey, { status: "pending", requestId, startedAt: Date.now() });
    const response = await mauiCall<T>(query.name, query.payload, { requestId });
    if (!response.ok) {
      const error = normalizeError(response.error, response.errorInfo);
      setRequest(requestKey, { status: "error", requestId, error: error.message, completedAt: Date.now() });
      return { ok: false, requestId, error };
    }
    const revision = installConfirmed(query.projectionKey, query.domain, response.data);
    setRequest(requestKey, { status: "success", requestId, completedAt: Date.now() });
    return { ok: true, requestId, revision, data: response.data };
  }
}

export const appClient: AppClient = new DefaultAppClient();

function queryIdentity(name: string, payload: unknown, revision?: number): string {
  return `${name}|${stableJson(payload)}|${revision ?? ""}`;
}

function stableJson(value: unknown): string {
  if (value === undefined) return "";
  if (value === null || typeof value !== "object") return JSON.stringify(value);
  if (Array.isArray(value)) return `[${value.map(stableJson).join(",")}]`;
  return `{${Object.entries(value as Record<string, unknown>).sort(([a], [b]) => a.localeCompare(b)).map(([key, item]) => `${JSON.stringify(key)}:${stableJson(item)}`).join(",")}}`;
}

function normalizeError(message: string, transport?: TransportError): AppError {
  return transport ? { code: transport.code, message: transport.message, retryable: transport.retryable } : { code: "legacy_error", message, retryable: false };
}

function createId(): string { return globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`; }

function withCancellation<T>(promise: Promise<QueryResult<T>>, signal?: AbortSignal): Promise<QueryResult<T>> {
  if (!signal) return promise;
  if (signal.aborted) return Promise.resolve({ ok: false, requestId: createId(), error: { code: "cancelled", message: "Request cancelled.", retryable: false } });
  return new Promise((resolve) => {
    const cancel = () => resolve({ ok: false, requestId: createId(), error: { code: "cancelled", message: "Request cancelled.", retryable: false } });
    signal.addEventListener("abort", cancel, { once: true });
    void promise.then((value) => { signal.removeEventListener("abort", cancel); resolve(value); });
  });
}

function cancelledCommand<T>(requestId: string, commandId: string): CommandResult<T> {
  return { ok: false, requestId, commandId, error: { code: "cancelled", message: "Request cancelled.", retryable: false } };
}
