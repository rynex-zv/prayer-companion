import { createFileRoute, Outlet, useNavigate, useRouterState } from "@tanstack/react-router";
import { Card } from "@/components/Card";
import { MapPin, Palette, Volume2, Bell, ShieldCheck, AlarmClock, Circle, Info, ChevronRight } from "lucide-react";
import { PageLog } from "@/components/PageLog";
import { usePageLog } from "@/hooks/usePageLog";
import { mauiCall } from "@/native/mauiWebberClient";
import { useAppLabels } from "@/hooks/useAppLabels";

export const Route = createFileRoute("/settings")({
  head: () => ({
    meta: [
      { title: "Settings — Pray Ad Free" },
      { name: "description", content: "App settings: location, theme, adhan, notifications, permissions, and more." },
    ],
  }),
  component: SettingsLayout,
});

function SettingsLayout() {
  const pathname = useRouterState({ select: (s) => s.location.pathname });
  // Only show menu at /settings exactly
  if (pathname === "/settings") return <SettingsIndex />;
  return <Outlet />;
}

const items = [
  { to: "/settings/locations", icon: MapPin, titleKey: "locations", title: "Locations", subtitleKey: "locationAndGps", subtitle: "Location and GPS" },
  { to: "/settings/theme", icon: Palette, titleKey: "themeDiagnostics", title: "Theme & Diagnostics", subtitleKey: "themeLanguageAccent", subtitle: "Theme, language, accent" },
  { to: "/settings/adhan", icon: Volume2, titleKey: "adhan", title: "Adhan Customizations", subtitleKey: "soundAndCalculation", subtitle: "Sound and calculation" },
  { to: "/settings/notifications", icon: Bell, titleKey: "notifications", title: "Notifications", subtitleKey: "remindersAndVibration", subtitle: "Reminders and vibration" },
  { to: "/settings/permissions", icon: ShieldCheck, titleKey: "permissions", title: "Permissions", subtitleKey: "systemPermissions", subtitle: "System permissions" },
  { to: "/settings/alarms", icon: AlarmClock, titleKey: "alarmReminders", title: "Alarm Reminders", subtitleKey: "alarmScreenReminders", subtitle: "Alarm-screen reminders" },
  { to: "/settings/tasbih", icon: Circle, titleKey: "tasbihSettings", title: "Tasbih", subtitleKey: "tasbihPresets", subtitle: "Tasbih presets" },
  { to: "/settings/about", icon: Info, titleKey: "about", title: "About", subtitleKey: "appAndContactInfo", subtitle: "App and contact info" },
] as const;

function SettingsIndex() {
  usePageLog("settings");
  const t = useAppLabels();
  const navigate = useNavigate();
  return (
    <div className="flex flex-col gap-3" data-selector-name="settings:index">
      <div className="flex items-center justify-between gap-2">
        <h1 className="text-xl font-bold" data-selector-name="settings:index-title">{t("settings", "Settings")}</h1>
        <PageLog page="settings" />
      </div>
      <Card className="divide-y divide-border p-0">
        {items.map((it) => {
          const Icon = it.icon;
          return (
            <button
              key={it.to}
              type="button"
              onClick={() => {
                void navigate({ to: it.to });
                void mauiCall("app.navigate", { route: it.to });
              }}
              data-selector-name={`settings:row:${it.titleKey}`}
              className="flex w-full items-center gap-3 px-4 py-3 text-start transition-colors hover:bg-muted/60"
            >
              <div className="flex h-9 w-9 items-center justify-center rounded-full bg-secondary text-secondary-foreground">
                <Icon className="h-4 w-4" />
              </div>
              <div className="flex-1">
                <div className="text-sm font-semibold">{t(it.titleKey, it.title)}</div>
                <div className="text-xs text-muted-foreground">{t(it.subtitleKey, it.subtitle)}</div>
              </div>
              <ChevronRight className="h-4 w-4 text-muted-foreground" />
            </button>
          );
        })}
      </Card>
    </div>
  );
}
