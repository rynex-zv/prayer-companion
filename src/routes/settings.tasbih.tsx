import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { SettingsHeader } from "@/components/SettingsHeader";
import { EditableSetting, OptionButtons, SectionBlock, StatusLine } from "@/components/SettingsFormControls";
import { useAppLabels } from "@/hooks/useAppLabels";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";

export const Route = createFileRoute("/settings/tasbih")({
  component: TasbihSettingsPage,
});

type TasbihPreset = {
  id: string;
  name: string;
  repeatMode: string;
  items: { text: string; targetCount: number }[];
};

type TasbihSnapshot = {
  selectedPresetId: string;
  presets: TasbihPreset[];
};

const repeatModes = ["Continue", "Reset", "None"];

function TasbihSettingsPage() {
  const t = useAppLabels();
  const { data, refresh } = useSnapshot<TasbihSnapshot>("tasbih.getSnapshot");
  const [status, setStatus] = useState("ready");
  if (!data) return null;

  const invoke = (action: string, payload: unknown) => {
    setStatus("saving");
    void mauiCall("settings.invoke", { action, payload }).then((res) => {
      setStatus(res.ok ? "saved" : "error");
      void refresh();
    });
  };

  return (
    <div data-selector-name="settings-tasbih:page" className="flex flex-col gap-3">
      <SettingsHeader title={t("tasbihSettings")} />
      <StatusLine selectorName="settings-tasbih:status" value={t(`status_${status}`)} />

      {data.presets.map((preset) => (
        <SectionBlock key={preset.id} title={preset.name}>
          <EditableSetting
            label={t("tasbihPresetName")}
            selectorName={`settings-tasbih:preset-name:${preset.id}`}
            value={preset.name}
            onChange={(name) => invoke("updateTasbihPreset", { id: preset.id, name, repeatMode: preset.repeatMode })}
          />
          <OptionButtons
            label={t("repeatMode")}
            value={preset.repeatMode}
            selectorName={`settings-tasbih:repeat:${preset.id}`}
            options={repeatModes.map((id) => ({ id, label: t(`tasbihRepeat_${id}`) }))}
            onChange={(repeatMode) => invoke("updateTasbihPreset", { id: preset.id, name: preset.name, repeatMode })}
          />
          {preset.items.map((item, index) => (
            <div key={`${preset.id}-${index}`} className="rounded-md border border-border bg-background p-3">
              <EditableSetting
                label={t("itemText")}
                selectorName={`settings-tasbih:item-text:${preset.id}:${index}`}
                value={item.text}
                onChange={(text) => invoke("updateTasbihItem", { presetId: preset.id, index, text, targetCount: item.targetCount })}
              />
              <EditableSetting
                className="mt-3"
                label={t("targetCount")}
                selectorName={`settings-tasbih:item-count:${preset.id}:${index}`}
                value={item.targetCount}
                onChange={(targetCount) => invoke("updateTasbihItem", { presetId: preset.id, index, text: item.text, targetCount: Number(targetCount) || 1 })}
              />
              <button
                type="button"
                onClick={() => invoke("removeTasbihItem", { presetId: preset.id, index })}
                data-selector-name={`settings-tasbih:item-remove:${preset.id}:${index}`}
                className="mt-3 rounded-md border border-border bg-card px-3 py-2 text-xs font-medium text-card-foreground"
              >
                {t("remove")}
              </button>
            </div>
          ))}
          <button
            type="button"
            onClick={() => invoke("addTasbihItem", { presetId: preset.id, text: t("itemText"), targetCount: 33 })}
            data-selector-name={`settings-tasbih:add-item:${preset.id}`}
            className="rounded-md border border-border bg-card px-3 py-2 text-sm font-medium text-card-foreground"
          >
            {t("add")}
          </button>
        </SectionBlock>
      ))}

      <button
        type="button"
        onClick={() => invoke("addTasbihPreset", { name: t("newPresetName") })}
        data-selector-name="settings-tasbih:add-preset"
        className="rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground"
      >
        {t("add")}
      </button>
    </div>
  );
}
