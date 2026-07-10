import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { SettingsHeader } from "@/components/SettingsHeader";
import { OptionButtons, SectionBlock, StatusLine } from "@/components/SettingsFormControls";
import { useAppLabels } from "@/hooks/useAppLabels";
import { useStoredSnapshot } from "@/hooks/useStoredSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";
import { Picker } from "@/components/Picker";

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
  const { data, refresh, setData } = useStoredSnapshot<TasbihSnapshot>("tasbih.getSnapshot", undefined, "settings.tasbih");
  const [status, setStatus] = useState("ready");
  const [newPresetName, setNewPresetName] = useState("");
  const [newItemText, setNewItemText] = useState("");
  const [newItemCount, setNewItemCount] = useState("33");
  if (!data) return null;

  const invoke = (action: string, payload: unknown) => {
    setStatus("saving");
    void mauiCall("settings.invoke", { action, payload }).then((res) => {
      setStatus(res.ok ? "saved" : "error");
      if (res.ok) {
        setData(res.data as TasbihSnapshot);
      } else {
        void refresh(true);
      }
    });
  };

  return (
    <div data-selector-name="settings-tasbih:page" className="flex flex-col gap-3">
      <SettingsHeader title={t("tasbihSettings")} />
      <StatusLine selectorName="settings-tasbih:status" value={t(`status_${status}`)} />

      <SectionBlock title={t("tasbihPresets")}>
        <div className="grid grid-cols-[1fr_auto] gap-2">
          <input
            value={newPresetName}
            onChange={(event) => setNewPresetName(event.currentTarget.value)}
            placeholder={t("newPresetName")}
            data-selector-name="settings-tasbih:new-preset-name"
            className="min-h-9 rounded-md border border-input bg-card px-3 py-2 text-sm"
          />
          <button
            type="button"
            onClick={() => {
              invoke("addTasbihPreset", { name: newPresetName.trim() || t("newPresetName") });
              setNewPresetName("");
            }}
            data-selector-name="settings-tasbih:add-preset-from-input"
            className="rounded-md border border-border bg-card px-3 py-2 text-sm font-medium"
          >
            {t("add")}
          </button>
        </div>
        <Picker value={data.selectedPresetId} onChange={(id) => void mauiCall("tasbih.selectPreset", { id }).then((res) => res.ok ? setData(res.data as TasbihSnapshot) : refresh(true))}>
          {data.presets.map((preset) => <option key={preset.id} value={preset.id}>{preset.name}</option>)}
        </Picker>
      </SectionBlock>

      {data.presets.filter((preset) => preset.id === data.selectedPresetId).map((preset) => (
        <SectionBlock key={preset.id} title={preset.name}>
          <DeferredEditableSetting
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
              <DeferredEditableSetting
                label={t("itemText")}
                selectorName={`settings-tasbih:item-text:${preset.id}:${index}`}
                value={item.text}
                onChange={(text) => invoke("updateTasbihItem", { presetId: preset.id, index, text, targetCount: item.targetCount })}
              />
              <DeferredEditableSetting
                className="mt-3"
                label={t("targetCount")}
                selectorName={`settings-tasbih:item-count:${preset.id}:${index}`}
                value={item.targetCount}
                onChange={(targetCount) => invoke("updateTasbihItem", { presetId: preset.id, index, text: item.text, targetCount: Number(targetCount) || 1 })}
              />
              <div className="mt-3 text-xs text-muted-foreground">
                {t("startIndex")}: <span dir="ltr">{index + 1}</span>
              </div>
              <div className="mt-3 grid grid-cols-2 gap-2">
                <button type="button" onClick={() => invoke("moveTasbihItem", { presetId: preset.id, index, direction: "up" })} data-selector-name={`settings-tasbih:item-up:${preset.id}:${index}`} className="rounded-md border border-border bg-card px-3 py-2 text-xs font-medium">
                  {t("moveUp")}
                </button>
                <button type="button" onClick={() => invoke("moveTasbihItem", { presetId: preset.id, index, direction: "down" })} data-selector-name={`settings-tasbih:item-down:${preset.id}:${index}`} className="rounded-md border border-border bg-card px-3 py-2 text-xs font-medium">
                  {t("moveDown")}
                </button>
              </div>
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
          <div className="grid grid-cols-[2fr_1fr] gap-2">
            <input
              value={newItemText}
              onChange={(event) => setNewItemText(event.currentTarget.value)}
              placeholder={t("itemText")}
              data-selector-name={`settings-tasbih:new-item-text:${preset.id}`}
              className="min-h-9 rounded-md border border-input bg-card px-3 py-2 text-sm"
            />
            <input
              value={newItemCount}
              onChange={(event) => setNewItemCount(event.currentTarget.value)}
              placeholder={t("targetCount")}
              data-selector-name={`settings-tasbih:new-item-count:${preset.id}`}
              className="min-h-9 rounded-md border border-input bg-card px-3 py-2 text-sm"
              dir="ltr"
            />
          </div>
          <button
            type="button"
            onClick={() => {
              invoke("addTasbihItem", { presetId: preset.id, text: newItemText.trim() || t("itemText"), targetCount: Number(newItemCount) || 33 });
              setNewItemText("");
              setNewItemCount("33");
            }}
            data-selector-name={`settings-tasbih:add-item:${preset.id}`}
            className="rounded-md border border-border bg-card px-3 py-2 text-sm font-medium text-card-foreground"
          >
            {t("add")}
          </button>
        </SectionBlock>
      ))}
    </div>
  );
}

function DeferredEditableSetting({
  label,
  value,
  selectorName,
  onChange,
  className,
}: {
  label: string;
  value: string | number;
  selectorName: string;
  onChange: (value: string) => void;
  className?: string;
}) {
  const [draft, setDraft] = useState(String(value));

  useEffect(() => {
    setDraft(String(value));
  }, [value]);

  const commit = () => {
    if (draft !== String(value)) {
      onChange(draft);
    }
  };

  return (
    <div className={className}>
      <label className="text-sm text-card-foreground">
        <span className="mb-1 block text-xs text-muted-foreground">{label}</span>
        <input
          value={draft}
          onChange={(event) => setDraft(event.currentTarget.value)}
          onBlur={commit}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.currentTarget.blur();
            }
          }}
          data-selector-name={selectorName}
          className="min-h-9 w-full rounded-md border border-input bg-card px-3 py-2 text-sm text-card-foreground"
        />
      </label>
    </div>
  );
}
