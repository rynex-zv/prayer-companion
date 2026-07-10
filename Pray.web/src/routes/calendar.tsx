import { createFileRoute } from "@tanstack/react-router";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";
import { Card, CardTitle } from "@/components/Card";
import { ChevronLeft, ChevronRight, CalendarDays } from "lucide-react";
import { cn } from "@/lib/utils";
import { PageLog } from "@/components/PageLog";
import { usePageLog } from "@/hooks/usePageLog";
import { useAppLabels } from "@/hooks/useAppLabels";

export const Route = createFileRoute("/calendar")({
  head: () => ({
    meta: [
      { title: "Calendar — Pray Ad Free" },
      { name: "description", content: "Monthly prayer time calendar." },
    ],
  }),
  component: CalendarPage,
});

type Day = { date: string; hijri: string; fajr: string; sunrise: string; dhuhr: string; asr: string; maghrib: string; isha: string; isToday?: boolean };
type Snapshot = { selectedMonth: string; selectedMonthValue?: string; statusMessage: string; days: Day[] };

function CalendarPage() {
  usePageLog("calendar");
  const t = useAppLabels();
  const { data, refresh } = useSnapshot<Snapshot>("calendar.getSnapshot");
  if (!data) return <div className="h-40 animate-pulse rounded-xl bg-muted" />;

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between gap-2">
        <h1 className="text-xl font-bold">{t("calendar")}</h1>
        <PageLog page="calendar" />
      </div>
      <Card className="flex items-center justify-between">
        <button onClick={() => mauiCall("calendar.previousMonth").then(refresh)} className="rounded-full p-2 hover:bg-muted" aria-label={t("previousMonth")}>
          <ChevronLeft className="h-5 w-5" />
        </button>
        <label className="flex items-center gap-2 font-semibold">
          <CalendarDays className="h-4 w-4 text-primary" />
          <input
            type="month"
            value={data.selectedMonthValue ?? ""}
            onChange={(event) => mauiCall("calendar.setMonth", { month: event.currentTarget.value }).then(refresh)}
            data-selector-name="calendar:month"
            className="w-32 rounded-md border border-input bg-card px-2 py-1 text-center text-sm"
            dir="ltr"
          />
          <span>{data.selectedMonth}</span>
        </label>
        <button onClick={() => mauiCall("calendar.nextMonth").then(refresh)} className="rounded-full p-2 hover:bg-muted" aria-label={t("nextMonth")}>
          <ChevronRight className="h-5 w-5" />
        </button>
      </Card>

      <div className="grid grid-cols-2 gap-2">
        <button onClick={() => mauiCall("calendar.today").then(refresh)} className="rounded-full bg-primary px-4 py-1.5 text-xs font-medium text-primary-foreground">
          {t("today")}
        </button>
        <button onClick={() => mauiCall("calendar.setMonth", { month: data.selectedMonthValue }).then(refresh)} data-selector-name="calendar:load" className="rounded-full border border-border bg-card px-4 py-1.5 text-xs font-medium">
          {t("load")}
        </button>
      </div>
      {data.statusMessage ? <p className="text-center text-xs text-muted-foreground">{data.statusMessage}</p> : null}

      <div className="space-y-2">
        {data.days.map((d) => (
          <Card key={d.date} className={cn("p-3", d.isToday && "ring-2 ring-primary")}>
            <div className="mb-2 flex items-center justify-between">
              <div>
                <div className="text-sm font-semibold">{d.date}</div>
                <div className="text-xs text-muted-foreground">{d.hijri}</div>
              </div>
              {d.isToday && <span className="rounded-full bg-primary px-2 py-0.5 text-[10px] font-bold text-primary-foreground">{t("todayBadge")}</span>}
            </div>
            <div className="grid grid-cols-3 gap-1.5 text-xs">
              {[
                [t("prayer_Fajr"), d.fajr], [t("prayer_Sunrise"), d.sunrise], [t("prayer_Dhuhr"), d.dhuhr],
                [t("prayer_Asr"), d.asr], [t("prayer_Maghrib"), d.maghrib], [t("prayer_Isha"), d.isha],
              ].map(([n, t]) => (
                <div key={n} className="rounded-md bg-muted/60 px-2 py-1.5">
                  <div className="text-[10px] uppercase text-muted-foreground">{n}</div>
                  <div className="font-medium tabular-nums" dir="ltr">{t}</div>
                </div>
              ))}
            </div>
          </Card>
        ))}
      </div>
    </div>
  );
}
