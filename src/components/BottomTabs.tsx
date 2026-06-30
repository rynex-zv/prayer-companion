import { Link, useRouterState } from "@tanstack/react-router";
import { Sun, Calendar, Compass, Circle, Settings as SettingsIcon } from "lucide-react";
import { cn } from "@/lib/utils";

const tabs = [
  { to: "/", icon: Sun, key: "today" },
  { to: "/calendar", icon: Calendar, key: "calendar" },
  { to: "/qibla", icon: Compass, key: "qibla" },
  { to: "/tasbih", icon: Circle, key: "tasbih" },
  { to: "/settings", icon: SettingsIcon, key: "settings" },
] as const;

export function BottomTabs({ labels }: { labels: Record<string, string> }) {
  const pathname = useRouterState({ select: (s) => s.location.pathname });
  return (
    <nav className="safe-bottom sticky bottom-0 z-30 mt-auto border-t border-border bg-card/90 backdrop-blur-md">
      <ul className="mx-auto flex max-w-md items-stretch justify-between px-2 pt-1.5">
        {tabs.map((t) => {
          const active = t.to === "/" ? pathname === "/" : pathname.startsWith(t.to);
          const Icon = t.icon;
          return (
            <li key={t.key} className="flex-1">
              <Link
                to={t.to}
                className={cn(
                  "flex flex-col items-center gap-0.5 rounded-lg px-2 py-1.5 text-[11px] font-medium transition-colors",
                  active ? "text-primary" : "text-muted-foreground hover:text-foreground",
                )}
              >
                <Icon className={cn("h-5 w-5", active && "stroke-[2.5]")} />
                <span>{labels[t.key] ?? t.key}</span>
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
