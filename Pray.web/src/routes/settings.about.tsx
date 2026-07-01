import { createFileRoute } from "@tanstack/react-router";
import { Card } from "@/components/Card";
import { SettingsHeader } from "@/components/SettingsHeader";
import { mauiCall } from "@/native/mauiWebberClient";
import { Mail, Phone, Globe, Bug } from "lucide-react";
import { usePageLog } from "@/hooks/usePageLog";

export const Route = createFileRoute("/settings/about")({
  component: AboutPage,
});

function AboutPage() {
  usePageLog("settings.about");
  const info = {
    name: "Pray Ad Free",
    tagline: "Prayer times, Qibla, and tasbih — ad free.",
    privacy: "We don't collect personal data. Everything stays on your device.",
    source: "Open source on GitHub.",
    maintainer: "Rynex",
    contact: "Support and feedback",
    email: "support@rynex.nl",
    phone: "+31 00 000 0000",
    website: "https://pray.rynex.nl",
    websiteNote: "Visit for updates and web version.",
  };

  const action = (a: string, p?: unknown) => mauiCall("settings.invoke", { action: a, payload: p });

  return (
    <div>
      <SettingsHeader title="About" />
      <div className="flex flex-col gap-3">
        <Card className="text-center">
          <div className="text-2xl font-bold">{info.name}</div>
          <p className="mt-1 text-sm text-muted-foreground">{info.tagline}</p>
        </Card>
        <Card className="space-y-2 text-sm">
          <p>{info.privacy}</p>
          <p>{info.source}</p>
          <p className="text-muted-foreground">Maintained by <span className="font-medium text-foreground">{info.maintainer}</span></p>
        </Card>
        <Card className="space-y-2">
          <div className="text-sm font-semibold">{info.contact}</div>
          <div className="space-y-1 text-sm">
            <div className="flex items-center gap-2"><Mail className="h-4 w-4 text-primary" />{info.email}</div>
            <div className="flex items-center gap-2"><Phone className="h-4 w-4 text-primary" />{info.phone}</div>
            <div className="flex items-center gap-2"><Globe className="h-4 w-4 text-primary" />{info.website}</div>
            <p className="text-xs text-muted-foreground">{info.websiteNote}</p>
          </div>
        </Card>
        <div className="grid grid-cols-2 gap-2">
          <button onClick={() => action("openEmail", { to: info.email })} className="flex items-center justify-center gap-2 rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground"><Mail className="h-4 w-4" /> Email</button>
          <button onClick={() => action("call", { number: info.phone })} className="flex items-center justify-center gap-2 rounded-md bg-secondary px-3 py-2 text-sm font-medium"><Phone className="h-4 w-4" /> Call</button>
          <button onClick={() => action("openUrl", { url: info.website })} className="flex items-center justify-center gap-2 rounded-md bg-secondary px-3 py-2 text-sm font-medium"><Globe className="h-4 w-4" /> Website</button>
          <button onClick={() => action("reportIssue")} className="flex items-center justify-center gap-2 rounded-md bg-secondary px-3 py-2 text-sm font-medium"><Bug className="h-4 w-4" /> Report</button>
        </div>
      </div>
    </div>
  );
}
