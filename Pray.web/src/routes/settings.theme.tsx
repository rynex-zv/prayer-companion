import { createFileRoute } from "@tanstack/react-router";
import { Card } from "@/components/Card";
import { Field } from "@/components/Field";
import { Picker } from "@/components/Picker";
import { SettingsHeader } from "@/components/SettingsHeader";
import { SegmentedControl } from "@/components/SegmentedControl";
import { cn } from "@/lib/utils";
import { Minus, Plus } from "lucide-react";
import { usePageLog } from "@/hooks/usePageLog";
import { useAppLabels } from "@/hooks/useAppLabels";
import { setLanguage, setThemeField, useAppStore } from "@/state/appStore";

export const Route = createFileRoute("/settings/theme")({
  component: ThemePage,
});

const ACCENT_HEX: Record<string, string> = {
  teal: "#0d9488",
  green: "#16a34a",
  blue: "#2563eb",
  amber: "#d97706",
  rose: "#e11d48",
};

const accentColors = ["teal", "green", "blue", "amber", "rose"];

function ThemePage() {
  usePageLog("settings.theme-diagnostics");
  const t = useAppLabels();
  const theme = useAppStore((state) => ({
    language: state.languageObject.code,
    languages: state.languages,
    themeMode: state.themeMode,
    accentColor: state.accentColor,
    textSize: state.textSize,
    bridgeReady: state.fieldSync["shell.bootstrap"]?.status !== "error",
    fieldSync: state.fieldSync,
  }));

  return (
    <div>
      <SettingsHeader title={t("themeDiagnostics")} />
      <div className="flex flex-col gap-3">
        <Card className="space-y-3">
          <Field label={t("language")}>
            <Picker value={theme.language} onChange={(value) => void setLanguage(value)} selectorName="theme:language">
              {theme.languages.map((language) => (
                <option key={language.code} value={language.code}>
                  {language.name}
                </option>
              ))}
            </Picker>
          </Field>
          <Field label={t("themeMode")}>
            <SegmentedControl
              selectorPrefix="theme:mode"
              value={theme.themeMode}
              onChange={(value) => void setThemeField("themeMode", value)}
              options={[
                { id: "system", label: t("system") },
                { id: "light", label: t("light") },
                { id: "dark", label: t("dark") },
              ]}
            />
          </Field>
          <Field label={t("accentColor")}>
            <div className="flex flex-wrap gap-2">
              {accentColors.map((color) => (
                <button
                  key={color}
                  type="button"
                  onClick={() => void setThemeField("accentColor", color)}
                  className={cn(
                    "h-9 w-9 rounded-full border-2 transition-all",
                    theme.accentColor === color ? "scale-110 border-foreground" : "border-transparent",
                  )}
                  style={{ backgroundColor: ACCENT_HEX[color] }}
                  aria-label={color}
                  data-selector-name={`theme:accent:${color}`}
                />
              ))}
            </div>
          </Field>
          <Field label={t("textSize")}>
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => void setThemeField("textSize", Math.max(75, theme.textSize - 5))}
                aria-label={`${t("textSize")} −`}
                className="rounded-full bg-muted p-2"
                data-selector-name="theme:text-size:decrease"
              >
                <Minus className="h-4 w-4" />
              </button>
              <div className="flex-1 text-center text-sm font-semibold tabular-nums" dir="ltr">
                {theme.textSize}%
              </div>
              <button
                type="button"
                onClick={() => void setThemeField("textSize", Math.min(150, theme.textSize + 5))}
                aria-label={`${t("textSize")} +`}
                className="rounded-full bg-muted p-2"
                data-selector-name="theme:text-size:increase"
              >
                <Plus className="h-4 w-4" />
              </button>
            </div>
          </Field>
        </Card>

        <Card>
          <div className="text-sm font-semibold">{t("diagnostics")}</div>
          <div className="mt-2 space-y-1 text-xs text-muted-foreground">
            <div>
              {t("bridgeReady")}: <span className="font-medium text-foreground">{String(theme.bridgeReady)}</span>
            </div>
            <div>
              {t("lastSync")}:{" "}
              <span className="font-medium text-foreground">
                {theme.fieldSync["theme.language"]?.status ?? "clean"}
              </span>
            </div>
          </div>
        </Card>
      </div>
    </div>
  );
}
