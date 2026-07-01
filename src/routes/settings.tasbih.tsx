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

export const Route = createFileRoute("/settings/tasbih")({
  component: TasbihSettingsPage,
});

type Preset = { id: string; name: string; repeatMode: string; items: { text: string; targetCount: number }[] };
type Snapshot = { presets: Preset[]; selectedPresetId: string };

function TasbihSettingsPage() {
  usePageLog("settings.tasbih-presets");
  const { data, refresh } = useSnapshot<Snapshot>("tasbih.getSnapshot");
  const [newName, setNewName] = useState("");
  const [itemText, setItemText] = useState("");
  const [itemCount, setItemCount] = useState(33);
  const [selectedId, setSelectedId] = useState<string>("");
  if (!data) return null;

  const id = selectedId || data.selectedPresetId;
  const preset = data.presets.find((p) => p.id === id);

  return (
    <div>
      <SettingsHeader title="Tasbih Presets" />
      <div className="flex flex-col gap-3">
        <Card className="flex gap-2">
          <input value={newName} onChange={(e) => setNewName(e.target.value)} placeholder="New preset name…" className="flex-1 rounded-lg border border-input bg-card px-3 py-2 text-sm" />
          <button onClick={() => { mauiCall("settings.invoke", { action: "addTasbihPreset", payload: { name: newName } }).then(refresh); setNewName(""); }} className="rounded-md bg-primary px-3 text-primary-foreground"><Plus className="h-4 w-4" /></button>
        </Card>

        <Card>
          <Field label="Edit preset">
            <Picker value={id} onChange={setSelectedId}>
              {data.presets.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
            </Picker>
          </Field>
          {preset && (
            <>
              <Field label="Name" className="mt-3">
                <input defaultValue={preset.name} className="rounded-lg border border-input bg-card px-3 py-2 text-sm" />
              </Field>
              <Field label="Repeat mode" className="mt-3">
                <Picker value={preset.repeatMode} onChange={() => undefined}>
                  {["Sequence", "Loop", "Once"].map((m) => <option key={m} value={m}>{m}</option>)}
                </Picker>
              </Field>

              <div className="mt-4 text-sm font-semibold">Items</div>
              <ul className="mt-2 space-y-2">
                {preset.items.map((it, i) => (
                  <li key={i} className="flex items-center gap-2">
                    <input defaultValue={it.text} className="flex-1 rounded-lg border border-input bg-card px-2 py-1.5 text-sm" />
                    <input type="number" defaultValue={it.targetCount} className="w-20 rounded-lg border border-input bg-card px-2 py-1.5 text-sm" />
                    <button className="rounded-full p-1 hover:bg-muted"><ArrowUp className="h-4 w-4" /></button>
                    <button className="rounded-full p-1 hover:bg-muted"><ArrowDown className="h-4 w-4" /></button>
                    <button className="rounded-full p-1 hover:bg-muted text-muted-foreground"><X className="h-4 w-4" /></button>
                  </li>
                ))}
              </ul>

              <div className="mt-3 grid grid-cols-[1fr_auto_auto] gap-2">
                <input value={itemText} onChange={(e) => setItemText(e.target.value)} placeholder="Item text" className="rounded-lg border border-input bg-card px-2 py-1.5 text-sm" />
                <input type="number" value={itemCount} onChange={(e) => setItemCount(Number(e.target.value))} className="w-20 rounded-lg border border-input bg-card px-2 py-1.5 text-sm" />
                <button className="rounded-md bg-primary px-3 text-primary-foreground"><Plus className="h-4 w-4" /></button>
              </div>
            </>
          )}
        </Card>
      </div>
    </div>
  );
}
