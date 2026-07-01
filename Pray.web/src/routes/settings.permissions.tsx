import { createFileRoute } from "@tanstack/react-router";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";
import { Card } from "@/components/Card";
import { SettingsHeader } from "@/components/SettingsHeader";
import { cn } from "@/lib/utils";
import { usePageLog } from "@/hooks/usePageLog";

export const Route = createFileRoute("/settings/permissions")({
  component: PermissionsPage,
});

type Perm = {
  alarmMode: { title: string; status: string; description: string };
  items: { id: string; title: string; role: string; description: string; fallback: string; status: string; action: string }[];
};

function PermissionsPage() {
  usePageLog("settings.permissions");
  const { data, refresh } = useSnapshot<Perm>("settings.getSnapshot", { section: "permissions" });
  if (!data) return null;

  return (
    <div>
      <SettingsHeader title="Permissions" />
      <div className="flex flex-col gap-3">
        <Card>
          <div className="text-xs uppercase tracking-wider text-muted-foreground">{data.alarmMode.title}</div>
          <div className="mt-1 text-sm font-semibold">{data.alarmMode.status}</div>
          <div className="mt-1 text-xs text-muted-foreground">{data.alarmMode.description}</div>
        </Card>

        {data.items.map((p) => {
          const granted = p.status === "Granted";
          return (
            <Card key={p.id}>
              <div className="flex items-start justify-between gap-2">
                <div>
                  <div className="flex items-center gap-2">
                    <div className="text-sm font-semibold">{p.title}</div>
                    <span className={cn("rounded-full px-2 py-0.5 text-[10px] font-medium",
                      p.role === "critical" ? "bg-destructive/10 text-destructive" : "bg-muted text-muted-foreground")}>
                      {p.role}
                    </span>
                  </div>
                  <p className="mt-1 text-xs text-muted-foreground">{p.description}</p>
                  <p className="mt-1 text-xs text-muted-foreground">Fallback: {p.fallback}</p>
                </div>
                <div className={cn("text-xs font-semibold", granted ? "text-success" : "text-warning")}>{p.status}</div>
              </div>
              {!granted && (
                <button onClick={() => mauiCall("settings.invoke", { action: "requestPermission", payload: { id: p.id } }).then(refresh)}
                  className="mt-3 w-full rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground">
                  {p.action}
                </button>
              )}
            </Card>
          );
        })}
      </div>
    </div>
  );
}
