import { createFileRoute } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { useProjection } from "@/hooks/useProjection";
import { executeCommand } from "@/client/applicationClient";
import { Card } from "@/components/Card";
import { ChevronLeft, ChevronRight, CalendarDays, X, Star } from "lucide-react";
import { cn } from "@/lib/utils";
import { PageLog } from "@/components/PageLog";
import { usePageLog } from "@/hooks/usePageLog";
import { useAppLabels } from "@/hooks/useAppLabels";

export const Route = createFileRoute("/calendar")({
  head: () => ({ meta: [] }),
  component: CalendarPage,
});

type Day = {
  sourceDate: string;
  weekday: number; // 0 Sunday..6 Saturday
  dayNumber: number;
  date: string;
  hijri: string;
  hijriDay: number;
  hijriMonth: number;
  hijriMonthName: string;
  hijriYear: number;
  fajr: string; sunrise: string; dhuhr: string; asr: string; maghrib: string; isha: string;
  isToday?: boolean;
  occasionKey?: string | null;
  occasionColor?: string | null;
  occasionImportance?: string | null;
};

type Snapshot = {
  selectedMonth: string;
  selectedMonthValue: string;
  monthName: string;
  hijriMonthLabel: string;
  yearNumber: number;
  monthNumber: number;
  statusMessage: string;
  days: Day[];
};

type ViewMode = "year" | "month" | "week" | "day";
type CalendarMode = "gregorian" | "hijri";

function CalendarPage() {
  usePageLog("calendar");
  const t = useAppLabels();
  const { data, refresh, setData } = useProjection<Snapshot>("calendar.getSnapshot");

  const [view, setView] = useState<ViewMode>("month");
  const [mode, setMode] = useState<CalendarMode>("gregorian");
  const [selectedIso, setSelectedIso] = useState<string | null>(null);

  if (!data) return <div className="h-40 animate-pulse rounded-xl bg-muted" />;
  const snapshot = data;

  const selectedDay = selectedIso ? data.days.find((d) => d.sourceDate === selectedIso) ?? null : null;
  const todayDay = data.days.find((d) => d.isToday) ?? null;

  async function navigate(direction: -1 | 1) {
    if (view === "month") {
      const result = await executeCommand<Snapshot>(direction < 0 ? "calendar.previousMonth" : "calendar.nextMonth");
      if (result.ok) setData(result.data);
      setSelectedIso(null);
      return;
    }

    if (view === "year") {
      const target = new Date(snapshot.yearNumber + direction, snapshot.monthNumber - 1, 1);
      const month = `${target.getFullYear()}-${String(target.getMonth() + 1).padStart(2, "0")}`;
      const result = await executeCommand<Snapshot>("calendar.setMonth", { month });
      if (result.ok) setData(result.data);
      setSelectedIso(null);
      return;
    }

    const anchor = selectedDay ?? todayDay ?? snapshot.days[0];
    if (!anchor) return;
    const target = new Date(`${anchor.sourceDate}T12:00:00`);
    target.setDate(target.getDate() + direction * (view === "week" ? 7 : 1));
    const iso = target.toISOString().slice(0, 10);
    const month = iso.slice(0, 7);
    if (month !== snapshot.selectedMonthValue) {
      const result = await executeCommand<Snapshot>("calendar.setMonth", { month });
      if (result.ok) setData(result.data);
    }
    setSelectedIso(iso);
  }

  return (
    <div className="flex flex-col gap-3 pb-4">
      <div className="flex items-center justify-between gap-2">
        <h1 className="text-xl font-bold">{t("calendar")}</h1>
        <PageLog page="calendar" />
      </div>

      <Card className="flex flex-col gap-3">
        <div className="flex items-center justify-between gap-2" dir="ltr">
          <button
            onClick={() => navigate(-1)}
            className="rounded-full p-2 hover:bg-muted"
            aria-label={t("previousMonth")}
          >
            <span dir="ltr"><ChevronLeft className="h-5 w-5" /></span>
          </button>
          <div className="flex flex-col items-center leading-tight" dir="auto">
            <div className="flex items-center gap-2 font-semibold">
              <CalendarDays className="h-4 w-4 text-primary" />
              <span>{mode === "gregorian" ? data.selectedMonth : localizedHijriHeader(data, t)}</span>
            </div>
            <div className="text-xs text-muted-foreground">
              {mode === "gregorian" ? localizedHijriHeader(data, t) : data.selectedMonth}
            </div>
          </div>
          <button
            onClick={() => navigate(1)}
            className="rounded-full p-2 hover:bg-muted"
            aria-label={t("nextMonth")}
          >
            <span dir="ltr"><ChevronRight className="h-5 w-5" /></span>
          </button>
        </div>

        <div className="grid grid-cols-4 gap-1 rounded-full bg-muted/60 p-1 text-xs font-medium">
          {(["year", "month", "week", "day"] as ViewMode[]).map((v) => (
            <button
              key={v}
              onClick={() => setView(v)}
              className={cn(
                "rounded-full px-2 py-1.5 transition",
                view === v ? "bg-card shadow ring-1 ring-primary/30 text-primary" : "text-muted-foreground hover:text-foreground"
              )}
            >
              {t("calView_" + v)}
            </button>
          ))}
        </div>

        <div className="flex items-center justify-between gap-2">
          <button
            onClick={async () => {
              const result = await executeCommand<Snapshot>("calendar.today");
              if (result.ok) setData(result.data);
              setSelectedIso(new Date().toISOString().slice(0, 10));
            }}
            className="rounded-full bg-primary px-4 py-1.5 text-xs font-medium text-primary-foreground"
          >
            {t("today")}
          </button>
          <div className="flex items-center gap-1 rounded-full bg-muted/60 p-1 text-xs">
            <button
              onClick={() => setMode("gregorian")}
              className={cn("rounded-full px-3 py-1", mode === "gregorian" ? "bg-card text-primary shadow" : "text-muted-foreground")}
            >
              {t("calMode_gregorian")}
            </button>
            <button
              onClick={() => setMode("hijri")}
              className={cn("rounded-full px-3 py-1", mode === "hijri" ? "bg-card text-primary shadow" : "text-muted-foreground")}
            >
              {t("calMode_hijri")}
            </button>
          </div>
        </div>
      </Card>

      {view === "year" && (
        <YearView data={data} mode={mode} onPickMonth={async (monthValue) => {
          const result = await executeCommand<Snapshot>("calendar.setMonth", { month: monthValue });
          if (result.ok) setData(result.data);
          setView("month");
        }} />
      )}
      {view === "month" && (
        <MonthView data={data} mode={mode} onSelect={(iso) => setSelectedIso(iso)} />
      )}
      {view === "week" && (
        <WeekView data={data} mode={mode} anchorIso={selectedIso ?? todayDay?.sourceDate ?? data.days[0]?.sourceDate} onSelect={(iso) => setSelectedIso(iso)} t={t} />
      )}
      {view === "day" && (
        <DayView day={selectedDay ?? todayDay ?? data.days[0]} mode={mode} t={t} />
      )}

      {selectedDay && view === "month" && (
        <DayBottomSheet day={selectedDay} mode={mode} onClose={() => setSelectedIso(null)} t={t} />
      )}
    </div>
  );
}

