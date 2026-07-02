import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";
import { Card } from "@/components/Card";
import { Field } from "@/components/Field";
import { Picker } from "@/components/Picker";
import { SettingsHeader } from "@/components/SettingsHeader";
import { Play, Square, X, Plus } from "lucide-react";
import { usePageLog } from "@/hooks/usePageLog";

export const Route = createFileRoute("/settings/adhan")({
  component: AdhanPage,
});

type Sound = { id: string; label: string; selected: boolean; isCustom: boolean };
type Reminder = { id: string; value: number; unit: string; direction: string };
type Adhan = {
  sounds: Sound[]; volume: number;
  calculationMethod: string; madhhab: string; highLatitudeRule: string;
  fajrAngle: number; ishaAngle: number; isCustomMethod: boolean;
  offsets: Record<string, number>;
  clockFormat: string;
  fasting: { iftarDelay: number; imsakAdvance: number };
  imsakReminders: Reminder[]; iftarReminders: Reminder[];
  perPrayerOverrides: { prayer: string; soundId: string; vibration: string }[];
};

function AdhanPage() {
  usePageLog("settings.adhan");
  const { data, refresh, setData } = useSnapshot<Adhan>("settings.getSnapshot", { section: "adhan" });
  const [playing, setPlaying] = useState<string | null>(null);
  if (!data) return null;
  const patch = (p: Partial<Adhan>) => {
    const next = { ...data, ...p };
    setData(next);
    return mauiCall("settings.patch", { adhan: next });
  };

  return (
    <div>
      <SettingsHeader title="Adhan" />
      <div className="flex flex-col gap-3">
        <Card>
          <div className="mb-2 text-sm font-semibold">Adhan sound</div>
          <ul className="space-y-1.5">
            {data.sounds.map((s) => (
              <li key={s.id} className="flex items-center gap-2">
                <button onClick={() => patch({ sounds: data.sounds.map((x) => ({ ...x, selected: x.id === s.id })) })}
                  className={`flex-1 rounded-md border px-3 py-2 text-left text-sm ${s.selected ? "border-primary bg-primary/10 font-semibold" : "border-border bg-card"}`}>
                  {s.label}
                </button>
                <button
                  onClick={() => { if (playing === s.id) { mauiCall("settings.invoke", { action: "stopPreview" }); setPlaying(null); } else { mauiCall("settings.invoke", { action: "previewSound", payload: { id: s.id } }); setPlaying(s.id); } }}
                  className="rounded-full bg-muted p-2"
                >
                  {playing === s.id ? <Square className="h-4 w-4" /> : <Play className="h-4 w-4" />}
                </button>
                {s.isCustom && (
                  <button onClick={() => mauiCall("settings.invoke", { action: "removeSound", payload: { id: s.id } }).then(refresh)} className="rounded-full bg-muted p-2">
                    <X className="h-4 w-4" />
                  </button>
                )}
              </li>
            ))}
          </ul>
          <button onClick={() => mauiCall("settings.invoke", { action: "addCustomSound" }).then(refresh)} className="mt-3 flex w-full items-center justify-center gap-1 rounded-md border border-dashed border-border py-2 text-sm">
            <Plus className="h-4 w-4" /> Add custom sound
          </button>
          <button onClick={() => mauiCall("settings.invoke", { action: "testNotification" })} className="mt-2 w-full rounded-md bg-secondary px-3 py-2 text-sm font-medium">
            Test notification
          </button>
          <Field label={`Volume (${data.volume}%)`} className="mt-3">
            <input type="range" min={0} max={100} value={data.volume} onChange={(e) => patch({ volume: Number(e.target.value) })} className="w-full accent-[var(--color-primary)]" />
          </Field>
        </Card>

        <Card className="space-y-3">
          <div className="text-sm font-semibold">Calculation</div>
          <Field label="Method"><Picker value={data.calculationMethod} onChange={(v) => patch({ calculationMethod: v, isCustomMethod: v === "Custom" })}>
            {["MuslimWorldLeague","Egyptian","Karachi","UmmAlQura","Dubai","Qatar","Kuwait","MoonsightingCommittee","NorthAmerica","Custom"].map((m) => <option key={m} value={m}>{m}</option>)}
          </Picker></Field>
          <Field label="Madhhab"><Picker value={data.madhhab} onChange={(v) => patch({ madhhab: v })}>
            {["Shafi", "Hanafi"].map((m) => <option key={m} value={m}>{m}</option>)}
          </Picker></Field>
          <Field label="High latitude rule"><Picker value={data.highLatitudeRule} onChange={(v) => patch({ highLatitudeRule: v })}>
            {["MiddleOfTheNight","SeventhOfTheNight","TwilightAngle"].map((m) => <option key={m} value={m}>{m}</option>)}
          </Picker></Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label="Fajr angle">
              <input type="number" value={data.fajrAngle} disabled={!data.isCustomMethod} onChange={(e) => patch({ fajrAngle: Number(e.target.value) })} className="rounded-lg border border-input bg-card px-3 py-2 text-sm disabled:opacity-50" />
            </Field>
            <Field label="Isha angle">
              <input type="number" value={data.ishaAngle} disabled={!data.isCustomMethod} onChange={(e) => patch({ ishaAngle: Number(e.target.value) })} className="rounded-lg border border-input bg-card px-3 py-2 text-sm disabled:opacity-50" />
            </Field>
          </div>
        </Card>

        <Card>
          <div className="mb-2 text-sm font-semibold">Offsets (minutes)</div>
          <div className="grid grid-cols-2 gap-2">
            {Object.entries(data.offsets).map(([k, v]) => (
              <Field key={k} label={k.charAt(0).toUpperCase() + k.slice(1)}>
                <input type="number" value={v} onChange={(e) => patch({ offsets: { ...data.offsets, [k]: Number(e.target.value) } })} className="rounded-lg border border-input bg-card px-3 py-2 text-sm" />
              </Field>
            ))}
          </div>
        </Card>

        <Card className="space-y-3">
          <Field label="Clock format"><Picker value={data.clockFormat} onChange={(v) => patch({ clockFormat: v })}>
            <option value="12h">12-hour</option><option value="24h">24-hour</option>
          </Picker></Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label="Iftar delay"><input type="number" value={data.fasting.iftarDelay} onChange={(e) => patch({ fasting: { ...data.fasting, iftarDelay: Number(e.target.value) } })} className="rounded-lg border border-input bg-card px-3 py-2 text-sm" /></Field>
            <Field label="Imsak advance"><input type="number" value={data.fasting.imsakAdvance} onChange={(e) => patch({ fasting: { ...data.fasting, imsakAdvance: Number(e.target.value) } })} className="rounded-lg border border-input bg-card px-3 py-2 text-sm" /></Field>
          </div>
        </Card>

        <Card>
          <div className="mb-2 text-sm font-semibold">Per-prayer adhan overrides</div>
          <ul className="space-y-2">
            {data.perPrayerOverrides.map((o) => (
              <li key={o.prayer} className="grid grid-cols-3 items-center gap-2 text-sm">
                <span className="font-medium">{o.prayer}</span>
                <Picker value={o.soundId} onChange={() => undefined}>
                  {data.sounds.map((s) => <option key={s.id} value={s.id}>{s.label}</option>)}
                </Picker>
                <Picker value={o.vibration} onChange={() => undefined}>
                  {["default","short","long","none"].map((v) => <option key={v} value={v}>{v}</option>)}
                </Picker>
              </li>
            ))}
          </ul>
        </Card>
      </div>
    </div>
  );
}
