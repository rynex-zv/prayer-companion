import { useSyncExternalStore } from "react";

export type RequestState = { status: "idle" | "pending" | "success" | "error" | "cancelled"; requestId?: string; error?: string; startedAt?: number; completedAt?: number };
export type OptimisticOperation = { key: string; domain: string; value: unknown; startedAt: number };

export type ClientState = {
  confirmed: Record<string, unknown>;
  revisions: { global: number; domains: Record<string, number>; eventSequence: number };
  optimistic: Record<string, OptimisticOperation>;
  requests: Record<string, RequestState>;
  ui: Record<string, unknown>;
};

const initialState: ClientState = {
  confirmed: {},
  revisions: { global: 0, domains: {}, eventSequence: 0 },
  optimistic: {},
  requests: {},
  ui: {},
};

let state = initialState;
const listeners = new Set<() => void>();

export function getClientState(): ClientState { return state; }
export function subscribeClientState(listener: () => void): () => void { listeners.add(listener); return () => listeners.delete(listener); }
export function useClientStore<T>(selector: (value: ClientState) => T): T {
  return useSyncExternalStore(subscribeClientState, () => selector(state), () => selector(state));
}

export function installConfirmed(key: string, domain: string, value: unknown): number {
  const revision = state.revisions.global + 1;
  state = {
    ...state,
    confirmed: { ...state.confirmed, [key]: value },
    revisions: { ...state.revisions, global: revision, domains: { ...state.revisions.domains, [domain]: revision } },
  };
  emit();
  return revision;
}

export function setRequest(key: string, request: RequestState): void {
  state = { ...state, requests: { ...state.requests, [key]: request } };
  emit();
}

export function setOptimistic(id: string, operation?: OptimisticOperation): void {
  const optimistic = { ...state.optimistic };
  if (operation) optimistic[id] = operation; else delete optimistic[id];
  state = { ...state, optimistic };
  emit();
}

export function setUi(key: string, value: unknown): void {
  state = { ...state, ui: { ...state.ui, [key]: value } };
  emit();
}

function emit(): void { listeners.forEach((listener) => listener()); }