/* ---------- Views ---------- */

function MonthView({ data, mode, onSelect }: { data: Snapshot; mode: CalendarMode; onSelect: (iso: string) => void }) {
  // Pad start of grid with blanks so weekday alignment is right
  const firstDay = data.days[0];
  const startPad = firstDay ? firstDay.weekday : 0;
  const cells: (Day | null)[] = [
    ...Array(startPad).fill(null),
    ...data.days,
  ];
  while (cells.length % 7 !== 0) cells.push(null);

  const weekdayKeys = ["wdSun", "wdMon", "wdTue", "wdWed", "wdThu", "wdFri", "wdSat"];
  const t = useAppLabels();

  return (
    <Card className="p-2">
      <div className="grid grid-cols-7 gap-1 pb-1">
        {weekdayKeys.map((k) => (
          <div key={k} className="text-center text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">
            {t(k)}
          </div>
        ))}
      </div>
      <div className="grid grid-cols-7 gap-1">
        {cells.map((day, i) => {
          if (!day) return <div key={i} className="aspect-square" />;
          const primary = mode === "gregorian" ? day.dayNumber : day.hijriDay;
          const secondary = mode === "gregorian" ? day.hijriDay : day.dayNumber;
          const hasOccasion = !!day.occasionKey;
          const major = day.occasionImportance === "major";
          return (
            <button
              key={day.sourceDate}
              onClick={() => onSelect(day.sourceDate)}
              className={cn(
                "relative aspect-square rounded-lg border text-sm transition",
                "flex flex-col items-center justify-center",
                day.isToday
                  ? "border-primary bg-primary/10 ring-1 ring-primary/40"
                  : hasOccasion
                    ? "border-accent/40 bg-accent/5"
                    : "border-border/40 bg-card hover:bg-muted/60"
              )}
              aria-label={day.date}
            >
              <span className="absolute top-1 right-1 text-[9px] leading-none text-muted-foreground tabular-nums">
                {secondary}
              </span>
              <span className={cn("text-base font-semibold tabular-nums", day.isToday && "text-primary")}>
                {primary}
              </span>
              {hasOccasion && (
                <span
                  className={cn(
                    "absolute bottom-1 h-1 w-1 rounded-full",
                    major ? "bg-primary" : "bg-accent"
                  )}
                />
              )}
            </button>
          );
        })}
      </div>
    </Card>
  );
}

