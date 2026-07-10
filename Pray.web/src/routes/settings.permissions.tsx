import { createFileRoute } from "@tanstack/react-router";
import { SettingsHeader } from "@/components/SettingsHeader";
import { SectionBlock } from "@/components/SettingsFormControls";
import { useAppLabels } from "@/hooks/useAppLabels";
import { useStoredSnapshot } from "@/hooks/useStoredSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";

export const Route = createFileRoute("/settings/permissions")({
  component: PermissionsPage,
});

type PermissionsSnapshot = {
  alarmMode: { title: string; status: string; description: string };
  items: { id: string; title: string; role: string; description: string; fallback: string; status: string; action: string }[];
};

function PermissionsPage() {
  const t = useAppLabels();
  const { data, refresh } = useStoredSnapshot<PermissionsSnapshot>("settings.getSnapshot", { section: "permissions" }, "settings.permissions");
  if (!data) return null;

  const request = (id: string) => {
    void mauiCall("settings.invoke", { action: "requestPermission", payload: { id } }).then(() => refresh(true));
  };

  return (
    <div data-selector-name="permissions:page" className="flex flex-col gap-3">
      <SettingsHeader title={t("permissions")} />
      <SectionBlock title={data.alarmMode.title}>
        <div data-selector-name="permissions:alarm-mode" className="text-sm text-card-foreground">
          <div className="font-semibold">{data.alarmMode.status}</div>
          <div className="mt-1 text-xs text-muted-foreground">{data.alarmMode.description}</div>
        </div>
      </SectionBlock>

      <SectionBlock title={t("systemPermissions")}>
        {data.items.map((permission) => (
          <div key={permission.id} className="rounded-md border border-border bg-background p-3 text-sm text-card-foreground">
            <div className="flex items-start justify-between gap-3">
              <div>
                <div className="font-semibold">{permission.title}</div>
                <div className="mt-1 text-xs text-muted-foreground">{permission.description}</div>
                {permission.fallback ? <div className="mt-1 text-xs text-muted-foreground">{permission.fallback}</div> : null}
                <div data-selector-name={`permissions:status:${permission.id}`} className="mt-2 text-xs font-medium text-primary">
                  {permission.status}
                </div>
              </div>
              <button
                type="button"
                onClick={() => request(permission.id)}
                data-selector-name={`permissions:request:${permission.id}`}
                className="shrink-0 rounded-md bg-primary px-3 py-2 text-xs font-medium text-primary-foreground"
              >
                {permission.action}
              </button>
            </div>
          </div>
        ))}
      </SectionBlock>
    </div>
  );
}
