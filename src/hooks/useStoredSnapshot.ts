import { useCallback, useEffect, useState } from "react";
import { mauiCall } from "@/native/mauiWebberClient";
import { getAppState, setSettingsSection, useAppStore } from "@/state/appStore";

export function useStoredSnapshot<T>(method: string, payload: unknown, storeKey: string) {
  const payloadKey = JSON.stringify(payload);
  const stored = useAppStore((state) => state.settings[storeKey] as T | undefined);
  const [data, setLocalData] = useState<T | null>(() => stored ?? null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(!stored);

  const setData = useCallback((next: T) => {
    setLocalData(next);
    setSettingsSection(storeKey, next);
  }, [storeKey]);

  const refresh = useCallback(async (forceBackend = false) => {
    const current = getAppState().settings[storeKey] as T | undefined;
    if (current && !forceBackend) {
      setLocalData(current);
      setLoading(false);
      return;
    }

    setLoading(true);
    const res = await mauiCall<T>(method, payload);
    if (res.ok) {
      setData(res.data);
      setError(null);
    } else {
      setError(res.error);
    }
    setLoading(false);
  }, [method, payloadKey, setData, storeKey]);

  useEffect(() => {
    void refresh(false);
  }, [refresh]);

  return { data, error, loading, refresh, setData };
}