function WeekView({ data, mode, anchorIso, onSelect, t }: { data: Snapshot; mode: CalendarMode; anchorIso?: string; onSelect: (iso: string) => void; t: (k: string) => string }) {
  const anchor = anchorIso ? data.days.find((d) => d.sourceDate === anchorIso) ?? data.days[0] : data.days[0];
  if (!anchor) return null;
  const start = data.days.findIndex((d) => d.sourceDate === anchor.sourceDate) - anchor.weekday;
  const week = Array.from({ length: 7 }, (_, i) => data.days[start + i]).filter(Boolean) as Day[];

  return (
    <div className="flex flex-col gap-2">
      {week.map((d) => (
        <Card key={d.sourceDate} className={cn("p-3", d.isToday && "ring-2 ring-primary")}>
          <button onClick={() => onSelect(d.sourceDate)} className="w-full text-left">
            <div className="mb-2 flex items-center justify-between">
              <div>
                <div className="text-sm font-semibold">
                  {mode === "gregorian" ? d.date : `${d.hijriDay} ${localizedHijriMonth(d, t)} ${d.hijriYear}`}
                </div>
                <div className="text-xs text-muted-foreground">
                  {mode === "gregorian" ? d.hijri : d.date}
                </div>
              </div>
              <div className="flex items-center gap-2">
                {d.occasionKey && (
                  <span className="rounded-full bg-accent/20 px-2 py-0.5 text-[10px] font-medium text-accent-foreground">
                    <Star className="mr-1 inline h-3 w-3" />{t(d.occasionKey)}
                  </span>
                )}
                {d.isToday && (
                  <span className="rounded-full bg-primary px-2 py-0.5 text-[10px] font-bold text-primary-foreground">
                    {t("todayBadge")}
                  </span>
                )}
              </div>
            </div>
            <PrayerGrid day={d} t={t} />
          </button>
        </Card>
      ))}
    </div>
  );
}

function DayView({ day, mode, t }: { day: Day; mode: CalendarMode; t: (k: string) => string }) {
  if (!day) return null;
  return (
    <Card className="flex flex-col gap-4 p-4">
      <div className="flex items-baseline justify-between">
        <div>
          <div className="text-2xl font-bold">
            {mode === "gregorian" ? day.date : `${day.hijriDay} ${localizedHijriMonth(day, t)} ${day.hijriYear}`}
          </div>
          <div className="text-sm text-muted-foreground">
            {mode === "gregorian" ? day.hijri : day.date}
          </div>
        </div>
        {day.isToday && (
          <span className="rounded-full bg-primary px-3 py-1 text-xs font-bold text-primary-foreground">{t("todayBadge")}</span>
        )}
      </div>
      {day.occasionKey && (
        <div className="rounded-lg border border-accent/40 bg-accent/10 px-3 py-2 text-sm">
          <Star className="mr-2 inline h-4 w-4 text-accent" />
          <span className="font-medium">{t(day.occasionKey)}</span>
        </div>
      )}
      <PrayerGrid day={day} t={t} />
    </Card>
  );
}

