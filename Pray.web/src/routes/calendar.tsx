import { createFileRoute } from "@tanstack/react-router";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";
import { Card } from "@/components/Card";
import {
    ChevronLeft,
    ChevronRight,
    CalendarDays,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { PageLog } from "@/components/PageLog";
import { usePageLog } from "@/hooks/usePageLog";
import { useAppLabels } from "@/hooks/useAppLabels";

type Day = {
    date: string;
    hijri: string;
    fajr: string;
    sunrise: string;
    dhuhr: string;
    asr: string;
    maghrib: string;
    isha: string;
    isToday?: boolean;
};

type Snapshot = {
    selectedMonth: string;
    selectedMonthValue: string;
    statusMessage: string;
    days: Day[];
};

export const Route = createFileRoute("/calendar")({
    head: () => ({ meta: [] }),
    component: CalendarPage,
});

function CalendarPage() {
    usePageLog("calendar");

    const t = useAppLabels();
    const { data, refresh } =
        useSnapshot<Snapshot>("calendar.getSnapshot");

    if (!data) {
        return (
            <div className="h-40 animate-pulse rounded-xl bg-muted" />
        );
    }

    const todayIndex = data.days.findIndex((day) => day.isToday);
    const days = todayIndex > 0
        ? [...data.days.slice(todayIndex), ...data.days.slice(0, todayIndex)]
        : data.days;

    const prayerRows = (day: Day) => [
        [t("prayer_Fajr"), day.fajr],
        [t("prayer_Sunrise"), day.sunrise],
        [t("prayer_Dhuhr"), day.dhuhr],
        [t("prayer_Asr"), day.asr],
        [t("prayer_Maghrib"), day.maghrib],
        [t("prayer_Isha"), day.isha],
    ];

    return (
        <div className="flex h-full min-h-0 flex-col gap-2">
            <div className="flex shrink-0 items-center justify-between gap-2">
                <h1 className="text-xl font-bold">{t("calendar")}</h1>
                <PageLog page="calendar" />
            </div>

            <Card className="flex shrink-0 items-center justify-between p-2">
                <button
                    type="button"
                    onClick={() =>
                        mauiCall("calendar.previousMonth").then(refresh)
                    }
                    className="rounded-full p-2 hover:bg-muted"
                    aria-label={t("previousMonth")}
                >
                    <ChevronLeft className="h-5 w-5 rtl:rotate-180" />
                </button>

                <label className="flex items-center gap-2 font-semibold">
                    <CalendarDays className="h-4 w-4 text-primary" />

                    <input
                        type="month"
                        value={data.selectedMonthValue}
                        onChange={(event) => {
                            const month = event.currentTarget.value;

                            if (!month) return;

                            void mauiCall("calendar.setMonth", { month }).then(
                                refresh,
                            );
                        }}
                        data-selector-name="calendar:month"
                        className="w-32 rounded-md border border-input bg-card px-2 py-1 text-center text-sm"
                        dir="ltr"
                    />

                    <span>{data.selectedMonth}</span>
                </label>

                <button
                    type="button"
                    onClick={() =>
                        mauiCall("calendar.nextMonth").then(refresh)
                    }
                    className="rounded-full p-2 hover:bg-muted"
                    aria-label={t("nextMonth")}
                >
                    <ChevronRight className="h-5 w-5 rtl:rotate-180" />
                </button>
            </Card>

            <button
                type="button"
                onClick={() => mauiCall("calendar.today").then(refresh)}
                className="shrink-0 rounded-full bg-primary px-4 py-1.5 text-xs font-medium text-primary-foreground"
            >
                {t("today")}
            </button>

            {data.statusMessage ? (
                <p className="text-center text-xs text-muted-foreground">
                    {data.statusMessage}
                </p>
            ) : null}

            <div
                className="min-h-0 flex-1 space-y-1.5 overflow-y-auto pb-2"
                data-selector-name="calendar:days"
            >
                {days.map((day) => (
                    <Card
                        key={day.date}
                        className={cn(
                            "p-2",
                            day.isToday && "ring-2 ring-primary",
                        )}
                    >
                        <div className="mb-1 flex items-center justify-between gap-2">
                            <div>
                                <div className="text-xs font-semibold">
                                    {day.date}
                                </div>

                                <div className="text-[10px] text-muted-foreground">
                                    {day.hijri}
                                </div>
                            </div>

                            {day.isToday ? (
                                <span className="rounded-full bg-primary px-2 py-0.5 text-[10px] font-bold text-primary-foreground">
                                    {t("todayBadge")}
                                </span>
                            ) : null}
                        </div>

                        <div className="grid grid-cols-6 gap-1 text-[10px]">
                            {prayerRows(day).map(([name, time]) => (
                                <div
                                    key={name}
                                    className="min-w-0 rounded bg-muted/60 px-1 py-1 text-center"
                                >
                                    <div className="truncate text-[9px] text-muted-foreground">
                                        {name}
                                    </div>

                                    <div
                                        className="font-medium tabular-nums"
                                        dir="ltr"
                                    >
                                        {time}
                                    </div>
                                </div>
                            ))}
                        </div>
                    </Card>
                ))}
            </div>
        </div>
    );
}
