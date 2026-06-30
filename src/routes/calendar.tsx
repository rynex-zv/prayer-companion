import { createFileRoute } from "@tanstack/react-router";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";
import { Card, CardTitle } from "@/components/Card";
import { ChevronLeft, ChevronRight, CalendarDays } from "lucide-react";
import { cn } from "@/lib/utils";

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
type Snapshot = { selectedMonth: string; statusMessage: string; days: Day[] };

function CalendarPage() {
  const { data, refresh } = useSnapshot<Snapshot>("calendar.getSnapshot");
  if (!data) return <div className="h-40 animate-pulse rounded-xl bg-muted" />;

  return (
    <div className="flex flex-col gap-3">
      <h1 className="text-xl font-bold">Calendar</h1>
      <Card className="flex items-center justify-between">
        <button onClick={() => mauiCall("calendar.previousMonth").then(refresh)} className="rounded-full p-2 hover:bg-muted" aria-label="Previous month">
          <ChevronLeft className="h-5 w-5" />
        </button>
        <div className="flex items-center gap-2 font-semibold">
          <CalendarDays className="h-4 w-4 text-primary" />
          {data.selectedMonth}
        </div>
        <button onClick={() => mauiCall("calendar.nextMonth").then(refresh)} className="rounded-full p-2 hover:bg-muted" aria-label="Next month">
          <ChevronRight className="h-5 w-5" />
        </button>
      </Card>

      <button onClick={() => mauiCall("calendar.today").then(refresh)} className="self-center rounded-full bg-primary px-4 py-1.5 text-xs font-medium text-primary-foreground">
        Today
      </button>

      <div className="space-y-2">
        {data.days.map((d) => (
          <Card key={d.date} className={cn("p-3", d.isToday && "ring-2 ring-primary")}>
            <div className="mb-2 flex items-center justify-between">
              <div>
                <div className="text-sm font-semibold">{d.date}</div>
                <div className="text-xs text-muted-foreground">{d.hijri}</div>
              </div>
              {d.isToday && <span className="rounded-full bg-primary px-2 py-0.5 text-[10px] font-bold text-primary-foreground">TODAY</span>}
            </div>
            <div className="grid grid-cols-3 gap-1.5 text-xs">
              {[
                ["Fajr", d.fajr], ["Sunrise", d.sunrise], ["Dhuhr", d.dhuhr],
                ["Asr", d.asr], ["Maghrib", d.maghrib], ["Isha", d.isha],
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
