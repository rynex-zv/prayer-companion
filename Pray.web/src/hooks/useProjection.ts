import { useCallback, useEffect, useState } from "react";
import { appClient } from "@/client/appClient";
import { getClientState, installConfirmed, useClientStore } from "@/client/clientStore";

/** Selects a confirmed projection and queries it only when missing or explicitly refreshed. */
export function useProjection<T>(method: string, payload?: unknown, explicitKey?: string) {
  const projectionKey = explicitKey ?? `${method.replace(/\.(getSnapshot|refresh)$/, "")}.snapshot`;
  const payloadKey = JSON.stringify(payload);
  const confirmed = useClientStore((state) => state.confirmed[projectionKey] as T | undefined);
  const [localData, setLocalData] = useState<T | null>(null);
  const data = localData ?? confirmed ?? null;
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(!data);

  const setData = useCallback((next: T) => {
    setLocalData(next);
    installConfirmed(projectionKey, method.split(".", 1)[0], next);
  }, [method, projectionKey]);

  const refresh = useCallback(async (force = false) => {
    const current = getClientState().confirmed[projectionKey];
    setLoading(force || current === undefined);
    const result = await appClient.query<T>({
      name: method,
      payload,
      domain: method.split(".", 1)[0],
      projectionKey,
    });
    if (result.ok) {
      if (!result.notModified) setData(result.data);
      setError(null);
    } else {
      setError(result.error.message);
    }
    setLoading(false);
  }, [method, payloadKey, projectionKey, setData]);

  useEffect(() => {
    if (getClientState().confirmed[projectionKey] === undefined) void refresh(false);
  }, [confirmed, projectionKey, refresh]);

  return { data, error, loading: loading && !data, refresh, setData };
}
