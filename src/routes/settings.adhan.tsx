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
import { useAppLabels } from "@/hooks/useAppLabels";

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
  const t = useAppLabels();
  const { data, refresh, setData } = useSnapshot<Adhan>("settings.getSnapshot", { section: "adhan" });
  const [playing, setPlaying] = useState<string | null>(null);
  if (!data) return null;
  const patch = (p: Partial<Adhan>) => {
    const next = { ...data, ...p };
    setData(next);
    return mauiCall("settings.patch", { adhan: next });
  };
  const patchReminder = (key: "imsakReminders" | "iftarReminders", next: Reminder[]) => {
    return patch({ [key]: next } as Partial<Adhan>);
  };
  const patchOverride = (prayer: string, p: Partial<Adhan["perPrayerOverrides"][number]>) => {
    return patch({
      perPrayerOverrides: data.perPrayerOverrides.map((item) =>
        item.prayer === prayer ? { ...item, ...p } : item,
      ),
    });
  };

  return (
    <div>
      <SettingsHeader title={t("adhan", "Adhan")} />
      <div className="flex flex-col gap-3">
        <Card>
          <div className="mb-2 text-sm font-semibold">{t("adhanSound", "Adhan sound")}</div>
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
            <Plus className="h-4 w-4" /> {t("addCustomSound", "Add custom sound")}
          </button>
          <button onClick={() => mauiCall("settings.invoke", { action: "testNotification" })} className="mt-2 w-full rounded-md bg-secondary px-3 py-2 text-sm font-medium">
            {t("testNotification", "Test notification")}
          </button>
          <Field label={`${t("volume", "Volume")} (${data.volume}%)`} className="mt-3">
            <input type="range" min={0} max={100} value={data.volume} onChange={(e) => patch({ volume: Number(e.target.value) })} className="w-full accent-[var(--color-primary)]" />
          </Field>
        </Card>

        <Card className="space-y-3">
          <div className="text-sm font-semibold">{t("calculation", "Calculation")}</div>
          <Field label={t("method", "Method")}><Picker value={data.calculationMethod} onChange={(v) => patch({ calculationMethod: v, isCustomMethod: v === "Custom" })}>
            {["MuslimWorldLeague","Egyptian","Karachi","UmmAlQura","Dubai","Qatar","Kuwait","MoonsightingCommittee","NorthAmerica","Custom"].map((m) => <option key={m} value={m}>{t(`method_${m}`, m)}</option>)}
          </Picker></Field>
          <Field label={t("madhhab", "Madhhab")}><Picker value={data.madhhab} onChange={(v) => patch({ madhhab: v })}>
            {["Shafi", "Hanafi"].map((m) => <option key={m} value={m}>{t(`madhhab_${m}`, m)}</option>)}
          </Picker></Field>
          <Field label={t("highLatitudeRule", "High latitude rule")}><Picker value={data.highLatitudeRule} onChange={(v) => patch({ highLatitudeRule: v })}>
            {["MiddleOfTheNight","SeventhOfTheNight","TwilightAngle"].map((m) => <option key={m} value={m}>{t(`highLatitude_${m}`, m)}</option>)}
          </Picker></Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label={t("fajrAngle", "Fajr angle")}>
              <input type="number" value={data.fajrAngle} disabled={!data.isCustomMethod} onChange={(e) => patch({ fajrAngle: Number(e.target.value) })} className="rounded-lg border border-input bg-card px-3 py-2 text-sm disabled:opacity-50" />
            </Field>
            <Field label={t("ishaAngle", "Isha angle")}>
              <input type="number" value={data.ishaAngle} disabled={!data.isCustomMethod} onChange={(e) => patch({ ishaAngle: Number(e.target.value) })} className="rounded-lg border border-input bg-card px-3 py-2 text-sm disabled:opacity-50" />
            </Field>
          </div>
        </Card>

        <Card>
          <div className="mb-2 text-sm font-semibold">{t("offsetsMinutes", "Offsets (minutes)")}</div>
          <div className="grid grid-cols-2 gap-2">
            {Object.entries(data.offsets).map(([k, v]) => (
              <Field key={k} label={k.charAt(0).toUpperCase() + k.slice(1)}>
                <input type="number" value={v} onChange={(e) => patch({ offsets: { ...data.offsets, [k]: Number(e.target.value) } })} className="rounded-lg border border-input bg-card px-3 py-2 text-sm" />
              </Field>
            ))}
          </div>
        </Card>

        <Card className="space-y-3">
          <Field label={t("clockFormat", "Clock format")}><Picker value={data.clockFormat} onChange={(v) => patch({ clockFormat: v })}>
            <option value="12h">{t("clock12h", "12-hour")}</option><option value="24h">{t("clock24h", "24-hour")}</option>
          </Picker></Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label={t("iftarDelay", "Iftar delay")}><input type="number" value={data.fasting.iftarDelay} onChange={(e) => patch({ fasting: { ...data.fasting, iftarDelay: Number(e.target.value) } })} className="rounded-lg border border-input bg-card px-3 py-2 text-sm" /></Field>
            <Field label={t("imsakAdvance", "Imsak advance")}><input type="number" value={data.fasting.imsakAdvance} onChange={(e) => patch({ fasting: { ...data.fasting, imsakAdvance: Number(e.target.value) } })} className="rounded-lg border border-input bg-card px-3 py-2 text-sm" /></Field>
          </div>
        </Card>

        <Card className="space-y-3">
          <div className="text-sm font-semibold">{t("fastingReminders", "Fasting reminders")}</div>
          {([
            ["imsakReminders", t("imsakReminders", "Imsak reminders")],
            ["iftarReminders", t("iftarReminders", "Iftar reminders")],
          ] as const).map(([key, title]) => (
            <div key={key} className="space-y-2">
              <div className="text-xs font-medium text-muted-foreground">{title}</div>
              <button
                onClick={() => patchReminder(key, [
                  ...data[key],
                  { id: String(Date.now()), value: 10, unit: "min", direction: "before" },
                ])}
                className="flex w-full items-center justify-center gap-1 rounded-md border border-dashed border-border py-2 text-sm"
              >
                <Plus className="h-4 w-4" /> {t("addReminder", "Add reminder")}
              </button>
              <ul className="space-y-2">
                {data[key].map((r) => (
                  <li key={r.id} className="grid grid-cols-[1fr_auto] gap-2">
                    <div className="grid grid-cols-3 gap-2">
                      <input
                        type="number"
                        value={r.value}
                        onChange={(e) => patchReminder(key, data[key].map((item) => item.id === r.id ? { ...item, value: Number(e.target.value) } : item))}
                        className="rounded-lg border border-input bg-card px-2 py-1.5 text-sm"
                      />
                      <Picker value={r.unit} onChange={(unit) => patchReminder(key, data[key].map((item) => item.id === r.id ? { ...item, unit } : item))}>
                        <option value="min">{t("minutes", "min")}</option>
                        <option value="hour">{t("hours", "hour")}</option>
                      </Picker>
                      <Picker value={r.direction} onChange={(direction) => patchReminder(key, data[key].map((item) => item.id === r.id ? { ...item, direction } : item))}>
                        <option value="before">{t("before", "before")}</option>
                        <option value="after">{t("after", "after")}</option>
                      </Picker>
                    </div>
                    <button
                      onClick={() => patchReminder(key, data[key].filter((item) => item.id !== r.id))}
                      className="rounded-full bg-muted p-2"
                    >
                      <X className="h-4 w-4" />
                    </button>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </Card>

        <Card>
          <div className="mb-2 text-sm font-semibold">{t("perPrayerAdhan", "Per-prayer adhan overrides")}</div>
          <ul className="space-y-2">
            {data.perPrayerOverrides.map((o) => (
              <li key={o.prayer} className="grid grid-cols-3 items-center gap-2 text-sm">
                <span className="font-medium">{o.prayer}</span>
                <Picker value={o.soundId} onChange={(soundId) => patchOverride(o.prayer, { soundId })}>
                  {data.sounds.map((s) => <option key={s.id} value={s.id}>{s.label}</option>)}
                </Picker>
                <Picker value={o.vibration} onChange={(vibration) => patchOverride(o.prayer, { vibration })}>
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
