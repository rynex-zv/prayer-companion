import { createFileRoute } from "@tanstack/react-router";
import { traceClient } from "@/client/telemetry";
import { useToday } from "@/domains/today/todayClient";
import { Card, CardTitle } from "@/components/Card";
import { RefreshCw, MapPin } from "lucide-react";
import { cn } from "@/lib/utils";
import { useEffect, useMemo, useRef, useState } from "react";
import { PageLog } from "@/components/PageLog";
import { usePageLog } from "@/hooks/usePageLog";
import { useAppLabels } from "@/hooks/useAppLabels";
import { Link } from "@tanstack/react-router";

export const Route = createFileRoute("/")({
  head: () => ({
    meta: [],
  }),
  component: TodayPage,
});

function Time({ children }: { children: string }) {
  return <span dir="ltr" className="font-medium tabular-nums">{children}</span>;
}

function TodayPage() {
  usePageLog("today");
  const { data, refresh, loading } = useToday();
  const L = useAppLabels();
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
    if (!data || renderTraceSent.current) {
      return;
    }

    renderTraceSent.current = true;
    requestAnimationFrame(() => {
      traceClient("renderComplete", { route: "today", timingCount: data.todayTimings.length });
    });
  }, [data]);

  if (!data) return <SkeletonToday />;
  const text = L;
  const prayer = (id: string) => text(`prayer_${id[0].toUpperCase()}${id.slice(1).toLowerCase()}`);

  if (data.error) {
    return (
      <div role="alert" data-selector-name="today:error" className="mx-auto mt-10 max-w-md rounded-xl border border-destructive/30 bg-destructive/10 p-5 text-center">
        <h1 className="text-lg font-semibold text-destructive">{data.error}</h1>
        <p className="mt-2 text-sm text-muted-foreground">{L("method")}: {data.calculation.selectedMethodLabel}</p>
        <Link to="/settings/adhan" className="mt-4 inline-block rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground">
          {L("adhan")}
        </Link>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-center gap-2">
        <p className="text-center text-sm font-medium text-primary" dir={data.isRtl ? "rtl" : "ltr"}>{L("basmala")}</p>
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
            onClick={() => { void refresh(); }}
            className="rounded-full p-2 text-muted-foreground hover:bg-muted"
            aria-label={L("refresh")}
          >
            <RefreshCw className={cn("h-4 w-4", loading && "animate-spin")} />
          </button>
        </div>
      </Card>

      <Card className="grid gap-1 text-xs text-muted-foreground" data-selector-name="today:calculation-summary">
        <div className="flex justify-between gap-3">
          <span>{L("method")}</span>
          <span className="text-right text-card-foreground">
            {data.calculation.selectedMethod === "Auto"
              ? `${data.calculation.selectedMethodLabel} → ${data.calculation.effectiveMethodLabel}`
              : data.calculation.effectiveMethodLabel}
          </span>
        </div>
        <div className="flex justify-between gap-3">
          <span>{L("madhhab")}</span>
          <span className="text-right text-card-foreground">{data.calculation.madhhabLabel}</span>
        </div>
        <div className="flex justify-between gap-3">
          <span>{L("highLatitudeRule")}</span>
          <span className="text-right text-card-foreground">{data.calculation.highLatitudeRuleLabel}</span>
        </div>
      </Card>

      {/* Next prayer hero */}
      <Card className="overflow-hidden border-0 p-0 shadow-[var(--shadow-hero)]">
        <div className="p-5 text-primary-foreground" style={{ background: "var(--gradient-primary)" }}>
          <div className="text-xs uppercase tracking-widest opacity-80">{text("nextPrayer")} · {text(data.nextPrayerDayId)}</div>
          <div className="mt-1 flex items-end justify-between gap-3">
            <div className="text-3xl font-bold">{prayer(data.nextPrayerId)}</div>
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
        <CardTitle className="mb-2">{L("today")}</CardTitle>
        <ul className="divide-y divide-border">
          {data.todayTimings.map((t) => (
            <li key={t.id} className={cn("flex items-center justify-between py-2.5", t.isNext && "font-semibold text-primary")}>
              <span className="flex items-center gap-2">
                <span className={cn("h-2 w-2 rounded-full", t.isNext ? "bg-primary" : "bg-accent")} />
                {prayer(t.id)}
              </span>
              <Time>{t.time}</Time>
            </li>
          ))}
        </ul>
      </Card>

      {/* Imsak / Iftar */}
      <div className="grid grid-cols-2 gap-3">
        <Card className={cn("bg-secondary/70", data.isImsakNext && "ring-2 ring-primary")}>
          <CardTitle>{L("imsak")}</CardTitle>
          <div className="mt-1 text-xl font-semibold"><Time>{data.imsakTime}</Time></div>
        </Card>
        <Card className={cn("bg-accent/70 text-accent-foreground", data.isIftarNext && "ring-2 ring-primary")}>
          <CardTitle>{L("iftar")}</CardTitle>
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
