import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { SettingsHeader } from "@/components/SettingsHeader";
import { EditableSetting, SectionBlock, StatusLine, ToggleSetting } from "@/components/SettingsFormControls";
import { useAppLabels } from "@/hooks/useAppLabels";
import { useStoredSnapshot } from "@/hooks/useStoredSnapshot";
import { syncField } from "@/state/appStore";

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
  const { data, setData } = useStoredSnapshot<AlarmRemindersSettings>("settings.getSnapshot", { section: "alarmReminders" }, "settings.alarmReminders");
  const [status, setStatus] = useState("ready");
  const [newReminderText, setNewReminderText] = useState("");
  if (!data) return null;

  const patch = (next: AlarmRemindersSettings) => {
    setData(next);
    setStatus("saving");
    void syncField("alarmReminders", "value", next).then((ok) => setStatus(ok ? "saved" : "error"));
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
        <div className="grid grid-cols-[1fr_auto] gap-2">
          <input
            value={newReminderText}
            onChange={(event) => setNewReminderText(event.currentTarget.value)}
            placeholder={t("newReminder")}
            data-selector-name="alarms:new-reminder-text"
            className="min-h-9 rounded-md border border-input bg-card px-3 py-2 text-sm"
          />
          <button
            type="button"
            onClick={() => {
              const text = newReminderText.trim();
              if (!text) {
                return;
              }
              patch({ ...data, userReminders: [...data.userReminders, { id: `new-${Date.now()}`, text, enabled: true }], userRemindersEnabled: true });
              setNewReminderText("");
            }}
            data-selector-name="alarms:add-reminder-from-input"
            className="rounded-md border border-border bg-card px-3 py-2 text-sm font-medium text-card-foreground"
          >
            {t("add")}
          </button>
        </div>
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
            <button
              type="button"
              onClick={() => patch({ ...data, userReminders: data.userReminders.filter((entry) => entry.id !== item.id) })}
              data-selector-name={`alarms:remove:${item.id}`}
              className="mt-3 rounded-md border border-border bg-card px-3 py-2 text-xs font-medium text-card-foreground"
            >
              {t("remove")}
            </button>
          </div>
        ))}
      </SectionBlock>
    </div>
  );
}
