import { createFileRoute } from "@tanstack/react-router";
import { SettingsHeader } from "@/components/SettingsHeader";
import { SectionBlock } from "@/components/SettingsFormControls";
import { useAppLabels } from "@/hooks/useAppLabels";
import { useProjection } from "@/hooks/useProjection";
import { platformIntents } from "@/client/applicationClient";
import { nativeBackendReady } from "@/client/applicationClient";
import { watchBrowserPermissionChanges } from "@/native/webPlatformAdapter";
import { useEffect } from "react";

export const Route = createFileRoute("/settings/permissions")({
  component: PermissionsPage,
});

type PermissionsSnapshot = {
  alarmMode: { title: string; status: string; description: string };
  items: { id: string; title: string; role: string; description: string; fallback: string; status: string; action: string; isGranted?: boolean; permissionState?: string }[];
};

function PermissionsPage() {
  const t = useAppLabels();
  const { data, refresh } = useProjection<PermissionsSnapshot>("settings.getSnapshot", { section: "permissions" }, "settings.permissions");
  useEffect(() => {
    if (nativeBackendReady()) return;
    return watchBrowserPermissionChanges(() => { void refresh(true); }, false);
  }, [refresh]);
  if (!data) return null;
  const alarmMode = data.alarmMode ?? { title: t("permissions"), status: t("status_error"), description: "" };
  const permissions = Array.isArray(data.items) ? data.items : [];

  const request = (id: string) => {
    void platformIntents.requestPermission(id).then(() => refresh(true));
  };

  return (
    <div data-selector-name="permissions:page" className="flex flex-col gap-3">
      <SettingsHeader title={t("permissions")} />
      <SectionBlock title={alarmMode.title}>
        <div data-selector-name="permissions:alarm-mode" className="text-sm text-card-foreground">
          <div className="font-semibold">{alarmMode.status}</div>
          <div className="mt-1 text-xs text-muted-foreground">{alarmMode.description}</div>
        </div>
      </SectionBlock>

      <SectionBlock title={t("systemPermissions")}>
        {permissions.map((permission) => (
          <div key={permission.id} className="rounded-md border border-border bg-background p-3 text-sm text-card-foreground">
            <div className="flex items-start justify-between gap-3">
              <div>
                <div className="font-semibold">{permission.title}</div>
                <div className="mt-1 text-xs text-muted-foreground">{permission.description}</div>
                {permission.fallback ? <div className="mt-1 text-xs text-muted-foreground">{permission.fallback}</div> : null}
                <div data-selector-name={`permissions:status:${permission.id}`} className="mt-2 text-xs font-medium text-primary">
                  {permission.isGranted ? t("permissionGranted") : permission.permissionState === "denied" ? t("permissionDenied") : t("permissionNotGranted")}
                </div>
              </div>
              <button
                type="button"
                disabled={permission.isGranted}
                onClick={() => request(permission.id)}
                data-selector-name={`permissions:request:${permission.id}`}
                className="shrink-0 rounded-md bg-primary px-3 py-2 text-xs font-medium text-primary-foreground disabled:opacity-50"
              >
                {permission.isGranted ? t("permissionGranted") : permission.action}
              </button>
            </div>
          </div>
        ))}
        {permissions.length === 0 ? <div className="text-sm text-muted-foreground">{t("status_error")}</div> : null}
      </SectionBlock>
    </div>
  );
}
