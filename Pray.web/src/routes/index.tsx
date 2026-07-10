import { createFileRoute } from "@tanstack/react-router";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall, mauiTrace } from "@/native/mauiWebberClient";
import { Card, CardTitle } from "@/components/Card";
import { RefreshCw, MapPin } from "lucide-react";
import { cn } from "@/lib/utils";
import { useEffect, useMemo, useRef, useState } from "react";
import { PageLog } from "@/components/PageLog";
import { usePageLog } from "@/hooks/usePageLog";

export const Route = createFileRoute("/")({
  head: () => ({
    meta: [
      { title: "Today — Pray Ad Free" },
      { name: "description", content: "Today's prayer times and countdown to the next prayer." },
    ],
  }),
  component: TodayPage,
});

type Timing = { id: string; name: string; time: string; baseTime?: string; isNext: boolean };
type Today = {
  locationTitle: string; hijriDate: string; gregorianDate: string;
  currentTime?: string;
  nextPrayerName: string; nextPrayerClock: string; nextPrayerBaseClock: string;
  showNextPrayerBaseClock: boolean; nextPrayerDayLabel: string;
  countdown: string; statusMessage: string;
  imsakTime: string; iftarTime: string;
  isImsakNext: boolean; isIftarNext: boolean;
  nextFastingCountdown: string;
  isRtl: boolean; labels: Record<string, string>;
  todayTimings: Timing[];
};

function Time({ children }: { children: string }) {
  return <span dir="ltr" className="font-medium tabular-nums">{children}</span>;
}

function TodayPage() {
  usePageLog("today");
  const { data, refresh, loading } = useSnapshot<Today>("today.getSnapshot");
  const [now, setNow] = useState(() => new Date());
  const renderTraceSent = useRef(false);
  const currentTime = useMemo(() => formatCurrentTime(now, data?.currentTime), [now, data?.currentTime]);

  useEffect(() => {
    const timer = window.setInterval(() => {
      setNow(new Date());
    }, 1000);

    return () => window.clearInterval(timer);
  }, []);

  useEffect(() => {
    const timer = window.setInterval(() => {
      void refresh(true);
    }, 30000);

    return () => window.clearInterval(timer);
  }, [refresh]);

  useEffect(() => {
    if (!data || renderTraceSent.current) {
      return;
    }

    renderTraceSent.current = true;
    requestAnimationFrame(() => {
      mauiTrace("renderComplete", { route: "today", timingCount: data.todayTimings.length });
    });
  }, [data]);

  if (!data) return <SkeletonToday />;
  const L = data.labels;

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-center gap-2">
        <p className="text-center text-sm font-medium text-primary" dir={data.isRtl ? "rtl" : "ltr"}>{L.basmala}</p>
        <PageLog page="today" />
      </div>

      <Card className="flex items-center justify-between">
        <div className="flex items-start gap-2">
          <MapPin className="mt-0.5 h-4 w-4 text-primary" />
          <div>
            <div className="font-semibold">{data.locationTitle}</div>
            <div className="text-xs text-muted-foreground">{data.gregorianDate}</div>
            <div className="text-xs text-muted-foreground">{data.hijriDate}</div>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {currentTime ? (
            <div data-selector-name="today:current-time" className="rounded-md bg-secondary px-2 py-1 text-sm font-semibold text-secondary-foreground tabular-nums" dir="ltr">
              {currentTime}
            </div>
          ) : null}
          <button
            onClick={() => { mauiCall("today.refresh").then(refresh); }}
            className="rounded-full p-2 text-muted-foreground hover:bg-muted"
            aria-label="Refresh"
          >
            <RefreshCw className={cn("h-4 w-4", loading && "animate-spin")} />
          </button>
        </div>
      </Card>

      {/* Next prayer hero */}
      <Card className="overflow-hidden border-0 p-0 shadow-[var(--shadow-hero)]">
        <div className="p-5 text-primary-foreground" style={{ background: "var(--gradient-primary)" }}>
          <div className="text-xs uppercase tracking-widest opacity-80">{L.nextPrayer} · {data.nextPrayerDayLabel}</div>
          <div className="mt-1 flex items-end justify-between gap-3">
            <div className="text-3xl font-bold">{data.nextPrayerName}</div>
            <div className="text-right">
              <div className="text-2xl font-semibold"><Time>{data.nextPrayerClock}</Time></div>
              {data.showNextPrayerBaseClock && (
                <div className="text-xs opacity-80 line-through"><Time>{data.nextPrayerBaseClock}</Time></div>
              )}
            </div>
          </div>
          <div className="mt-3 rounded-lg bg-black/15 px-3 py-2 text-center text-lg font-bold tabular-nums">
            <Time>{data.countdown}</Time>
          </div>
        </div>
      </Card>

      {/* Timings */}
      <Card className="border-primary/20 bg-card/95">
        <CardTitle className="mb-2">{L.today}</CardTitle>
        <ul className="divide-y divide-border">
          {data.todayTimings.map((t) => (
            <li key={t.id} className={cn("flex items-center justify-between py-2.5", t.isNext && "font-semibold text-primary")}>
              <span className="flex items-center gap-2">
                <span className={cn("h-2 w-2 rounded-full", t.isNext ? "bg-primary" : "bg-accent")} />
                {t.name}
              </span>
              <Time>{t.time}</Time>
            </li>
          ))}
        </ul>
      </Card>

      {/* Imsak / Iftar */}
      <div className="grid grid-cols-2 gap-3">
        <Card className={cn("bg-secondary/70", data.isImsakNext && "ring-2 ring-primary")}>
          <CardTitle>{L.imsak}</CardTitle>
          <div className="mt-1 text-xl font-semibold"><Time>{data.imsakTime}</Time></div>
        </Card>
        <Card className={cn("bg-accent/70 text-accent-foreground", data.isIftarNext && "ring-2 ring-primary")}>
          <CardTitle>{L.iftar}</CardTitle>
          <div className="mt-1 text-xl font-semibold"><Time>{data.iftarTime}</Time></div>
        </Card>
      </div>

      {data.statusMessage ? (
        <p className="text-center text-xs text-muted-foreground">{data.statusMessage}</p>
      ) : null}
    </div>
  );
}

function formatCurrentTime(now: Date, sample?: string) {
  if (!sample) {
    return "";
  }

  const useTwelveHourClock = /\b(?:AM|PM)\b/i.test(sample);
  return now.toLocaleTimeString(undefined, {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: useTwelveHourClock,
  });
}

function SkeletonToday() {
  return (
    <div className="space-y-3">
      {Array.from({ length: 4 }).map((_, i) => (
        <div key={i} className="h-24 animate-pulse rounded-xl bg-muted" />
      ))}
    </div>
  );
}
