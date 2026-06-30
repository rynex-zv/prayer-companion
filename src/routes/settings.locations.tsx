import { createFileRoute } from "@tanstack/react-router";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";
import { Card } from "@/components/Card";
import { Field } from "@/components/Field";
import { Picker } from "@/components/Picker";
import { Toggle } from "@/components/Toggle";
import { SettingsHeader } from "@/components/SettingsHeader";
import { AlertTriangle } from "lucide-react";

export const Route = createFileRoute("/settings/locations")({
  component: LocationsPage,
});

type Loc = {
  useGps: boolean; latitude: number; longitude: number;
  country: string; city: string; vpnWarning: boolean;
  countries: { code: string; name: string; cities: string[] }[];
};

function LocationsPage() {
  const { data, refresh } = useSnapshot<Loc>("settings.getSnapshot", { section: "locations" });
  if (!data) return null;
  const cities = data.countries.find((c) => c.code === data.country)?.cities ?? [];
  const patch = (p: Partial<Loc>) => mauiCall("settings.patch", { locations: { ...data, ...p } }).then(refresh);

  return (
    <div>
      <SettingsHeader title="Locations" />
      <div className="flex flex-col gap-3">
        {data.vpnWarning && (
          <Card className="flex items-start gap-2 border-warning/40 bg-warning/10">
            <AlertTriangle className="h-4 w-4 text-warning" />
            <p className="text-xs">VPN detected — location may be inaccurate.</p>
          </Card>
        )}

        <Card>
          <Toggle checked={data.useGps} onChange={(v) => patch({ useGps: v })} label="Use GPS" />
          <button
            onClick={() => mauiCall("settings.invoke", { action: "refreshGps" }).then(refresh)}
            className="mt-3 w-full rounded-md border border-border bg-card px-3 py-2 text-sm font-medium hover:bg-muted"
          >
            Refresh GPS
          </button>
        </Card>

        <Card className="space-y-3">
          <Field label="Country">
            <Picker value={data.country} onChange={(v) => patch({ country: v, city: data.countries.find((c) => c.code === v)?.cities[0] ?? "" })}>
              {data.countries.map((c) => <option key={c.code} value={c.code}>{c.name}</option>)}
            </Picker>
          </Field>
          <Field label="City">
            <Picker value={data.city} onChange={(v) => patch({ city: v })}>
              {cities.map((c) => <option key={c} value={c}>{c}</option>)}
            </Picker>
          </Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label="Latitude">
              <input type="number" defaultValue={data.latitude} step="0.0001"
                onBlur={(e) => patch({ latitude: Number(e.target.value) })}
                className="rounded-lg border border-input bg-card px-3 py-2 text-sm" />
            </Field>
            <Field label="Longitude">
              <input type="number" defaultValue={data.longitude} step="0.0001"
                onBlur={(e) => patch({ longitude: Number(e.target.value) })}
                className="rounded-lg border border-input bg-card px-3 py-2 text-sm" />
            </Field>
          </div>
        </Card>
      </div>
    </div>
  );
}
