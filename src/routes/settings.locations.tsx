import { createFileRoute } from "@tanstack/react-router";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall, mauiTrace } from "@/native/mauiWebberClient";
import { Card } from "@/components/Card";
import { Field } from "@/components/Field";
import { Picker } from "@/components/Picker";
import { Toggle } from "@/components/Toggle";
import { SettingsHeader } from "@/components/SettingsHeader";
import { AlertTriangle, RefreshCw } from "lucide-react";
import { usePageLog } from "@/hooks/usePageLog";
import { useAppLabels } from "@/hooks/useAppLabels";
import { useEffect } from "react";

export const Route = createFileRoute("/settings/locations")({
  component: LocationsPage,
});

type Loc = {
  useGps: boolean; latitude: number; longitude: number;
  country: string; city: string; vpnWarning: boolean;
  countries: { code: string; name: string; cities: string[] }[];
};

const fallbackCountries: Loc["countries"] = [
  { code: "NL", name: "Netherlands", cities: ["Amsterdam", "Rotterdam", "Utrecht"] },
  { code: "SA", name: "Saudi Arabia", cities: ["Makkah", "Madinah", "Riyadh"] },
  { code: "TR", name: "Turkey", cities: ["Istanbul", "Ankara"] },
  { code: "US", name: "United States", cities: ["New York", "Chicago", "Dearborn"] },
];

function normalizeLocation(data: Partial<Loc>): Loc {
  const countries = Array.isArray(data.countries) && data.countries.length > 0 ? data.countries : fallbackCountries;
  const fallbackCountry = countries[0];
  const country = countries.some((item) => item.code === data.country) ? data.country! : fallbackCountry.code;
  const cities = countries.find((item) => item.code === country)?.cities ?? fallbackCountry.cities;
  const city = cities.includes(data.city ?? "") ? data.city! : cities[0] ?? "";
  return {
    useGps: Boolean(data.useGps),
    latitude: Number.isFinite(data.latitude) ? data.latitude! : 0,
    longitude: Number.isFinite(data.longitude) ? data.longitude! : 0,
    country,
    city,
    vpnWarning: Boolean(data.vpnWarning),
    countries,
  };
}

function LocationsPage() {
  usePageLog("settings.locations");
  const t = useAppLabels();
  const { data, error, loading, refresh, setData } = useSnapshot<Loc>("settings.getSnapshot", { section: "locations" });
  useEffect(() => {
    mauiTrace("locations.branch", {
      hasData: data != null,
      loading,
      hasError: error != null,
      textLength: document.body.innerText.length,
      country: data?.country,
      city: data?.city,
    });
  }, [data, error, loading]);
  if (!data) {
    return (
      <div>
        <SettingsHeader title={t("locations", "Locations")} logPage="settings.locations" />
        <Card className="min-h-32">
          {error ? (
            <div className="space-y-3">
              <div className="flex items-start gap-2 text-sm text-destructive">
                <AlertTriangle className="mt-0.5 h-4 w-4" />
                <span>{error}</span>
              </div>
              <button
                onClick={refresh}
                className="inline-flex items-center gap-2 rounded-md border border-border px-3 py-2 text-sm font-medium hover:bg-muted"
              >
                <RefreshCw className="h-4 w-4" />
                {t("refreshGps", "Refresh GPS")}
              </button>
            </div>
          ) : (
            <div className="space-y-3">
              <div className="h-5 w-32 animate-pulse rounded bg-muted" />
              <div className="h-10 animate-pulse rounded bg-muted" />
              <div className="h-10 animate-pulse rounded bg-muted" />
            </div>
          )}
        </Card>
      </div>
    );
  }
  const location = normalizeLocation(data);
  const cities = location.countries.find((c) => c.code === location.country)?.cities ?? [];
  const patch = (p: Partial<Loc>) => {
    const next = normalizeLocation({ ...location, ...p });
    setData(next);
    return mauiCall("settings.patch", { locations: next });
  };

  return (
    <div>
      <SettingsHeader title={t("locations", "Locations")} />
      <div className="flex flex-col gap-3">
        {location.vpnWarning && (
          <Card className="flex items-start gap-2 border-warning/40 bg-warning/10">
            <AlertTriangle className="h-4 w-4 text-warning" />
            <p className="text-xs">{t("vpnWarning", "VPN detected - location may be inaccurate.")}</p>
          </Card>
        )}

        <Card>
          <Toggle
            checked={location.useGps}
            onChange={(v) => patch({ useGps: v })}
            label={t("useGps", "Use GPS")}
            selectorName="locations:gps-toggle"
          />
          <button
            onClick={async () => {
              await mauiCall("settings.invoke", { action: "refreshGps" });
              const res = await mauiCall<Loc>("settings.getSnapshot", { section: "locations" });
              if (res.ok) setData(normalizeLocation(res.data));
            }}
            disabled={loading}
            data-selector-name="locations:refresh-gps"
            className="mt-3 w-full rounded-md border border-border bg-card px-3 py-2 text-sm font-medium text-card-foreground hover:bg-muted"
          >
            {t("refreshGps", "Refresh GPS")}
          </button>
        </Card>

        <Card className="space-y-3">
          <Field label={t("country", "Country")}>
            <Picker value={location.country} selectorName="locations:country" onChange={(v) => patch({ country: v, city: location.countries.find((c) => c.code === v)?.cities[0] ?? "" })}>
              {location.countries.map((c) => <option key={c.code} value={c.code}>{c.name}</option>)}
            </Picker>
          </Field>
          <Field label={t("city", "City")}>
            <Picker value={location.city} selectorName="locations:city" onChange={(v) => patch({ city: v })}>
              {cities.map((c) => <option key={c} value={c}>{c}</option>)}
            </Picker>
          </Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label={t("latitude", "Latitude")}>
              <input type="number" defaultValue={location.latitude} step="0.0001"
                onBlur={(e) => patch({ latitude: Number(e.target.value) })}
                data-selector-name="locations:latitude"
                className="rounded-lg border border-input bg-card px-3 py-2 text-sm text-card-foreground" />
            </Field>
            <Field label={t("longitude", "Longitude")}>
              <input type="number" defaultValue={location.longitude} step="0.0001"
                onBlur={(e) => patch({ longitude: Number(e.target.value) })}
                data-selector-name="locations:longitude"
                className="rounded-lg border border-input bg-card px-3 py-2 text-sm text-card-foreground" />
            </Field>
          </div>
        </Card>
      </div>
    </div>
  );
}
