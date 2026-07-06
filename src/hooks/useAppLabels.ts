import { useSnapshot } from "@/hooks/useSnapshot";
import { useEffect, useState } from "react";

type ShellLabels = {
  labels: Record<string, string>;
};

export type LabelProxy = Record<string, string>;

const emptyLabels: Record<string, string> = {};

export function createLabelProxy(labels: Record<string, string>): LabelProxy {
  return new Proxy(labels, {
    get(target, prop) {
      if (typeof prop !== "string") {
        return Reflect.get(target, prop);
      }

      return target[prop] ?? prop;
    },
  }) as LabelProxy;
}

export function useAppLabels() {
  const [version, setVersion] = useState(0);
  const { data } = useSnapshot<ShellLabels>("app.getShellSnapshot", undefined, [version]);
  useEffect(() => {
    const refresh = () => setVersion((value) => value + 1);
    window.addEventListener("prayadfree:shell-refresh", refresh);
    return () => window.removeEventListener("prayadfree:shell-refresh", refresh);
  }, []);
  const labels = createLabelProxy(data?.labels ?? emptyLabels);
  return (key: string) => labels[key];
}

export function refreshShellLabels() {
  window.dispatchEvent(new CustomEvent("prayadfree:shell-refresh"));
}
