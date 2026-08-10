import { useCallback, useEffect } from "react";
import { appClient } from "@/client/appClient";
import { getClientState, useClientStore } from "@/client/clientStore";

export type TasbihSnapshot = {
  count: number;
  currentPhrase: string;
  progressText: string;
  isPresetSelectionEnabled: boolean;
  selectedPresetId: string;
  presets: { id: string; name: string; repeatMode: string; items: { text: string; targetCount: number }[] }[];
  [key: string]: unknown;
};

const PROJECTION = "tasbih.snapshot";

export function useTasbih(): {
  data: TasbihSnapshot | null;
  loading: boolean;
  error?: string;
  refresh: () => Promise<void>;
  increment: () => Promise<void>;
  reset: () => Promise<void>;
  selectPreset: (id: string) => Promise<void>;
  invokeSettings: (action: string, payload: unknown) => Promise<boolean>;
} {
  const data = useClientStore((state) => state.confirmed[PROJECTION] as TasbihSnapshot | undefined) ?? null;
  const request = useClientStore((state) => state.requests["query:tasbih.getSnapshot||"]);

  const refresh = useCallback(async () => {
    await appClient.query<TasbihSnapshot>({ name: "tasbih.getSnapshot", domain: "tasbih", projectionKey: PROJECTION });
  }, []);
  useEffect(() => { if (!getClientState().confirmed[PROJECTION]) void refresh(); }, [refresh]);

  const run = useCallback(async (name: string, payload?: unknown) => {
    await appClient.command<TasbihSnapshot>({ name, payload, domain: "tasbih", projectionKey: PROJECTION });
  }, []);

  return {
    data,
    loading: !data && (!request || request.status === "pending"),
    error: request?.error,
    refresh,
    increment: () => run("tasbih.increment"),
    reset: () => run("tasbih.reset"),
    selectPreset: (id) => run("tasbih.selectPreset", { id }),
    invokeSettings: async (action, payload) => {
      const methods: Record<string, string> = {
        addTasbihPreset: "tasbih.addPreset",
        updateTasbihPreset: "tasbih.updatePreset",
        removeTasbihPreset: "tasbih.removePreset",
        addTasbihItem: "tasbih.addItem",
        updateTasbihItem: "tasbih.updateItem",
        moveTasbihItem: "tasbih.moveItem",
        removeTasbihItem: "tasbih.removeItem",
      };
      const result = await appClient.command<TasbihSnapshot>({ name: methods[action] ?? action, payload, domain: "tasbih", projectionKey: PROJECTION });
      return result.ok;
    },
  };
}
