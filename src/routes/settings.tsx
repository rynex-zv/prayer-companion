import { createFileRoute, Link, Outlet, useRouterState } from "@tanstack/react-router";
import { Card } from "@/components/Card";
import { MapPin, Palette, Volume2, Bell, ShieldCheck, AlarmClock, Circle, Info, ChevronRight } from "lucide-react";
import { PageLog } from "@/components/PageLog";
import { usePageLog } from "@/hooks/usePageLog";

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
  { to: "/settings/locations", icon: MapPin, title: "Locations", subtitle: "Location and GPS" },
  { to: "/settings/theme", icon: Palette, title: "Theme & Diagnostics", subtitle: "Theme, language, accent" },
  { to: "/settings/adhan", icon: Volume2, title: "Adhan Customizations", subtitle: "Sound and calculation" },
  { to: "/settings/notifications", icon: Bell, title: "Notifications", subtitle: "Reminders and vibration" },
  { to: "/settings/permissions", icon: ShieldCheck, title: "Permissions", subtitle: "System permissions" },
  { to: "/settings/alarms", icon: AlarmClock, title: "Alarm Reminders", subtitle: "Alarm-screen reminders" },
  { to: "/settings/tasbih", icon: Circle, title: "Tasbih", subtitle: "Tasbih presets" },
  { to: "/settings/about", icon: Info, title: "About", subtitle: "App and contact info" },
] as const;

function SettingsIndex() {
  usePageLog("settings");
  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between gap-2">
        <h1 className="text-xl font-bold">Settings</h1>
        <PageLog page="settings" />
      </div>
      <Card className="divide-y divide-border p-0">
        {items.map((it) => {
          const Icon = it.icon;
          return (
            <Link
              key={it.to}
              to={it.to}
              className="flex items-center gap-3 px-4 py-3 transition-colors hover:bg-muted/60"
            >
              <div className="flex h-9 w-9 items-center justify-center rounded-full bg-secondary text-secondary-foreground">
                <Icon className="h-4 w-4" />
              </div>
              <div className="flex-1">
                <div className="text-sm font-semibold">{it.title}</div>
                <div className="text-xs text-muted-foreground">{it.subtitle}</div>
              </div>
              <ChevronRight className="h-4 w-4 text-muted-foreground" />
            </Link>
          );
        })}
      </Card>
    </div>
  );
}
