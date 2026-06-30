import { Link } from "@tanstack/react-router";
import { ChevronLeft } from "lucide-react";
import type { ReactNode } from "react";

export function SettingsHeader({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className="mb-3 flex items-center gap-2">
      <Link to="/settings" className="rounded-full p-2 hover:bg-muted" aria-label="Back">
        <ChevronLeft className="h-5 w-5" />
      </Link>
      <h1 className="flex-1 text-lg font-bold">{title}</h1>
      {children}
    </div>
  );
}
