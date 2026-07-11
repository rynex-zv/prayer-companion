import { useNavigate, useRouterState } from "@tanstack/react-router";
import { Sun, Calendar, Compass, Circle, Settings as SettingsIcon } from "lucide-react";
import { cn } from "@/lib/utils";
import { mauiCall } from "@/native/mauiWebberClient";
import { useAppStore } from "@/state/appStore";

const tabs = [
  { to: "/", icon: Sun, key: "today" },
  { to: "/calendar", icon: Calendar, key: "calendar" },
  { to: "/qibla", icon: Compass, key: "qibla" },
  { to: "/tasbih", icon: Circle, key: "tasbih" },
  { to: "/settings", icon: SettingsIcon, key: "settings" },
] as const;

export function BottomTabs({ labels }: { labels: Record<string, string> }) {
  const pathname = useRouterState({ select: (s) => s.location.pathname });
  const navigate = useNavigate();
  const direction = useAppStore((state) => state.direction);
  const orderedTabs = direction === "rtl" ? [...tabs].reverse() : tabs;
  return (
    <nav className="safe-bottom z-30 mt-auto shrink-0 border-t border-border bg-card/90 backdrop-blur-md" data-selector-name="bottom-tabs" dir={direction}>
      <ul className="mx-auto flex max-w-md items-stretch justify-between px-2 pt-1.5">
        {orderedTabs.map((t) => {
          const active = t.to === "/" ? pathname === "/" : pathname.startsWith(t.to);
          const Icon = t.icon;
          return (
            <li key={t.key} className="flex-1">
              <button
                type="button"
                onClick={() => {
                  void navigate({ to: t.to });
                  void mauiCall("app.navigate", { route: t.to });
                }}
                data-selector-name={`tab:${t.key}`}
                className={cn(
                  "relative flex w-full flex-col items-center gap-0.5 rounded-xl px-2 py-1.5 text-[11px] font-medium transition-all duration-200 active:scale-95",
                  active ? "text-primary" : "text-muted-foreground hover:text-foreground",
                )}
              >
                {active && (
                  <span className="pointer-events-none absolute inset-x-3 top-0 h-0.5 rounded-full bg-primary" />
                )}
                <Icon className={cn("h-5 w-5 transition-transform", active && "scale-110 stroke-[2.5]")} />
                <span>{labels[t.key] ?? t.key}</span>
              </button>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
