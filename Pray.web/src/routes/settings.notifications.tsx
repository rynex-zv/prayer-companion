import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { SettingsHeader } from "@/components/SettingsHeader";
import { EditableSetting, OptionButtons, SectionBlock, StatusLine, ToggleSetting } from "@/components/SettingsFormControls";
import { useAppLabels } from "@/hooks/useAppLabels";
import { useProjection } from "@/hooks/useProjection";
import { patchSettingsSection, platformIntents } from "@/client/applicationClient";

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
  reminderScope: string;
  reminderPrayer: string;
  reminderScopes: Option[];
  reminderPrayers: Option[];
  reminderAlertTypes: Option[];
  reminderUnits: Option[];
  reminderDirections: Option[];
  reminders: Reminder[];
  pendingDeferredReminder?: { prayer: string; notifyTime: string; openAlarmScreen: boolean; label: string } | null;
};

type Option = { id: string; label: string };
type Reminder = { id?: string; value: number; unit: string; direction: string; alertType: string; label?: string };

const reminderTypes = [
  { id: "Alarm", labelKey: "reminderType_Alarm" },
  { id: "AdhanNotification", labelKey: "reminderType_Adhan" },
];
const vibrationStrengths = ["Light", "Medium", "Strong"];
const vibrationPatterns = ["Default", "Pulse", "Heartbeat"];

function NotificationsPage() {
  const t = useAppLabels();
  const { data, setData } = useProjection<NotificationSettings>("settings.getSnapshot", { section: "notifications" }, "settings.notifications");
  const [status, setStatus] = useState("ready");
  if (!data) return null;

  const patch = (next: NotificationSettings) => {
    setData(next);
    setStatus("saving");
    void patchSettingsSection("notifications", next).then((response) => {
      if (!response.ok) return setStatus("error");
      setData(response.data.projection);
      setStatus("saved");
    });
  };

  const invoke = (action: "testAlarm" | "testNotification") => {
    setStatus(action);
    const command = action === "testAlarm" ? platformIntents.testAlarm() : platformIntents.testNotification();
    void command.then((res) => setStatus(res.ok ? "ready" : "error"));
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
          options={reminderTypes.map((item) => ({ id: item.id, label: t(item.labelKey) }))}
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
        <p className="text-xs text-muted-foreground">{t("windowsBackgroundServiceHint")}</p>
      </SectionBlock>

      <SectionBlock title={t("adhanReminders")}>
        {data.pendingDeferredReminder ? (
          <div className="rounded-md border border-primary/30 bg-primary/10 p-3 text-sm" data-selector-name="notifications:pending-deferred">
            <div className="font-semibold">{t("pendingDeferredReminder")}</div>
            <div className="mt-1 text-xs text-muted-foreground">{data.pendingDeferredReminder.label}</div>
          </div>
        ) : null}
        <OptionButtons
          label={t("scope")}
          value={data.reminderScope}
          selectorName="notifications:reminder-scope"
          options={data.reminderScopes ?? [{ id: "All", label: t("reminder_All") }, { id: "SpecificPrayer", label: t("reminder_SpecificPrayer") }]}
          onChange={(reminderScope) => patch({ ...data, reminderScope })}
        />
        <OptionButtons
          label={t("prayer")}
          value={data.reminderPrayer}
          selectorName="notifications:reminder-prayer"
          options={data.reminderPrayers ?? []}
          onChange={(reminderPrayer) => patch({ ...data, reminderPrayer })}
        />
        <button
          type="button"
          onClick={() => patch({ ...data, reminders: [...(data.reminders ?? []), { value: 10, unit: "minute", direction: "before", alertType: "Adhan" }] })}
          data-selector-name="notifications:reminder-add"
          className="rounded-md border border-border bg-card px-3 py-2 text-sm font-medium"
        >
          {t("add")}
        </button>
        <div className="space-y-2">
          {(data.reminders ?? []).map((reminder, index) => (
            <div key={reminder.id ?? index} className="rounded-md border border-border bg-background p-3">
              <div className="grid grid-cols-2 gap-2">
                <EditableSetting
                  label={t("newReminderText")}
                  selectorName={`notifications:reminder-value:${index}`}
                  value={reminder.value}
                  onChange={(value) => patch({ ...data, reminders: data.reminders.map((item, i) => i === index ? { ...item, value: Number(value) || 0 } : item) })}
                />
                <OptionButtons
                  label={t("unit")}
                  value={reminder.unit}
                  selectorName={`notifications:reminder-unit:${index}`}
                  options={data.reminderUnits ?? [{ id: "minute", label: t("minutes") }, { id: "hour", label: t("hours") }]}
                  onChange={(unit) => patch({ ...data, reminders: data.reminders.map((item, i) => i === index ? { ...item, unit } : item) })}
                />
                <OptionButtons
                  label={t("direction")}
                  value={reminder.direction}
                  selectorName={`notifications:reminder-direction:${index}`}
                  options={data.reminderDirections ?? [{ id: "before", label: t("before") }, { id: "after", label: t("after") }]}
                  onChange={(direction) => patch({ ...data, reminders: data.reminders.map((item, i) => i === index ? { ...item, direction } : item) })}
                />
                <OptionButtons
                  label={t("alertType")}
                  value={reminder.alertType}
                  selectorName={`notifications:reminder-alert:${index}`}
                  options={data.reminderAlertTypes ?? [{ id: "Adhan", label: t("reminderType_Adhan") }, { id: "Notification", label: t("reminderType_Notification") }, { id: "Silent", label: t("reminderType_Silent") }]}
                  onChange={(alertType) => patch({ ...data, reminders: data.reminders.map((item, i) => i === index ? { ...item, alertType } : item) })}
                />
              </div>
              <button
                type="button"
                onClick={() => patch({ ...data, reminders: data.reminders.filter((_, i) => i !== index) })}
                data-selector-name={`notifications:reminder-remove:${index}`}
                className="mt-2 rounded-md border border-border bg-card px-3 py-2 text-xs font-medium"
              >
                {t("remove")}
              </button>
            </div>
          ))}
        </div>
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
