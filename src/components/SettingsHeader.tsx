import { useNavigate } from "@tanstack/react-router";
import { ChevronLeft } from "lucide-react";
import type { ReactNode } from "react";
import { PageLog } from "@/components/PageLog";
import { mauiCall } from "@/native/mauiWebberClient";

export function SettingsHeader({ title, logPage, children }: { title: string; logPage?: string; children?: ReactNode }) {
  const navigate = useNavigate();
  return (
    <div className="mb-3 flex items-center gap-2">
      <button
        type="button"
        onClick={() => {
          void navigate({ to: "/settings" });
          void mauiCall("app.navigate", { route: "/settings" });
        }}
        data-selector-name="settings:back"
        className="rounded-full p-2 hover:bg-muted"
        aria-label="Back"
      >
        <ChevronLeft className="h-5 w-5" />
      </button>
      <h1 className="flex-1 text-lg font-bold" data-selector-name="settings:title">{title}</h1>
      <PageLog page={logPage ?? `settings.${title.toLowerCase().replace(/[^a-z0-9]+/g, "-")}`} />
      {children}
    </div>
  );
}
