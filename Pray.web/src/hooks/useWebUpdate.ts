import { useEffect, useState } from "react";
import type { WebUpdateSnapshot } from "@/native/webUpdateTypes";

const emptySnapshot: WebUpdateSnapshot = {
  status: "idle",
  currentVersion: "",
  latestVersion: "",
  availableVersion: "",
  required: false,
  error: "",
};

export function useWebUpdate(): WebUpdateSnapshot & { apply: () => Promise<void> } {
  const [snapshot, setSnapshot] = useState<WebUpdateSnapshot>(() => window.__prayWebUpdate?.getSnapshot() ?? emptySnapshot);

  useEffect(() => {
    const api = window.__prayWebUpdate;
    if (!api) return undefined;
    return api.subscribe(setSnapshot);
  }, []);

  return {
    ...snapshot,
    apply: async () => {
      await window.__prayWebUpdate?.apply();
    },
  };
}

export function getWebUpdateSnapshot(): WebUpdateSnapshot {
  return window.__prayWebUpdate?.getSnapshot() ?? emptySnapshot;
}
