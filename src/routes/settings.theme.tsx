import { createFileRoute } from "@tanstack/react-router";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";
import { Card } from "@/components/Card";
import { Field } from "@/components/Field";
import { Picker } from "@/components/Picker";
import { SettingsHeader } from "@/components/SettingsHeader";
import { SegmentedControl } from "@/components/SegmentedControl";
import { cn } from "@/lib/utils";
import { Minus, Plus } from "lucide-react";
import { usePageLog } from "@/hooks/usePageLog";

export const Route = createFileRoute("/settings/theme")({
  component: ThemePage,
});

type Theme = {
  language: string; themeMode: string; accentColor: string; textSize: number;
  diagnostics: { bridgeReady: boolean; lastSync: string };
  languages: { code: string; name: string }[];
  accentColors: string[];
};

const ACCENT_HEX: Record<string, string> = {
  teal: "#0d9488", green: "#16a34a", blue: "#2563eb", amber: "#d97706", rose: "#e11d48",
};

function ThemePage() {
  usePageLog("settings.theme-diagnostics");
  const { data, refresh } = useSnapshot<Theme>("settings.getSnapshot", { section: "theme" });
  if (!data) return null;
  const patch = (p: Partial<Theme>) => mauiCall("settings.patch", { theme: { ...data, ...p } }).then(refresh);

  return (
    <div>
      <SettingsHeader title="Theme & Diagnostics" />
      <div className="flex flex-col gap-3">
        <Card className="space-y-3">
          <Field label="Language">
            <Picker value={data.language} onChange={(v) => { patch({ language: v }); mauiCall("app.setLanguage", { language: v }); }}>
              {data.languages.map((l) => <option key={l.code} value={l.code}>{l.name}</option>)}
            </Picker>
          </Field>
          <Field label="Theme">
            <SegmentedControl
              value={data.themeMode}
              onChange={(v) => { patch({ themeMode: v }); mauiCall("app.setTheme", { theme: v }); }}
              options={[
                { id: "system", label: "System" },
                { id: "light", label: "Light" },
                { id: "dark", label: "Dark" },
              ]}
            />
          </Field>
          <Field label="Accent color">
            <div className="flex flex-wrap gap-2">
              {data.accentColors.map((c) => (
                <button
                  key={c}
                  onClick={() => patch({ accentColor: c })}
                  className={cn("h-9 w-9 rounded-full border-2 transition-all",
                    data.accentColor === c ? "border-foreground scale-110" : "border-transparent")}
                  style={{ backgroundColor: ACCENT_HEX[c] }}
                  aria-label={c}
                />
              ))}
            </div>
          </Field>
          <Field label="Text size">
            <div className="flex items-center gap-2">
              <button onClick={() => patch({ textSize: Math.max(75, data.textSize - 5) })} className="rounded-full bg-muted p-2"><Minus className="h-4 w-4" /></button>
              <div className="flex-1 text-center text-sm font-semibold tabular-nums">{data.textSize}%</div>
              <button onClick={() => patch({ textSize: Math.min(150, data.textSize + 5) })} className="rounded-full bg-muted p-2"><Plus className="h-4 w-4" /></button>
            </div>
          </Field>
        </Card>

        <Card>
          <div className="text-sm font-semibold">Diagnostics</div>
          <div className="mt-2 space-y-1 text-xs text-muted-foreground">
            <div>Bridge ready: <span className="font-medium text-foreground">{String(data.diagnostics.bridgeReady)}</span></div>
            <div>Last sync: <span className="font-medium text-foreground">{data.diagnostics.lastSync}</span></div>
          </div>
        </Card>
      </div>
    </div>
  );
}
