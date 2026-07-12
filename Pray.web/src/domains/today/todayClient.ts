import { useCallback, useEffect } from "react";
import { appClient } from "@/client/appClient";
import { useClientStore } from "@/client/clientStore";
import { bootstrapAppState } from "@/state/appStore";

export type TodaySnapshot = {
  locationTitle: string; hijriDate: string; gregorianDate: string; currentTime?: string;
  nextPrayerId: string; nextPrayerClock: string; nextPrayerBaseClock: string;
  showNextPrayerBaseClock: boolean; nextPrayerDayId: string; countdown: string; statusMessage: string;
  imsakTime: string; iftarTime: string; isImsakNext: boolean; isIftarNext: boolean;
  nextFastingCountdown: string; isRtl: boolean; labels: Record<string, string>;
  todayTimings: { id: string; time: string; baseTime?: string; isNext: boolean }[];
};

const PROJECTION = "today.snapshot";

export function useToday() {
  const data = useClientStore((state) => state.confirmed[PROJECTION] as TodaySnapshot | undefined) ?? null;
  const loading = useClientStore((state) => state.requests["command:today.refresh"]?.status === "pending" || (!data && state.requests["query:app.bootstrap||"]?.status === "pending"));
  const refresh = useCallback(async () => {
    await appClient.command<TodaySnapshot>({ name: "today.refresh", domain: "today", projectionKey: PROJECTION });
  }, []);
  useEffect(() => { void bootstrapAppState(); }, []);
  useEffect(() => appClient.subscribe((event) => { if (event.domain === "today" && event.type !== "domain.changed") void refresh(); }), [refresh]);
  return { data, loading, refresh };
}
