import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";
import { Card } from "@/components/Card";
import { Toggle } from "@/components/Toggle";
import { SettingsHeader } from "@/components/SettingsHeader";
import { Plus, X } from "lucide-react";

export const Route = createFileRoute("/settings/alarms")({
  component: AlarmsPage,
});

type Reminder = { id: string; text: string; enabled: boolean };
type A = { builtIn: Reminder[]; userRemindersEnabled: boolean; userReminders: Reminder[] };

function AlarmsPage() {
  const { data, refresh } = useSnapshot<A>("settings.getSnapshot", { section: "alarmReminders" });
  const [draft, setDraft] = useState("");
  if (!data) return null;
  const patch = (p: Partial<A>) => mauiCall("settings.patch", { alarmReminders: { ...data, ...p } }).then(refresh);

  return (
    <div>
      <SettingsHeader title="Alarm Reminders" />
      <div className="flex flex-col gap-3">
        <Card>
          <div className="mb-2 text-sm font-semibold">Built-in</div>
          <ul className="space-y-2">
            {data.builtIn.map((r) => (
              <li key={r.id} className="flex items-center justify-between gap-2">
                <span className="text-sm">{r.text}</span>
                <Toggle checked={r.enabled} onChange={(v) => patch({ builtIn: data.builtIn.map((x) => x.id === r.id ? { ...x, enabled: v } : x) })} />
              </li>
            ))}
          </ul>
        </Card>

        <Card>
          <div className="mb-2 flex items-center justify-between text-sm font-semibold">
            Your reminders
            <Toggle checked={data.userRemindersEnabled} onChange={(v) => patch({ userRemindersEnabled: v })} />
          </div>
          <div className="flex gap-2">
            <input value={draft} onChange={(e) => setDraft(e.target.value)} placeholder="New reminder…" className="flex-1 rounded-lg border border-input bg-card px-3 py-2 text-sm" />
            <button
              onClick={() => { if (!draft.trim()) return; patch({ userReminders: [...data.userReminders, { id: String(Date.now()), text: draft.trim(), enabled: true }] }); setDraft(""); }}
              className="rounded-md bg-primary px-3 text-primary-foreground"
            ><Plus className="h-4 w-4" /></button>
          </div>
          <ul className="mt-3 space-y-2">
            {data.userReminders.map((r) => (
              <li key={r.id} className="flex items-center gap-2">
                <input
                  defaultValue={r.text}
                  onBlur={(e) => patch({ userReminders: data.userReminders.map((x) => x.id === r.id ? { ...x, text: e.target.value } : x) })}
                  className="flex-1 rounded-lg border border-input bg-card px-2 py-1.5 text-sm"
                />
                <Toggle checked={r.enabled} onChange={(v) => patch({ userReminders: data.userReminders.map((x) => x.id === r.id ? { ...x, enabled: v } : x) })} />
                <button onClick={() => patch({ userReminders: data.userReminders.filter((x) => x.id !== r.id) })} className="rounded-full p-1 text-muted-foreground hover:bg-muted">
                  <X className="h-4 w-4" />
                </button>
              </li>
            ))}
          </ul>
        </Card>
      </div>
    </div>
  );
}
