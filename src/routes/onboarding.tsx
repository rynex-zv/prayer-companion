import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";
import { Card } from "@/components/Card";
import { Picker } from "@/components/Picker";
import { Field } from "@/components/Field";
import { AlertTriangle, ChevronRight } from "lucide-react";
import { PageLog } from "@/components/PageLog";
import { usePageLog } from "@/hooks/usePageLog";
import { refreshShellLabels, useAppLabels } from "@/hooks/useAppLabels";

export const Route = createFileRoute("/onboarding")({
  head: () => ({
    meta: [
      { title: "Welcome — Pray Ad Free" },
      { name: "robots", content: "noindex" },
    ],
  }),
  component: OnboardingPage,
});

type Snapshot = {
  completed?: boolean; steps?: string[]; language: string;
  permissionsScenario?: string; vpnWarning?: boolean;
  canUseInternet?: boolean; canUseGps?: boolean;
  title?: string; subtitle?: string;
  permissions?: unknown[];
  location?: { vpnWarning?: boolean };
};

function OnboardingPage() {
  usePageLog("onboarding");
  const t = useAppLabels();
  const { data, refresh } = useSnapshot<Snapshot>("onboarding.getSnapshot");
  const [step, setStep] = useState(0);
  const navigate = useNavigate();
  if (!data) return null;

  const steps = data.steps?.length ? data.steps : [t("language", "Language"), t("permissions", "Permissions"), t("locationAndGps", "Location")];
  const cur = steps[step];
  const locationVpnWarning = data.vpnWarning ?? data.location?.vpnWarning ?? false;

  return (
    <div className="flex min-h-[80vh] flex-col">
      <div className="mb-4 flex items-center gap-1">
        {steps.map((_, i) => (
          <div key={i} className={`h-1 flex-1 rounded-full ${i <= step ? "bg-primary" : "bg-muted"}`} />
        ))}
      </div>

      <Card className="flex-1 space-y-4">
        <div className="text-xs uppercase tracking-wider text-muted-foreground">
          {t("stepProgress", "Step")} {step + 1} {t("of", "of")} {steps.length}
        </div>
        <div className="flex items-center justify-between gap-2">
          <h1 className="text-2xl font-bold">{cur}</h1>
          <PageLog page="onboarding" />
        </div>

        {step === 0 && (
          <Field label={t("chooseLanguage", "Choose your language")}>
            <Picker value={data.language} onChange={(v) => mauiCall("app.setLanguage", { language: v }).then(() => { refreshShellLabels(); return refresh(); })}>
              {[
                { code: "en", name: "English" }, { code: "ar", name: "العربية" },
                { code: "fr", name: "Français" }, { code: "es", name: "Español" }, { code: "tr", name: "Türkçe" },
              ].map((l) => <option key={l.code} value={l.code}>{l.name}</option>)}
            </Picker>
          </Field>
        )}

        {step === 1 && (
          <div className="space-y-2">
            <p className="text-sm text-muted-foreground">{t("permissionsIntro", "Grant location and notification access for accurate prayer times and reminders.")}</p>
            <div className="rounded-lg bg-muted p-3 text-sm">
              {t("permissionStatus", "Permission status")}: <span className="font-medium">{data.permissionsScenario ?? `${data.permissions?.length ?? 0} ${t("permissions", "permissions")}`}</span>
            </div>
            <button onClick={() => mauiCall("settings.invoke", { action: "requestAllPermissions" })} className="w-full rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground">{t("grantPermissions", "Grant permissions")}</button>
          </div>
        )}

        {step === 2 && (
          <div className="space-y-2 text-sm">
            {data.title || data.subtitle ? (
              <>
                {data.title && <p className="font-medium">{data.title}</p>}
                {data.subtitle && <p className="text-muted-foreground">{data.subtitle}</p>}
              </>
            ) : data.canUseInternet === false && data.canUseGps === false ? (
              <p className="text-muted-foreground">{t("locationNoInternetGps", "No internet or GPS. Please set your location manually in Settings after onboarding.")}</p>
            ) : data.canUseInternet ? (
              <p className="text-muted-foreground">{t("locationNetwork", "We'll use your network to estimate your location. You can override this anytime.")}</p>
            ) : (
              <p className="text-muted-foreground">{t("locationGps", "GPS will be used to determine your location.")}</p>
            )}
            {locationVpnWarning && (
              <div className="flex items-start gap-2 rounded-md border border-warning/40 bg-warning/10 p-3 text-xs">
                <AlertTriangle className="h-4 w-4 text-warning" />
                {t("vpnWarning", "VPN detected - location may be inaccurate.")}
              </div>
            )}
          </div>
        )}
      </Card>

      <div className="mt-4 flex justify-between">
        <button onClick={() => setStep((s) => Math.max(0, s - 1))} disabled={step === 0} className="rounded-md px-4 py-2 text-sm font-medium text-muted-foreground disabled:opacity-30">{t("back", "Back")}</button>
        <button
          onClick={() => {
            if (step === steps.length - 1) {
              mauiCall("onboarding.complete").then(() => navigate({ to: "/" }));
            } else setStep((s) => s + 1);
          }}
          className="inline-flex items-center gap-1 rounded-md bg-primary px-5 py-2 text-sm font-medium text-primary-foreground"
        >
          {step === steps.length - 1 ? t("finish", "Finish") : t("next", "Next")} <ChevronRight className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}
