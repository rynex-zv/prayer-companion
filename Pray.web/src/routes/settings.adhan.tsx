import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { SettingsHeader } from "@/components/SettingsHeader";
import { EditableSetting, OptionButtons, SectionBlock, StatusLine } from "@/components/SettingsFormControls";
import { Picker } from "@/components/Picker";
import { useAppLabels } from "@/hooks/useAppLabels";
import { useProjection } from "@/hooks/useProjection";
import { patchSettingsSection, platformIntents } from "@/client/applicationClient";

export const Route = createFileRoute("/settings/adhan")({
  component: AdhanPage,
});

type AdhanSettings = {
  sounds: { id: string; label: string; selected: boolean; isCustom: boolean; canPreview?: boolean }[];
  volume: number;
  calculationEngine?: string;
  calculationEngines?: Option[];
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

const prayers = ["fajr", "sunrise", "dhuhr", "asr", "maghrib", "isha", "imsak"];

function AdhanPage() {
  const t = useAppLabels();
  const { data, setData } = useProjection<AdhanSettings>("settings.getSnapshot", { section: "adhan" }, "settings.adhan");
  const [status, setStatus] = useState("ready");

  if (!data) return null;

  const catalogsReady = Boolean(
    data.calculationEngines?.length === 1 &&
    data.calculationMethods?.length &&
    data.madhhabs?.length &&
    data.highLatitudeRules?.length &&
    data.clockFormats?.length,
  );
  if (!catalogsReady) {
    return (
      <div data-selector-name="adhan:page" className="flex flex-col gap-3">
        <SettingsHeader title={t("adhan")} />
        <StatusLine selectorName="adhan:status" value={t("status_error")} />
        <div role="alert" className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
          {t("errorSomethingWentWrong")}
        </div>
      </div>
    );
  }

  const patch = (next: AdhanSettings) => {
    setData(next);
    setStatus("saving");
    void patchSettingsSection("adhan", next).then((response) => {
      if (!response.ok) return setStatus("error");
      setData(response.data.projection);
      setStatus("saved");
    });
  };

  const applyProjectionResponse = (response: { ok: boolean; data?: unknown; error?: string }) => {
    if (!response.ok) {
      setStatus("error");
      return;
    }

    const payload = response.data as { projection?: AdhanSettings; cancelled?: boolean } | undefined;
    if (payload?.cancelled) {
      setStatus("ready");
      return;
    }

    if (payload?.projection) {
      setData(payload.projection);
      setStatus("saved");
    }
  };

  const addCustomSound = async () => {
    setStatus("saving");
    applyProjectionResponse(await platformIntents.addCustomAdhanSound());
  };

  const removeCustomSound = async (id: string) => {
    setStatus("saving");
    applyProjectionResponse(await platformIntents.removeCustomAdhanSound(id));
  };

  const previewSound = async (id: string) => {
    const response = await platformIntents.previewAdhanSound(id);
    if (!response.ok) setStatus("error");
  };

  const calculationMethods = data.calculationMethods!;
  const madhhabs = data.madhhabs!;
  const highLatitudeRules = data.highLatitudeRules!;
  const clockFormats = data.clockFormats!;

  return (
    <div data-selector-name="adhan:page" className="flex flex-col gap-3">
      <SettingsHeader title={t("adhan")} />
      <StatusLine selectorName="adhan:status" value={t(`status_${status}`)} />

      <SectionBlock title={t("adhanSound")}>
        <button
          type="button"
          onClick={() => void addCustomSound()}
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
              <button type="button" disabled={sound.canPreview === false} onClick={() => void previewSound(sound.id)} className="rounded-md border border-border px-2 py-1 text-xs disabled:opacity-40" data-selector-name={`adhan:sound-play:${sound.id}`}>
                {t("play")}
              </button>
              {sound.isCustom ? (
                <button type="button" onClick={() => void removeCustomSound(sound.id)} className="rounded-md border border-border px-2 py-1 text-xs" data-selector-name={`adhan:sound-remove:${sound.id}`}>
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
            aria-label={t("volume")}
            data-selector-name="adhan:volume"
            className="w-full accent-primary"
          />
        </div>
      </SectionBlock>

      <SectionBlock title={t("calculation")}>
        <div className="text-sm text-card-foreground">
          <div className="mb-1 text-xs text-muted-foreground">{t("calculationEngine")}</div>
          <div data-selector-name="adhan:calculation-engine" className="rounded-md border border-border bg-muted/30 px-3 py-2">
            {data.calculationEngines![0].label}
          </div>
        </div>
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
