import { useCallback, useEffect, useRef } from "react";
import { appClient } from "@/client/appClient";
import { useClientStore } from "@/client/clientStore";
import { bootstrapAppState } from "@/state/appStore";
import { useAppStore } from "@/state/appStore";

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
  const language = useAppStore((state) => state.language);
  const bootstrapStatus = useAppStore((state) => state.bootstrapStatus);
  const bootstrappedLanguage = useRef<string | null>(null);
  const data = useClientStore((state) => state.confirmed[PROJECTION] as TodaySnapshot | undefined) ?? null;
  const loading = useClientStore((state) => state.requests["command:today.refresh"]?.status === "pending" || (!data && state.requests["query:app.bootstrap||"]?.status === "pending"));
  const refresh = useCallback(async () => {
    await appClient.command<TodaySnapshot>({ name: "today.refresh", domain: "today", projectionKey: PROJECTION });
  }, []);
  useEffect(() => { void bootstrapAppState(); }, []);
  useEffect(() => {
    // Bootstrap contains a Today projection in the persisted language. Whenever
    // the language changes afterwards, replace that projection immediately so
    // page content and the localized shell can never drift apart.
    if (bootstrapStatus !== "ready") return;
    if (bootstrappedLanguage.current === null) {
      bootstrappedLanguage.current = language;
      return;
    }
    if (bootstrappedLanguage.current !== language) {
      bootstrappedLanguage.current = language;
      void refresh();
    }
  }, [bootstrapStatus, language, refresh]);
  useEffect(() => appClient.subscribe((event) => { if (event.domain === "today" && event.type !== "domain.changed") void refresh(); }), [refresh]);
  return { data, loading, refresh };
}
