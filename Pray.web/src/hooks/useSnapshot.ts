import { useEffect, useState, useCallback } from "react";
import { appClient } from "@/client/appClient";
import { getClientState, installConfirmed, useClientStore } from "@/client/clientStore";

export function useSnapshot<T>(method: string, payload?: unknown, deps: unknown[] = []) {
  const projectionKey = `${method.replace(/\.(getSnapshot|refresh)$/, "")}.snapshot`;
  const confirmed = useClientStore((state) => state.confirmed[projectionKey] as T | undefined);
  const [localData, setLocalData] = useState<T | null>(null);
  const data = localData ?? confirmed ?? null;
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const refresh = useCallback(async (silentOrEvent?: unknown) => {
    const silent = silentOrEvent === true;
    if (!silent) {
      setLoading(true);
    }
    const res = await appClient.query<T>({ name: method, payload, domain: method.split(".", 1)[0], projectionKey });
    if (res.ok) {
      setLocalData(res.data);
      setError(null);
    } else {
      setError(res.error.message);
    }
    if (!silent) {
      setLoading(false);
    }
  }, [method, JSON.stringify(payload)]);

  useEffect(() => { if (!getClientState().confirmed[projectionKey]) void refresh(); /* eslint-disable-next-line */ }, [method, JSON.stringify(payload), ...deps]);
  const setData = useCallback((next: T) => { setLocalData(next); installConfirmed(projectionKey, method.split(".", 1)[0], next); }, [projectionKey, method]);
  return { data, error, loading: loading && !data, refresh, setData };
}
