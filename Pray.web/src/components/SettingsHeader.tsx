import { useNavigate } from "@tanstack/react-router";
import { ChevronLeft, ChevronRight } from "lucide-react";
import type { ReactNode } from "react";
import { PageLog } from "@/components/PageLog";
import { useAppLabels } from "@/hooks/useAppLabels";
import { useAppStore } from "@/state/appStore";

export function SettingsHeader({ title, logPage, children }: { title: string; logPage?: string; children?: ReactNode }) {
  const navigate = useNavigate();
  const direction = useAppStore((state) => state.direction);
  const t = useAppLabels();
  const BackIcon = direction === "rtl" ? ChevronRight : ChevronLeft;
  return (
    <div className="mb-3 flex items-center gap-2" dir={direction}>
      <button
        type="button"
        onClick={() => {
          void navigate({ to: "/settings" });
        }}
        data-selector-name="settings:back"
        className="rounded-full p-2 hover:bg-muted"
        aria-label={t("back")}
      >
        <BackIcon className="h-5 w-5" />
      </button>
      <h1 className="flex-1 text-lg font-bold" data-selector-name="settings:title">{title}</h1>
      <PageLog page={logPage ?? `settings.${title.toLowerCase().replace(/[^a-z0-9]+/g, "-")}`} />
      {children}
    </div>
  );
}