function YearView({ data, mode, onPickMonth }: { data: Snapshot; mode: CalendarMode; onPickMonth: (monthValue: string) => void }) {
  // Render 12 mini-months around selected year.
  // We only have the currently-loaded month's days; use them for the current month,
  // and render skeletons for the rest with the ability to jump to any.
  const t = useAppLabels();
  const year = data.yearNumber;
  const months = useMemo(() => Array.from({ length: 12 }, (_, i) => i + 1), []);

  return (
    <div className="grid grid-cols-2 gap-2 md:grid-cols-3">
      {months.map((m) => {
        const isCurrent = m === data.monthNumber;
        const monthValue = `${year}-${String(m).padStart(2, "0")}`;
        const daysInMonth = new Date(year, m, 0).getDate();
        return (
          <button
            key={m}
            onClick={() => onPickMonth(monthValue)}
            className={cn(
              "rounded-xl border p-2 text-left transition",
              isCurrent ? "border-primary bg-primary/5 ring-1 ring-primary/40" : "border-border/40 bg-card hover:bg-muted/60"
            )}
          >
            <div className="mb-1 text-xs font-semibold">{t("month_" + m)}</div>
            <div className="grid grid-cols-7 gap-[2px]">
              {Array.from({ length: daysInMonth }, (_, i) => {
                const dayNum = i + 1;
                const iso = `${monthValue}-${String(dayNum).padStart(2, "0")}`;
                const day = isCurrent ? data.days.find((d) => d.sourceDate === iso) : undefined;
                const hasOccasion = !!day?.occasionKey;
                const isToday = !!day?.isToday;
                return (
                  <div
                    key={dayNum}
                    className={cn(
                      "aspect-square rounded-[3px] text-[8px] leading-none flex items-center justify-center tabular-nums",
                      isToday && "bg-primary text-primary-foreground font-bold",
                      !isToday && hasOccasion && "bg-accent/30 text-accent-foreground",
                      !isToday && !hasOccasion && "text-muted-foreground"
                    )}
                  >
                    {mode === "gregorian" ? dayNum : day?.hijriDay ?? dayNum}
                  </div>
                );
              })}
            </div>
          </button>
        );
      })}
    </div>
  );
}

/* ---------- Helpers ---------- */

function localizedHijriHeader(data: Snapshot, t: (k: string) => string) {
  const first = data.days[0];
  const last = data.days[data.days.length - 1];
  if (!first || !last) return data.hijriMonthLabel;
  const firstName = localizedHijriMonth(first, t);
  const lastName = localizedHijriMonth(last, t);
  return first.hijriMonth === last.hijriMonth
    ? `${firstName} ${first.hijriYear}`
    : `${firstName} – ${lastName} ${last.hijriYear}`;
}

function localizedHijriMonth(day: Day, t: (k: string) => string) {
  return day.hijriMonth >= 1 && day.hijriMonth <= 12
    ? t(`hijriMonth_${day.hijriMonth}`)
    : day.hijriMonthName;
}

function PrayerGrid({ day, t }: { day: Day; t: (k: string) => string }) {
  const rows: [string, string][] = [
    [t("prayer_Fajr"), day.fajr],
    [t("prayer_Sunrise"), day.sunrise],
    [t("prayer_Dhuhr"), day.dhuhr],
    [t("prayer_Asr"), day.asr],
    [t("prayer_Maghrib"), day.maghrib],
    [t("prayer_Isha"), day.isha],
  ];
  return (
    <div className="grid grid-cols-3 gap-1.5 text-xs">
      {rows.map(([label, time]) => (
        <div key={label} className="rounded-md bg-muted/60 px-2 py-1.5">
          <div className="text-[10px] uppercase text-muted-foreground">{label}</div>
          <div className="font-medium tabular-nums" dir="ltr">{time}</div>
        </div>
      ))}
    </div>
  );
}

function DayBottomSheet({ day, mode, onClose, t }: { day: Day; mode: CalendarMode; onClose: () => void; t: (k: string) => string }) {
  return (
    <div className="fixed inset-0 z-40 flex items-end justify-center bg-background/60 backdrop-blur-sm md:items-center" onClick={onClose}>
      <div
        className="w-full max-w-md rounded-t-2xl border border-border bg-card p-4 shadow-2xl md:rounded-2xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-3 flex items-start justify-between">
          <div>
            <div className="text-lg font-bold">
              {mode === "gregorian" ? day.date : `${day.hijriDay} ${localizedHijriMonth(day, t)} ${day.hijriYear}`}
            </div>
            <div className="text-sm text-muted-foreground">
              {mode === "gregorian" ? day.hijri : day.date}
            </div>
          </div>
          <button onClick={onClose} className="rounded-full p-1 hover:bg-muted" aria-label={t("close")}>
            <X className="h-5 w-5" />
          </button>
        </div>
        {day.occasionKey && (
          <div className="mb-3 rounded-lg border border-accent/40 bg-accent/10 px-3 py-2 text-sm">
            <Star className="mr-2 inline h-4 w-4 text-accent" />
            <span className="font-medium">{t(day.occasionKey)}</span>
          </div>
        )}
        <PrayerGrid day={day} t={t} />
      </div>
    </div>
  );
}
