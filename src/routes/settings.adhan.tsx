import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { SettingsHeader } from "@/components/SettingsHeader";
import { EditableSetting, OptionButtons, SectionBlock, StatusLine } from "@/components/SettingsFormControls";
import { useAppLabels } from "@/hooks/useAppLabels";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";

export const Route = createFileRoute("/settings/adhan")({
  component: AdhanPage,
});

type AdhanSettings = {
  sounds: { id: string; label: string; selected: boolean; isCustom: boolean }[];
  volume: number;
  calculationMethod: string;
  madhhab: string;
  highLatitudeRule: string;
  fajrAngle: number;
  ishaAngle: number;
  isCustomMethod: boolean;
  offsets: Record<string, number>;
  clockFormat: string;
  fasting: { iftarDelay: number; imsakAdvance: number };
  imsakReminders: unknown[];
  iftarReminders: unknown[];
  perPrayerOverrides: { prayer: string; soundId: string; vibration: string }[];
};

const methods = ["MuslimWorldLeague", "Egyptian", "Karachi", "UmmAlQura", "Dubai", "Qatar", "Kuwait", "MoonsightingCommittee", "NorthAmerica", "Custom"];
const madhhabs = ["Shafi", "Hanafi"];
const highLatitudeRules = ["MiddleOfTheNight", "SeventhOfTheNight", "TwilightAngle"];
const prayers = ["fajr", "sunrise", "dhuhr", "asr", "maghrib", "isha", "imsak"];

function AdhanPage() {
  const t = useAppLabels();
  const { data, setData } = useSnapshot<AdhanSettings>("settings.getSnapshot", { section: "adhan" });
  const [status, setStatus] = useState("ready");
  if (!data) return null;

  const patch = (next: AdhanSettings) => {
    setData(next);
    setStatus("saving");
    void mauiCall("settings.patch", { adhan: next }).then((res) => setStatus(res.ok ? "saved" : "error"));
  };

  const selectedSound = data.sounds.find((sound) => sound.selected)?.id ?? data.sounds[0]?.id ?? "";

  return (
    <div data-selector-name="adhan:page" className="flex flex-col gap-3">
      <SettingsHeader title={t("adhan")} />
      <StatusLine selectorName="adhan:status" value={t(`status_${status}`)} />

      <SectionBlock title={t("adhanSound")}>
        <OptionButtons
          label={t("adhanSound")}
          value={selectedSound}
          selectorName="adhan:sound"
          options={data.sounds.map((sound) => ({ id: sound.id, label: sound.label }))}
          onChange={(id) => patch({ ...data, sounds: data.sounds.map((sound) => ({ ...sound, selected: sound.id === id })) })}
        />
        <EditableSetting
          label={t("volume")}
          selectorName="adhan:volume"
          value={data.volume}
          onChange={(value) => patch({ ...data, volume: Number(value) || 0 })}
        />
        <button
          type="button"
          onClick={() => void mauiCall("settings.invoke", { action: "previewSound", payload: { id: selectedSound } })}
          data-selector-name="adhan:preview"
          className="rounded-md border border-border bg-card px-3 py-2 text-sm font-medium text-card-foreground"
        >
          {t("testAlarm")}
        </button>
      </SectionBlock>

      <SectionBlock title={t("calculation")}>
        <OptionButtons
          label={t("method")}
          value={data.calculationMethod}
          selectorName="adhan:method"
          options={methods.map((id) => ({ id, label: t(`method_${id}`) }))}
          onChange={(calculationMethod) => patch({ ...data, calculationMethod })}
        />
        <OptionButtons
          label={t("madhhab")}
          value={data.madhhab}
          selectorName="adhan:madhhab"
          options={madhhabs.map((id) => ({ id, label: t(`madhhab_${id}`) }))}
          onChange={(madhhab) => patch({ ...data, madhhab })}
        />
        <OptionButtons
          label={t("highLatitudeRule")}
          value={data.highLatitudeRule}
          selectorName="adhan:high-latitude"
          options={highLatitudeRules.map((id) => ({ id, label: t(`highLatitude_${id}`) }))}
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
          options={[
            { id: "12h", label: t("clock12h") },
            { id: "24h", label: t("clock24h") },
          ]}
          onChange={(clockFormat) => patch({ ...data, clockFormat })}
        />
      </SectionBlock>
    </div>
  );
}
