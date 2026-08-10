import { mauiCall, type TransportError } from "@/native/mauiWebberClient";
import { applyAppEvent, getClientState, installBootstrap, installConfirmed, setRequest } from "./clientStore";

export type AppError = { code: string; message: string; retryable: boolean; details?: unknown };
export type QueryResult<T> = { ok: true; requestId: string; revision: number; data: T; notModified?: boolean } | { ok: false; requestId: string; error: AppError };
export type CommandResult<T> = { ok: true; requestId: string; commandId: string; revision: number; changedDomains: string[]; data: T } | { ok: false; requestId: string; commandId: string; error: AppError };
export type AppEvent = { sequence: number; eventId: string; timestamp: string; domain: string; type: string; revision: number; causeRequestId?: string; payload?: unknown; invalidationKey?: string };
export type BootstrapResult = {
  contractVersion: number;
  persistenceSchemaVersion: number;
  revisions: { global: number; domains: Record<string, number>; eventSequence: number };
  startup: { route: string; intent?: string };
  projections: Record<string, unknown>;
};

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
  private readonly broadcast = typeof BroadcastChannel !== "undefined" ? new BroadcastChannel("prayer-companion:app-events:v2") : undefined;

  constructor() {
    if (typeof window !== "undefined") {
      window.addEventListener("mauiwebber:app-event", (event) => this.acceptEvent((event as CustomEvent<AppEvent>).detail));
    }
    if (this.broadcast) this.broadcast.onmessage = (message) => this.acceptEvent(message.data as AppEvent);
  }

  bootstrap<T>(request: Omit<AppQuery<T>, "name">): Promise<QueryResult<T>> {
    return this.query({ ...request, name: "app.bootstrap" });
  }

  query<T>(query: AppQuery<T>): Promise<QueryResult<T>> {
    const current = getClientState();
    const effectiveQuery = {
      ...query,
      ifRevision: query.ifRevision ?? (current.confirmed[query.projectionKey] !== undefined ? current.revisions.domains[query.domain] : undefined),
    };
    const key = queryIdentity(effectiveQuery.name, effectiveQuery.payload, effectiveQuery.ifRevision);
    let shared = inFlightQueries.get(key) as Promise<QueryResult<T>> | undefined;
    if (!shared) {
      shared = this.executeQuery(effectiveQuery, key);
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
    const response = await mauiCall<T>(command.name, command.payload, {
      requestId,
      commandId,
      domain: command.domain,
      expectedRevision: command.expectedRevision,
    });
    if (!response.ok) {
      const error = normalizeError(response.error, response.errorInfo);
      setRequest(requestKey, { status: error.code === "cancelled" ? "cancelled" : "error", requestId, error: error.message, completedAt: Date.now() });
      return { ok: false, requestId, commandId, error };
    }
    this.acceptEvents(response.events);
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
    const payload = addQueryMetadata(query.payload, query.ifRevision);
    const response = await mauiCall<T>(query.name, payload, { requestId, domain: query.domain });
    if (!response.ok) {
      const error = normalizeError(response.error, response.errorInfo);
      setRequest(requestKey, { status: "error", requestId, error: error.message, completedAt: Date.now() });
      return { ok: false, requestId, error };
    }
    this.acceptEvents(response.events);
    if (isNotModified(response.data)) {
      setRequest(requestKey, { status: "success", requestId, completedAt: Date.now() });
      return { ok: true, requestId, revision: response.data.revision, data: undefined as T, notModified: true };
    }
    let revision: number;
    if (query.name === "app.bootstrap") {
      const bootstrap = response.data as BootstrapResult;
      installBootstrap(bootstrap.projections, bootstrap.revisions);
      revision = bootstrap.revisions.global;
    } else {
      revision = installConfirmed(query.projectionKey, query.domain, response.data);
    }
    setRequest(requestKey, { status: "success", requestId, completedAt: Date.now() });
    return { ok: true, requestId, revision, data: response.data };
  }

  private acceptEvents(events?: unknown[]): void {
    for (const value of events ?? []) {
      const event = value as AppEvent;
      this.acceptEvent(event);
      this.broadcast?.postMessage(event);
    }
  }

  private acceptEvent(event: AppEvent): void {
    if (!event) return;
    const applied = applyAppEvent(event);
    // Platform completion is a one-shot correlation signal. A newer domain
    // revision may legitimately arrive first, but the waiting caller must still
    // receive the completion for its operation ID.
    if (applied || event.type.startsWith("platform.operation.")) {
      eventListeners.forEach((listener) => listener(event));
    }
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

function addQueryMetadata(payload: unknown, ifRevision?: number): unknown {
  const body = payload && typeof payload === "object" && !Array.isArray(payload) ? payload as Record<string, unknown> : {};
  return { ...body, _query: { ifRevision } };
}

function isNotModified(value: unknown): value is { notModified: true; revision: number } {
  return typeof value === "object" && value !== null && (value as { notModified?: unknown }).notModified === true;
}
