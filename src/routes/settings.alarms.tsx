import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { SettingsHeader } from "@/components/SettingsHeader";
import { EditableSetting, SectionBlock, StatusLine, ToggleSetting } from "@/components/SettingsFormControls";
import { useAppLabels } from "@/hooks/useAppLabels";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";

export const Route = createFileRoute("/settings/alarms")({
  component: AlarmsPage,
});

type Reminder = { id: string; text: string; enabled: boolean };
type AlarmRemindersSettings = {
  builtIn: Reminder[];
  userRemindersEnabled: boolean;
  userReminders: Reminder[];
};

function AlarmsPage() {
  const t = useAppLabels();
  const { data, setData } = useSnapshot<AlarmRemindersSettings>("settings.getSnapshot", { section: "alarmReminders" });
  const [status, setStatus] = useState("ready");
  if (!data) return null;

  const patch = (next: AlarmRemindersSettings) => {
    setData(next);
    setStatus("saving");
    void mauiCall("settings.patch", { alarmReminders: next }).then((res) => setStatus(res.ok ? "saved" : "error"));
  };

  return (
    <div data-selector-name="alarms:page" className="flex flex-col gap-3">
      <SettingsHeader title={t("alarmReminders")} />
      <StatusLine selectorName="alarms:status" value={t(`status_${status}`)} />

      <SectionBlock title={t("builtIn")}>
        {data.builtIn.map((item) => (
          <ToggleSetting
            key={item.id}
            label={item.text}
            checked={item.enabled}
            onChange={(enabled) => patch({ ...data, builtIn: data.builtIn.map((entry) => entry.id === item.id ? { ...entry, enabled } : entry) })}
            selectorName={`alarms:built-in:${item.id}`}
            onLabel={t("enabled")}
            offLabel={t("disabled")}
          />
        ))}
      </SectionBlock>

      <SectionBlock title={t("yourReminders")}>
        <ToggleSetting label={t("yourReminders")} checked={data.userRemindersEnabled} onChange={(userRemindersEnabled) => patch({ ...data, userRemindersEnabled })} selectorName="alarms:user-enabled" onLabel={t("enabled")} offLabel={t("disabled")} />
        {data.userReminders.map((item) => (
          <div key={item.id} className="rounded-md border border-border bg-background p-3">
            <ToggleSetting
              label={item.text}
              checked={item.enabled}
              onChange={(enabled) => patch({ ...data, userReminders: data.userReminders.map((entry) => entry.id === item.id ? { ...entry, enabled } : entry) })}
              selectorName={`alarms:user-toggle:${item.id}`}
              onLabel={t("enabled")}
              offLabel={t("disabled")}
            />
            <EditableSetting
              className="mt-3"
              label={t("newReminder")}
              selectorName={`alarms:reminder-text:${item.id}`}
              value={item.text}
              onChange={(text) => patch({ ...data, userReminders: data.userReminders.map((entry) => entry.id === item.id ? { ...entry, text } : entry) })}
            />
          </div>
        ))}
        <button
          type="button"
          onClick={() => patch({ ...data, userReminders: [...data.userReminders, { id: `new-${Date.now()}`, text: t("newReminder"), enabled: true }] })}
          data-selector-name="alarms:add-reminder"
          className="rounded-md border border-border bg-card px-3 py-2 text-sm font-medium text-card-foreground"
        >
          {t("addReminder")}
        </button>
      </SectionBlock>
    </div>
  );
}
