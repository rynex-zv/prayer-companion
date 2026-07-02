import { useSnapshot } from "@/hooks/useSnapshot";
import { useEffect, useState } from "react";

type ShellLabels = {
  labels: Record<string, string>;
};

export function useAppLabels() {
  const [version, setVersion] = useState(0);
  const { data } = useSnapshot<ShellLabels>("app.getShellSnapshot", undefined, [version]);
  useEffect(() => {
    const refresh = () => setVersion((value) => value + 1);
    window.addEventListener("prayadfree:shell-refresh", refresh);
    return () => window.removeEventListener("prayadfree:shell-refresh", refresh);
  }, []);
  const labels = data?.labels ?? {};
  return (key: string, fallback: string) => labels[key] ?? fallback;
}

export function refreshShellLabels() {
  window.dispatchEvent(new CustomEvent("prayadfree:shell-refresh"));
}
