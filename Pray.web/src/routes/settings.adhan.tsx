import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { SettingsHeader } from "@/components/SettingsHeader";
import { EditableSetting, OptionButtons, SectionBlock, StatusLine } from "@/components/SettingsFormControls";
import { Picker } from "@/components/Picker";
import { useAppLabels } from "@/hooks/useAppLabels";
import { useStoredSnapshot } from "@/hooks/useStoredSnapshot";
import { mauiCall } from "@/client/legacyClient";
import { syncField } from "@/state/appStore";

export const Route = createFileRoute("/settings/adhan")({
  component: AdhanPage,
});

type AdhanSettings = {
  sounds: { id: string; label: string; selected: boolean; isCustom: boolean; canPreview?: boolean }[];
  volume: number;
  calculationMethod: string;
  calculationMethods?: Option[];
  madhhab: string;
  madhhabs?: Option[];
  highLatitudeRule: string;
  highLatitudeRules?: Option[];
  fajrAngle: number;
  ishaAngle: number;
  isCustomMethod: boolean;
  offsets: Record<string, number>;
  clockFormat: string;
  clockFormats?: Option[];
  fasting: { iftarDelay: number; imsakAdvance: number };
  imsakReminders: Reminder[];
  iftarReminders: Reminder[];
  reminderUnits?: Option[];
  reminderDirections?: Option[];
  vibrationOverrideOptions?: Option[];
  perPrayerOverrides: { prayer: string; label?: string; soundId: string; vibration: string }[];
};

type Option = { id: string; label: string };
type Reminder = { id?: string; value: number; unit: string; direction: string; label?: string };

const fallbackMethods = ["Auto", "Jafari", "Karachi", "Isna", "MuslimWorldLeague", "UmmAlQura", "Egypt", "Tehran", "Gulf", "Kuwait", "Qatar", "Singapore", "France", "Turkey", "Russia", "Moonsighting", "Dubai", "Jakim", "Tunisia", "Algeria", "Kemenag", "Morocco", "Portugal", "Jordan", "Custom"];
const fallbackMadhhabs = ["Shafi", "Maliki", "Hanbali", "Hanafi"];
const fallbackHighLatitudeRules = ["MiddleOfTheNight", "SeventhOfTheNight", "TwilightAngle"];
const prayers = ["fajr", "sunrise", "dhuhr", "asr", "maghrib", "isha", "imsak"];

