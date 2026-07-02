import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";
import { Card } from "@/components/Card";
import { Picker } from "@/components/Picker";
import { Field } from "@/components/Field";
import { SettingsHeader } from "@/components/SettingsHeader";
import { Plus, X, ArrowUp, ArrowDown } from "lucide-react";
import { usePageLog } from "@/hooks/usePageLog";
import { useAppLabels } from "@/hooks/useAppLabels";

export const Route = createFileRoute("/settings/tasbih")({
  component: TasbihSettingsPage,
});

type Preset = { id: string; name: string; repeatMode: string; items: { text: string; targetCount: number }[] };
type Snapshot = { presets: Preset[]; selectedPresetId: string };

function TasbihSettingsPage() {
  usePageLog("settings.tasbih-presets");
  const t = useAppLabels();
  const { data, refresh } = useSnapshot<Snapshot>("tasbih.getSnapshot");
  const [newName, setNewName] = useState("");
  const [itemText, setItemText] = useState("");
  const [itemCount, setItemCount] = useState(33);
  const [selectedId, setSelectedId] = useState<string>("");
  if (!data) return null;

  const id = selectedId || data.selectedPresetId;
  const preset = data.presets.find((p) => p.id === id);
  const invoke = (action: string, payload?: unknown) => mauiCall("settings.invoke", { action, payload }).then(refresh);

  return (
    <div>
      <SettingsHeader title={t("tasbihPresets", "Tasbih Presets")} />
      <div className="flex flex-col gap-3">
        <Card className="flex gap-2">
          <input value={newName} onChange={(e) => setNewName(e.target.value)} placeholder={t("newPresetName", "New preset name...")} className="flex-1 rounded-lg border border-input bg-card px-3 py-2 text-sm" />
          <button onClick={() => { mauiCall("settings.invoke", { action: "addTasbihPreset", payload: { name: newName } }).then(refresh); setNewName(""); }} className="rounded-md bg-primary px-3 text-primary-foreground"><Plus className="h-4 w-4" /></button>
        </Card>

        <Card>
          <Field label={t("editPreset", "Edit preset")}>
            <Picker value={id} onChange={setSelectedId}>
              {data.presets.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
            </Picker>
          </Field>
          {preset && (
            <>
              <Field label={t("tasbihPresetName", "Name")} className="mt-3">
                <input
                  defaultValue={preset.name}
                  onBlur={(e) => invoke("updateTasbihPreset", { id: preset.id, name: e.target.value })}
                  className="rounded-lg border border-input bg-card px-3 py-2 text-sm"
                />
              </Field>
              <Field label={t("repeatMode", "Repeat mode")} className="mt-3">
                <Picker value={preset.repeatMode} onChange={(repeatMode) => invoke("updateTasbihPreset", { id: preset.id, repeatMode })}>
                  {["Sequence", "Loop", "Once"].map((m) => <option key={m} value={m}>{t(`tasbihRepeat_${m}`, m)}</option>)}
                </Picker>
              </Field>

              <div className="mt-4 text-sm font-semibold">{t("items", "Items")}</div>
              <ul className="mt-2 space-y-2">
                {preset.items.map((it, i) => (
                  <li key={i} className="flex items-center gap-2">
                    <input
                      defaultValue={it.text}
                      onBlur={(e) => invoke("updateTasbihItem", { presetId: preset.id, index: i, text: e.target.value })}
                      className="flex-1 rounded-lg border border-input bg-card px-2 py-1.5 text-sm"
                    />
                    <input
                      type="number"
                      defaultValue={it.targetCount}
                      onBlur={(e) => invoke("updateTasbihItem", { presetId: preset.id, index: i, targetCount: Number(e.target.value) })}
                      className="w-20 rounded-lg border border-input bg-card px-2 py-1.5 text-sm"
                    />
                    <button onClick={() => invoke("moveTasbihItem", { presetId: preset.id, index: i, direction: "up" })} className="rounded-full p-1 hover:bg-muted"><ArrowUp className="h-4 w-4" /></button>
                    <button onClick={() => invoke("moveTasbihItem", { presetId: preset.id, index: i, direction: "down" })} className="rounded-full p-1 hover:bg-muted"><ArrowDown className="h-4 w-4" /></button>
                    <button onClick={() => invoke("removeTasbihItem", { presetId: preset.id, index: i })} className="rounded-full p-1 hover:bg-muted text-muted-foreground"><X className="h-4 w-4" /></button>
                  </li>
                ))}
              </ul>

              <div className="mt-3 grid grid-cols-[1fr_auto_auto] gap-2">
                <input value={itemText} onChange={(e) => setItemText(e.target.value)} placeholder={t("itemText", "Item text")} className="rounded-lg border border-input bg-card px-2 py-1.5 text-sm" />
                <input type="number" value={itemCount} onChange={(e) => setItemCount(Number(e.target.value))} className="w-20 rounded-lg border border-input bg-card px-2 py-1.5 text-sm" />
                <button
                  onClick={() => {
                    if (!itemText.trim()) return;
                    invoke("addTasbihItem", { presetId: preset.id, text: itemText.trim(), targetCount: itemCount }).then(() => setItemText(""));
                  }}
                  className="rounded-md bg-primary px-3 text-primary-foreground"
                >
                  <Plus className="h-4 w-4" />
                </button>
              </div>
            </>
          )}
        </Card>
      </div>
    </div>
  );
}
