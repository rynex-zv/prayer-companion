import { useCallback, useEffect, useState } from "react";
import { mauiCall } from "@/native/mauiWebberClient";
import { getAppState, setSettingsSection, useAppStore } from "@/state/appStore";

export function useStoredSnapshot<T>(method: string, payload: unknown, storeKey: string) {
  const payloadKey = JSON.stringify(payload);
  const stored = useAppStore((state) => state.settings[storeKey] as T | undefined);
  const canUseStoredInitial =
    typeof window !== "undefined" &&
    Boolean(window.mauiWebber);
  const [data, setLocalData] = useState<T | null>(() => canUseStoredInitial ? stored ?? null : null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(!canUseStoredInitial || !stored);

  const setData = useCallback((next: T) => {
    setLocalData(next);
    setSettingsSection(storeKey, next);
  }, [storeKey]);

  const refresh = useCallback(async (forceBackend = false) => {
    const current = canUseStoredInitial ? getAppState().settings[storeKey] as T | undefined : undefined;
    setLoading(forceBackend || !current);
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
