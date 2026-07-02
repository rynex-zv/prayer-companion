import { useEffect, useState, useCallback } from "react";
import { flushSync } from "react-dom";
import { mauiCall, mauiTrace } from "@/native/mauiWebberClient";

export function useSnapshot<T>(method: string, payload?: unknown, deps: unknown[] = []) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const refresh = useCallback(async () => {
    setLoading(true);
    const res = await mauiCall<T>(method, payload);
    mauiTrace("snapshot.result", {
      method,
      ok: res.ok,
      hasData: res.ok ? res.data != null : false,
    });
    if (res.ok) {
      flushSync(() => {
        setData(res.data);
        setError(null);
        setLoading(false);
      });
      mauiTrace("snapshot.setData", { method, hasData: res.data != null });
    } else {
      flushSync(() => {
        setError(res.error);
        setLoading(false);
      });
    }
    mauiTrace("snapshot.setLoadingFalse", { method });
  }, [method, JSON.stringify(payload)]);

  useEffect(() => { refresh(); /* eslint-disable-next-line */ }, [method, JSON.stringify(payload), ...deps]);
  return { data, error, loading, refresh, setData };
}
