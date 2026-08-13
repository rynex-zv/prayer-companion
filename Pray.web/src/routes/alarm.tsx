import { useEffect, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { AlarmClock, Minus, Plus } from "lucide-react";

import { PageLog } from "@/components/PageLog";
import { usePageLog } from "@/hooks/usePageLog";
import { useProjection } from "@/hooks/useProjection";
import { executeCommand } from "@/client/applicationClient";

export const Route = createFileRoute("/alarm")({
  head: () => ({ meta: [] }),
  component: AlarmPage,
});

type AlarmLabels = {
  title: string;
  snooze: string;
  stop: string;
  noActiveAlarm: string;
  delayTemplate: string;
};

type AlarmSnapshot = {
  isActive: boolean;
  prayerClock: string;
  delayFromBase: string;
  prayerName: string;
  reminderText: string;
  canSnooze: boolean;
  minDelayMinutes: number;
  maxDelayMinutes: number;
  selectedDelayMinutes: number;
  labels: AlarmLabels;
};

function AlarmPage() {
  usePageLog("alarm");
  const { data, refresh, setData } = useProjection<AlarmSnapshot>("alarm.getSnapshot");
  const [delayMinutes, setDelayMinutes] = useState(0);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (data) {
      setDelayMinutes(data.selectedDelayMinutes);
    }
  }, [data?.selectedDelayMinutes]);

  useEffect(() => {
    let cancelled = false;
    let timer = 0;
    const intervalMs = data?.isActive ? 1000 : 10_000;
    const poll = async () => {
      await refresh(true);
      if (!cancelled) {
        timer = window.setTimeout(poll, intervalMs);
      }
    };
    timer = window.setTimeout(poll, intervalMs);
    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [data?.isActive, refresh]);

  const run = async (method: "alarm.snooze" | "alarm.stop", payload?: unknown) => {
    if (submitting) return;
    setSubmitting(true);
    try {
      const result = await executeCommand<AlarmSnapshot>(method, payload);
      if (result.ok) setData(result.data);
    } finally {
      setSubmitting(false);
    }
  };

  if (!data) {
    return (
      <div className="flex min-h-[70vh] flex-col items-center justify-center">
        <AlarmClock className="h-20 w-20 text-primary" aria-hidden="true" />
      </div>
    );
  }

  if (!data.isActive) {
    return (
      <div className="flex min-h-[70vh] flex-col items-center justify-center gap-5 text-center" data-selector-name="alarm:inactive">
        <div className="flex w-full items-center justify-end"><PageLog page="alarm" /></div>
        <AlarmClock className="h-20 w-20 text-primary" aria-hidden="true" />
        <div>
          <p className="text-sm font-medium text-muted-foreground">{data.labels.title}</p>
          <h1 className="mt-2 text-2xl font-bold">{data.labels.noActiveAlarm}</h1>
        </div>
      </div>
    );
  }

  const delayLabel = data.labels.delayTemplate.replace("{0}", String(delayMinutes));
  const canDecrease = data.canSnooze && delayMinutes > data.minDelayMinutes && !submitting;
  const canIncrease = data.canSnooze && delayMinutes < data.maxDelayMinutes && !submitting;

  return (
    <div className="flex min-h-[70vh] flex-col items-center justify-center gap-6 text-center">
      <div className="flex w-full items-center justify-end"><PageLog page="alarm" /></div>
      <AlarmClock className="h-20 w-20 text-primary" aria-hidden="true" />
      <div>
        <p className="text-sm font-medium text-muted-foreground">{data.labels.title}</p>
        <h1 className="mt-2 text-3xl font-bold">{data.prayerName}</h1>
      </div>
      <div className="text-6xl font-bold tabular-nums text-primary" dir="ltr">{data.prayerClock}</div>
      {data.delayFromBase ? <p className="text-lg font-semibold">{data.delayFromBase}</p> : null}
      {data.reminderText ? <p className="max-w-sm text-sm text-muted-foreground">{data.reminderText}</p> : null}

      {data.canSnooze ? (
        <div className="flex items-center gap-4">
          <button
            type="button"
            data-selector-name="alarm.delay.decrease"
            aria-label={data.labels.snooze}
            disabled={!canDecrease}
            onClick={() => setDelayMinutes((value) => Math.max(data.minDelayMinutes, value - 1))}
            className="grid h-12 w-12 place-items-center rounded-full bg-secondary text-secondary-foreground disabled:opacity-40"
          >
            <Minus className="h-5 w-5" />
          </button>
          <span className="min-w-40 text-base font-semibold tabular-nums">{delayLabel}</span>
          <button
            type="button"
            data-selector-name="alarm.delay.increase"
            aria-label={data.labels.snooze}
            disabled={!canIncrease}
            onClick={() => setDelayMinutes((value) => Math.min(data.maxDelayMinutes, value + 1))}
            className="grid h-12 w-12 place-items-center rounded-full bg-secondary text-secondary-foreground disabled:opacity-40"
          >
            <Plus className="h-5 w-5" />
          </button>
        </div>
      ) : null}

      <div className="flex w-full max-w-sm flex-col gap-3">
        {data.canSnooze ? (
          <button
            type="button"
            data-selector-name="alarm.snooze"
            disabled={submitting}
            onClick={() => void run("alarm.snooze", { minutes: delayMinutes })}
            className="rounded-full bg-secondary px-6 py-4 text-lg font-semibold text-secondary-foreground shadow-md disabled:opacity-50"
          >
            {data.labels.snooze}
          </button>
        ) : null}
        <button
          type="button"
          data-selector-name="alarm.stop"
          disabled={submitting}
          onClick={() => void run("alarm.stop")}
          className="rounded-full bg-destructive px-6 py-4 text-lg font-semibold text-destructive-foreground shadow-md disabled:opacity-50"
        >
          {data.labels.stop}
        </button>
      </div>
    </div>
  );
}