function AdhanPage() {
  const t = useAppLabels();
  const { data, setData, refresh } = useStoredSnapshot<AdhanSettings>("settings.getSnapshot", { section: "adhan" }, "settings.adhan");
  const [status, setStatus] = useState("ready");
  useEffect(() => {
    if (!data) return;

    const hasLegacySoundIds = data.sounds.some((sound) => sound.id === "builtin_1" || sound.id === "builtin_2");
    const missingBackendCatalogs = !data.calculationMethods?.length || !data.madhhabs?.length || !data.clockFormats?.length;
    if (hasLegacySoundIds || data.sounds.length < 10 || missingBackendCatalogs) {
      void refresh(true);
    }
  }, [data, refresh]);

  if (!data) return null;

  const patch = (next: AdhanSettings) => {
    setData(next);
    setStatus("saving");
    void syncField("adhan", "value", next).then((ok) => setStatus(ok ? "saved" : "error"));
  };

  const selectedSound = data.sounds.find((sound) => sound.selected)?.id ?? data.sounds[0]?.id ?? "";
  const calculationMethods = data.calculationMethods?.length ? data.calculationMethods : fallbackMethods.map((id) => ({ id, label: t(`method_${id}`) }));
  const madhhabs = data.madhhabs?.length ? data.madhhabs : fallbackMadhhabs.map((id) => ({ id, label: t(`madhhab_${id}`) }));
  const highLatitudeRules = data.highLatitudeRules?.length ? data.highLatitudeRules : fallbackHighLatitudeRules.map((id) => ({ id, label: t(`highLatitude_${id}`) }));
  const clockFormats = data.clockFormats?.length ? data.clockFormats : [
    { id: "auto", label: t("auto") },
    { id: "12h", label: t("clock12h") },
    { id: "24h", label: t("clock24h") },
  ];

  return (
    <div data-selector-name="adhan:page" className="flex flex-col gap-3">
      <SettingsHeader title={t("adhan")} />
      <StatusLine selectorName="adhan:status" value={t(`status_${status}`)} />

      <SectionBlock title={t("adhanSound")}>
        <button
          type="button"
          onClick={() => void mauiCall("settings.invoke", { action: "addCustomAdhanSound" })}
          data-selector-name="adhan:add-custom-sound"
          className="rounded-md border border-border bg-card px-3 py-2 text-sm font-medium text-card-foreground"
        >
          {t("addCustomSound")}
			  </button>
        <div className="space-y-2">
          {data.sounds.map((sound) => (
            <div key={sound.id} className="grid grid-cols-[1fr_auto_auto_auto] items-center gap-2 rounded-md border border-border bg-background p-2 text-sm">
              <span>{sound.label}</span>
              <button type="button" onClick={() => patch({ ...data, sounds: data.sounds.map((item) => ({ ...item, selected: item.id === sound.id })) })} className="rounded-md border border-border px-2 py-1 text-xs" data-selector-name={`adhan:sound-select:${sound.id}`}>
                {sound.selected ? t("selected") : t("select")}
              </button>
              <button type="button" disabled={sound.canPreview === false} onClick={() => void mauiCall("settings.invoke", { action: "previewSound", payload: { id: sound.id } })} className="rounded-md border border-border px-2 py-1 text-xs disabled:opacity-40" data-selector-name={`adhan:sound-play:${sound.id}`}>
                {t("play")}
              </button>
              {sound.isCustom ? (
                <button type="button" onClick={() => void mauiCall("settings.invoke", { action: "removeCustomAdhanSound", payload: { id: sound.id } })} className="rounded-md border border-border px-2 py-1 text-xs" data-selector-name={`adhan:sound-remove:${sound.id}`}>
                  {t("remove")}
                </button>
              ) : <span />}
            </div>
          ))}
        </div>
        <div className="text-sm text-card-foreground">
          <div className="mb-1 flex items-center justify-between text-xs text-muted-foreground">
            <span>{t("volume")}</span>
            <span data-selector-name="adhan:volume-label">{data.volume}%</span>
          </div>
          <input
            type="range"
            min="0"
            max="100"
            value={data.volume}
            onChange={(event) => patch({ ...data, volume: Number(event.currentTarget.value) || 0 })}
            data-selector-name="adhan:volume"
            className="w-full accent-primary"
          />
        </div>
      </SectionBlock>

      <SectionBlock title={t("calculation")}>
        <SelectSetting
          label={t("method")}
          value={data.calculationMethod}
          selectorName="adhan:method"
          options={calculationMethods}
          onChange={(calculationMethod) => patch({ ...data, calculationMethod })}
        />
        <SelectSetting
          label={t("madhhab")}
          value={data.madhhab}
          selectorName="adhan:madhhab"
          options={madhhabs}
          onChange={(madhhab) => patch({ ...data, madhhab })}
        />
        <SelectSetting
          label={t("highLatitudeRule")}
          value={data.highLatitudeRule}
          selectorName="adhan:high-latitude"
          options={highLatitudeRules}
          onChange={(highLatitudeRule) => patch({ ...data, highLatitudeRule })}
        />
        <div className="grid grid-cols-2 gap-3">
          <EditableSetting label={t("fajrAngle")} selectorName="adhan:fajr-angle" value={data.fajrAngle} onChange={(value) => patch({ ...data, fajrAngle: Number(value) || 0 })} />
          <EditableSetting label={t("ishaAngle")} selectorName="adhan:isha-angle" value={data.ishaAngle} onChange={(value) => patch({ ...data, ishaAngle: Number(value) || 0 })} />
        </div>
      </SectionBlock>

      <SectionBlock title={t("offsetsMinutes")}>
        <div className="grid grid-cols-2 gap-3">
          {prayers.map((prayer) => (
            <EditableSetting
              key={prayer}
              label={t(`prayer_${prayer[0].toUpperCase()}${prayer.slice(1)}`)}
              selectorName={`adhan:offset:${prayer}`}
              value={data.offsets[prayer] ?? 0}
              onChange={(value) => patch({ ...data, offsets: { ...data.offsets, [prayer]: Number(value) || 0 } })}
            />
          ))}
        </div>
      </SectionBlock>

      <SectionBlock title={t("fastingReminders")}>
        <div className="grid grid-cols-2 gap-3">
          <EditableSetting label={t("iftarDelay")} selectorName="adhan:iftar-delay" value={data.fasting.iftarDelay} onChange={(value) => patch({ ...data, fasting: { ...data.fasting, iftarDelay: Number(value) || 0 } })} />
          <EditableSetting label={t("imsakAdvance")} selectorName="adhan:imsak-advance" value={data.fasting.imsakAdvance} onChange={(value) => patch({ ...data, fasting: { ...data.fasting, imsakAdvance: Number(value) || 0 } })} />
        </div>
        <OptionButtons
          label={t("clockFormat")}
          value={data.clockFormat}
          selectorName="adhan:clock-format"
          options={clockFormats}
          onChange={(clockFormat) => patch({ ...data, clockFormat })}
        />
        <ReminderEditor
          title={t("imsakReminders")}
          selectorPrefix="adhan:imsak-reminder"
          reminders={data.imsakReminders ?? []}
          units={data.reminderUnits ?? defaultReminderUnits(t)}
          directions={data.reminderDirections ?? defaultReminderDirections(t)}
          onChange={(imsakReminders) => patch({ ...data, imsakReminders })}
        />
        <ReminderEditor
          title={t("iftarReminders")}
          selectorPrefix="adhan:iftar-reminder"
          reminders={data.iftarReminders ?? []}
          units={data.reminderUnits ?? defaultReminderUnits(t)}
          directions={data.reminderDirections ?? defaultReminderDirections(t)}
          onChange={(iftarReminders) => patch({ ...data, iftarReminders })}
        />
      </SectionBlock>

      <SectionBlock title={t("perPrayerAdhan")}>
        {data.perPrayerOverrides.map((override, index) => (
          <div key={override.prayer} className="grid gap-2 rounded-md border border-border bg-background p-3 text-sm">
            <div className="font-medium">{override.label ?? override.prayer}</div>
            <OptionButtons
              label={t("adhanSound")}
              value={override.soundId}
              selectorName={`adhan:override-sound:${override.prayer}`}
              options={[{ id: "default", label: t("useGlobal") }, ...data.sounds.map((sound) => ({ id: sound.id, label: sound.label }))]}
              onChange={(soundId) => patch({
                ...data,
                perPrayerOverrides: data.perPrayerOverrides.map((item, i) => i === index ? { ...item, soundId } : item),
              })}
            />
            <OptionButtons
              label={t("vibration")}
              value={override.vibration}
              selectorName={`adhan:override-vibration:${override.prayer}`}
              options={data.vibrationOverrideOptions ?? [
                { id: "default", label: t("useGlobal") },
                { id: "enabled", label: t("enabled") },
                { id: "none", label: t("disabled") },
              ]}
              onChange={(vibration) => patch({
                ...data,
                perPrayerOverrides: data.perPrayerOverrides.map((item, i) => i === index ? { ...item, vibration } : item),
              })}
            />
          </div>
        ))}
      </SectionBlock>
    </div>
  );
}

