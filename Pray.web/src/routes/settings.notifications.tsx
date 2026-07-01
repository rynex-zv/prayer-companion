import { createFileRoute } from "@tanstack/react-router";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";
import { Card } from "@/components/Card";
import { Field } from "@/components/Field";
import { Picker } from "@/components/Picker";
import { Toggle } from "@/components/Toggle";
import { SettingsHeader } from "@/components/SettingsHeader";
import { usePageLog } from "@/hooks/usePageLog";

export const Route = createFileRoute("/settings/notifications")({
  component: NotificationsPage,
});

type N = {
  enableAdhan: boolean; mobilePrimaryAdhanType: string;
  hideOnCloseWindows: boolean; runBackgroundServiceWindows: boolean;
  vibration: boolean; vibrationStrength: string; vibrationPattern: string;
  minutesBefore: number; reminders: unknown[];
};

function NotificationsPage() {
  usePageLog("settings.notifications");
  const { data, refresh } = useSnapshot<N>("settings.getSnapshot", { section: "notifications" });
  if (!data) return null;
  const patch = (p: Partial<N>) => mauiCall("settings.patch", { notifications: { ...data, ...p } }).then(refresh);

  return (
    <div>
      <SettingsHeader title="Notifications" />
      <div className="flex flex-col gap-3">
        <Card className="space-y-3">
          <div className="flex items-center justify-between text-sm font-medium">
            Enable adhan <Toggle checked={data.enableAdhan} onChange={(v) => patch({ enableAdhan: v })} />
          </div>
          <Field label="Mobile primary adhan type">
            <Picker value={data.mobilePrimaryAdhanType} onChange={(v) => patch({ mobilePrimaryAdhanType: v })}>
              {["Full", "Notification", "Silent"].map((m) => <option key={m} value={m}>{m}</option>)}
            </Picker>
          </Field>
          <div className="flex items-center justify-between text-sm font-medium">
            Hide on close (Windows) <Toggle checked={data.hideOnCloseWindows} onChange={(v) => patch({ hideOnCloseWindows: v })} />
          </div>
          <div className="flex items-center justify-between text-sm font-medium">
            Run background service (Windows) <Toggle checked={data.runBackgroundServiceWindows} onChange={(v) => patch({ runBackgroundServiceWindows: v })} />
          </div>
        </Card>

        <Card className="space-y-3">
          <button onClick={() => mauiCall("settings.invoke", { action: "testNotification" })} className="w-full rounded-md bg-secondary px-3 py-2 text-sm font-medium">Test notification</button>
          <button onClick={() => mauiCall("settings.invoke", { action: "testAlarm" })} className="w-full rounded-md bg-secondary px-3 py-2 text-sm font-medium">Test alarm</button>
        </Card>

        <Card className="space-y-3">
          <div className="flex items-center justify-between text-sm font-medium">
            Vibration <Toggle checked={data.vibration} onChange={(v) => patch({ vibration: v })} />
          </div>
          <Field label="Vibration strength">
            <Picker value={data.vibrationStrength} onChange={(v) => patch({ vibrationStrength: v })}>
              {["Light", "Medium", "Strong"].map((m) => <option key={m} value={m}>{m}</option>)}
            </Picker>
          </Field>
          <Field label="Vibration pattern">
            <Picker value={data.vibrationPattern} onChange={(v) => patch({ vibrationPattern: v })}>
              {["Default", "Pulse", "Heartbeat"].map((m) => <option key={m} value={m}>{m}</option>)}
            </Picker>
          </Field>
          <Field label="Minutes before">
            <input type="number" value={data.minutesBefore} onChange={(e) => patch({ minutesBefore: Number(e.target.value) })} className="rounded-lg border border-input bg-card px-3 py-2 text-sm" />
          </Field>
        </Card>
      </div>
    </div>
  );
}
