import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { SettingsHeader } from "@/components/SettingsHeader";
import { EditableSetting, OptionButtons, SectionBlock, StatusLine, ToggleSetting } from "@/components/SettingsFormControls";
import { useAppLabels } from "@/hooks/useAppLabels";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";

export const Route = createFileRoute("/settings/notifications")({
  component: NotificationsPage,
});

type NotificationSettings = {
  enableAdhan: boolean;
  mobilePrimaryAdhanType: string;
  hideOnCloseWindows: boolean;
  runBackgroundServiceWindows: boolean;
  vibration: boolean;
  vibrationStrength: string;
  vibrationPattern: string;
  minutesBefore: number;
  reminders: unknown[];
};

const reminderTypes = ["Full", "Notification", "Silent"];
const vibrationStrengths = ["Light", "Medium", "Strong"];
const vibrationPatterns = ["Default", "Pulse", "Heartbeat"];

function NotificationsPage() {
  const t = useAppLabels();
  const { data, setData } = useSnapshot<NotificationSettings>("settings.getSnapshot", { section: "notifications" });
  const [status, setStatus] = useState("ready");
  if (!data) return null;

  const patch = (next: NotificationSettings) => {
    setData(next);
    setStatus("saving");
    void mauiCall("settings.patch", { notifications: next }).then((res) => setStatus(res.ok ? "saved" : "error"));
  };

  const invoke = (action: string) => {
    setStatus(action);
    void mauiCall("settings.invoke", { action }).then((res) => setStatus(res.ok ? "ready" : "error"));
  };

  return (
    <div data-selector-name="notifications:page" className="flex flex-col gap-3">
      <SettingsHeader title={t("notifications")} />
      <StatusLine selectorName="notifications:status" value={t(`status_${status}`)} />

      <SectionBlock title={t("remindersAndVibration")}>
        <ToggleSetting label={t("enableAdhan")} checked={data.enableAdhan} onChange={(enableAdhan) => patch({ ...data, enableAdhan })} selectorName="notifications:enable-adhan" onLabel={t("enabled")} offLabel={t("disabled")} />
        <OptionButtons
          label={t("primaryAdhanType")}
          value={data.mobilePrimaryAdhanType}
          selectorName="notifications:primary-type"
          options={reminderTypes.map((id) => ({ id, label: t(`reminderType_${id}`) }))}
          onChange={(mobilePrimaryAdhanType) => patch({ ...data, mobilePrimaryAdhanType })}
        />
        <EditableSetting label={t("minutesBefore")} selectorName="notifications:minutes-before" value={data.minutesBefore} onChange={(value) => patch({ ...data, minutesBefore: Number(value) || 0 })} />
        <ToggleSetting label={t("vibration")} checked={data.vibration} onChange={(vibration) => patch({ ...data, vibration })} selectorName="notifications:vibration" onLabel={t("enabled")} offLabel={t("disabled")} />
        <OptionButtons
          label={t("vibrationStrength")}
          value={data.vibrationStrength}
          selectorName="notifications:vibration-strength"
          options={vibrationStrengths.map((id) => ({ id, label: t(`vibration_${id}`) }))}
          onChange={(vibrationStrength) => patch({ ...data, vibrationStrength })}
        />
        <OptionButtons
          label={t("vibrationPattern")}
          value={data.vibrationPattern}
          selectorName="notifications:vibration-pattern"
          options={vibrationPatterns.map((id) => ({ id, label: t(`vibration_${id}`) }))}
          onChange={(vibrationPattern) => patch({ ...data, vibrationPattern })}
        />
      </SectionBlock>

      <SectionBlock title={t("systemPermissions")}>
        <ToggleSetting label={t("hideOnCloseWindows")} checked={data.hideOnCloseWindows} onChange={(hideOnCloseWindows) => patch({ ...data, hideOnCloseWindows })} selectorName="notifications:hide-on-close" onLabel={t("enabled")} offLabel={t("disabled")} />
        <ToggleSetting label={t("runBackgroundWindows")} checked={data.runBackgroundServiceWindows} onChange={(runBackgroundServiceWindows) => patch({ ...data, runBackgroundServiceWindows })} selectorName="notifications:run-background" onLabel={t("enabled")} offLabel={t("disabled")} />
      </SectionBlock>

      <div className="grid grid-cols-2 gap-2">
        <button type="button" onClick={() => invoke("testNotification")} data-selector-name="notifications:test-notification" className="rounded-md border border-border bg-card px-3 py-2 text-sm">
          {t("testNotification")}
        </button>
        <button type="button" onClick={() => invoke("testAlarm")} data-selector-name="notifications:test-alarm" className="rounded-md border border-border bg-card px-3 py-2 text-sm">
          {t("testAlarm")}
        </button>
      </div>
    </div>
  );
}
