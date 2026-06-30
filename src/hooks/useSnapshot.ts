import { useEffect, useState, useCallback } from "react";
import { mauiCall } from "@/native/mauiWebberClient";

export function useSnapshot<T>(method: string, payload?: unknown, deps: unknown[] = []) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const refresh = useCallback(async () => {
    setLoading(true);
    const res = await mauiCall<T>(method, payload);
    if (res.ok) { setData(res.data); setError(null); }
    else { setError(res.error); }
    setLoading(false);
  }, [method, JSON.stringify(payload)]);

  useEffect(() => { refresh(); /* eslint-disable-next-line */ }, [method, JSON.stringify(payload), ...deps]);
  return { data, error, loading, refresh };
}
