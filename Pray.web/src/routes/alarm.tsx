import { createFileRoute } from "@tanstack/react-router";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";
import { useAppLabels } from "@/hooks/useAppLabels";
import { PageLog } from "@/components/PageLog";
import { usePageLog } from "@/hooks/usePageLog";
import { AlarmClock } from "lucide-react";

export const Route = createFileRoute("/alarm")({
  head: () => ({ meta: [] }),
  component: AlarmPage,
});

type AlarmSnapshot = {
  prayerName: string;
  timeText: string;
  soundLabel?: string;
  canSnooze: boolean;
  snoozeMinutes: number;
  isActive: boolean;
  labels?: Record<string, string>;
};

// NOTE: Core must expose `alarm.getSnapshot`, `alarm.snooze`, `alarm.stop`
// (via PrayAdFree.Core/WebCoreRpcDispatcher + WebCatalog). Until then, this
// route renders a friendly placeholder on web; the native shell will host
// this same route once the RPC methods are wired.
function AlarmPage() {
  usePageLog("alarm");
  const t = useAppLabels();
  const { data, error, refresh } = useSnapshot<AlarmSnapshot>("alarm.getSnapshot");

  const L = data?.labels ?? {};
  const snoozeLabel = L.snooze ?? t("snooze");
  const stopLabel = L.stop ?? t("stop");
  const noAlarm = L.noActiveAlarm ?? t("noActiveAlarm");

  return (
    <div className="flex min-h-[70vh] flex-col items-center justify-center gap-6 text-center">
      <div className="flex w-full items-center justify-end">
        <PageLog page="alarm" />
      </div>

      <AlarmClock className="h-24 w-24 text-primary animate-pulse" />

      {data ? (
        <>
          <div className="text-3xl font-bold">{data.prayerName}</div>
          <div className="text-6xl font-bold tabular-nums text-primary" dir="ltr">
            {data.timeText}
          </div>
          {data.soundLabel ? (
            <div className="text-sm text-muted-foreground">{data.soundLabel}</div>
          ) : null}

          <div className="flex w-full max-w-sm flex-col gap-3">
            {data.canSnooze ? (
              <button
                data-selector-name="alarm.snooze"
                onClick={() =>
                  mauiCall("alarm.snooze", { minutes: data.snoozeMinutes }).then(() => refresh())
                }
                className="rounded-full bg-secondary px-6 py-4 text-lg font-semibold text-secondary-foreground shadow-md active:scale-[0.98]"
              >
                {snoozeLabel}
                {data.snoozeMinutes ? ` · ${data.snoozeMinutes}m` : ""}
              </button>
            ) : null}
            <button
              data-selector-name="alarm.stop"
              onClick={() => mauiCall("alarm.stop").then(() => refresh())}
              className="rounded-full bg-destructive px-6 py-4 text-lg font-semibold text-destructive-foreground shadow-md active:scale-[0.98]"
            >
              {stopLabel}
            </button>
          </div>
        </>
      ) : (
        <div className="text-sm text-muted-foreground">
          {error ? noAlarm : "…"}
        </div>
      )}
    </div>
  );
}
