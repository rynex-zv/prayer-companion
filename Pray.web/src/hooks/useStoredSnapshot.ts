import { useCallback, useEffect, useState } from "react";
import { appClient } from "@/client/appClient";
import { getClientState, installConfirmed, useClientStore } from "@/client/clientStore";

export function useStoredSnapshot<T>(method: string, payload: unknown, storeKey: string) {
  const payloadKey = JSON.stringify(payload);
  const stored = useClientStore((state) => state.confirmed[storeKey] as T | undefined);
  const [data, setLocalData] = useState<T | null>(() => stored ?? null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(!stored);

  const setData = useCallback((next: T) => {
    setLocalData(next);
    installConfirmed(storeKey, method.split(".", 1)[0], next);
  }, [storeKey]);

  const refresh = useCallback(async (forceBackend = false) => {
    const current = getClientState().confirmed[storeKey] as T | undefined;
    setLoading(forceBackend || !current);
    const res = await appClient.query<T>({ name: method, payload, domain: method.split(".", 1)[0], projectionKey: storeKey });
    if (res.ok) {
      setData(res.data);
      setError(null);
    } else {
      setError(res.error.message);
    }
    setLoading(false);
  }, [method, payloadKey, setData, storeKey]);

  useEffect(() => {
    void refresh(false);
  }, [refresh]);

  return { data, error, loading, refresh, setData };
}