function SelectSetting({
  label,
  value,
  options,
  selectorName,
  onChange,
}: {
  label: string;
  value: string;
  options: Option[];
  selectorName: string;
  onChange: (value: string) => void;
}) {
  return (
    <label className="text-sm text-card-foreground">
      <span className="mb-1 block text-xs text-muted-foreground">{label}</span>
      <Picker value={value} onChange={onChange} selectorName={selectorName}>
        {options.map((option) => (
          <option key={option.id} value={option.id}>{option.label}</option>
        ))}
      </Picker>
    </label>
  );
}

function defaultReminderUnits(t: (key: string) => string): Option[] {
  return [{ id: "minute", label: t("minutes") }, { id: "hour", label: t("hours") }];
}

function defaultReminderDirections(t: (key: string) => string): Option[] {
  return [{ id: "before", label: t("before") }, { id: "after", label: t("after") }];
}

function ReminderEditor({
  title,
  selectorPrefix,
  reminders,
  units,
  directions,
  onChange,
}: {
  title: string;
  selectorPrefix: string;
  reminders: Reminder[];
  units: Option[];
  directions: Option[];
  onChange: (reminders: Reminder[]) => void;
}) {
  const t = useAppLabels();
  const addReminder = () => onChange([...reminders, { value: 10, unit: "minute", direction: "before" }]);
  return (
    <div className="rounded-md border border-border bg-background p-3">
      <div className="mb-2 text-xs font-medium text-muted-foreground">{title}</div>
      <button type="button" onClick={addReminder} data-selector-name={`${selectorPrefix}:add`} className="mb-3 rounded-md border border-border bg-card px-3 py-2 text-xs font-medium">
        {t("add")}
      </button>
      <div className="space-y-2">
        {reminders.map((reminder, index) => (
          <div key={`${selectorPrefix}-${index}`} className="grid grid-cols-[1fr_auto] gap-2 rounded-md border border-border p-2">
            <div className="grid grid-cols-3 gap-2">
              <EditableSetting label={t("newReminderText")} selectorName={`${selectorPrefix}:value:${index}`} value={reminder.value} onChange={(value) => onChange(reminders.map((item, i) => i === index ? { ...item, value: Number(value) || 0 } : item))} />
              <OptionButtons label={t("unit")} value={reminder.unit} selectorName={`${selectorPrefix}:unit:${index}`} options={units} onChange={(unit) => onChange(reminders.map((item, i) => i === index ? { ...item, unit } : item))} />
              <OptionButtons label={t("direction")} value={reminder.direction} selectorName={`${selectorPrefix}:direction:${index}`} options={directions} onChange={(direction) => onChange(reminders.map((item, i) => i === index ? { ...item, direction } : item))} />
            </div>
            <button type="button" onClick={() => onChange(reminders.filter((_, i) => i !== index))} data-selector-name={`${selectorPrefix}:remove:${index}`} className="self-end rounded-md border border-border px-2 py-1 text-xs">
              {t("remove")}
            </button>
          </div>
        ))}
      </div>
    </div>
  );
}
