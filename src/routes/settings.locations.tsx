import { createFileRoute } from "@tanstack/react-router";
import { mauiCall } from "@/native/mauiWebberClient";
import { SettingsHeader } from "@/components/SettingsHeader";
import { useState } from "react";

export const Route = createFileRoute("/settings/locations")({
  component: LocationsPage,
});

type Loc = {
  useGps: boolean;
  latitude: number;
  longitude: number;
  country: string;
  city: string;
  vpnWarning: boolean;
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
  const [data, setData] = useState<Loc>(() => normalizeLocation({
    useGps: true,
    country: "NL",
    city: "Amsterdam",
    latitude: 52.3896,
    longitude: 4.9123,
    countries: fallbackCountries,
  }));
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const location = normalizeLocation(data);
  const cities = location.countries.find((c) => c.code === location.country)?.cities ?? [];
  const [latitudeDraft, setLatitudeDraft] = useState(() => String(location.latitude));
  const [longitudeDraft, setLongitudeDraft] = useState(() => String(location.longitude));

  const refresh = async () => {
    setLoading(true);
    const res = await mauiCall<Loc>("settings.getSnapshot", { section: "locations" });
    if (res.ok) {
      setData(normalizeLocation(res.data));
      setError(null);
    } else {
      setError(res.error);
    }
    setLoading(false);
  };

  const patch = (p: Partial<Loc>) => {
    const next = normalizeLocation({ ...location, ...p });
    setData(next);
    return mauiCall("settings.patch", { locations: next });
  };

  return (
    <div>
      <SettingsHeader title="المواقع" />
      <div className="flex flex-col gap-3">
        <div className="rounded-md border border-border bg-card p-3 text-sm text-card-foreground">
          <div data-selector-name="locations:status">{loading ? "loading" : error ? "error" : "ready"}</div>
          {error ? (
            <button
              type="button"
              onClick={refresh}
              className="mt-2 rounded-md border border-border px-3 py-2 text-sm"
            >
              تحديث GPS
            </button>
          ) : null}
        </div>

        <label className="flex items-center justify-between rounded-md border border-border bg-card p-3 text-sm text-card-foreground">
          <span>استخدام GPS</span>
          <input
            type="checkbox"
            checked={location.useGps}
            onChange={(event) => void patch({ useGps: event.target.checked })}
            data-selector-name="locations:gps-toggle"
          />
        </label>

        <button
          onClick={async () => {
            await mauiCall("settings.invoke", { action: "refreshGps" });
            const res = await mauiCall<Loc>("settings.getSnapshot", { section: "locations" });
            if (res.ok) setData(normalizeLocation(res.data));
          }}
          disabled={loading}
          data-selector-name="locations:refresh-gps"
          className="rounded-md border border-border bg-card px-3 py-2 text-sm font-medium text-card-foreground disabled:opacity-50"
        >
          تحديث GPS
        </button>

        <label className="text-sm text-card-foreground">
          <span className="mb-1 block text-xs text-muted-foreground">الدولة</span>
          <select
            value={location.country}
            onChange={(event) => void patch({ country: event.target.value, city: location.countries.find((c) => c.code === event.target.value)?.cities[0] ?? "" })}
            data-selector-name="locations:country"
            className="w-full rounded-md border border-input bg-card px-3 py-2 text-sm text-card-foreground"
          >
            {location.countries.map((country) => <option key={country.code} value={country.code}>{country.name}</option>)}
          </select>
        </label>

        <label className="text-sm text-card-foreground">
          <span className="mb-1 block text-xs text-muted-foreground">المدينة</span>
          <select
            value={location.city}
            onChange={(event) => void patch({ city: event.target.value })}
            data-selector-name="locations:city"
            className="w-full rounded-md border border-input bg-card px-3 py-2 text-sm text-card-foreground"
          >
            {cities.map((city) => <option key={city} value={city}>{city}</option>)}
          </select>
        </label>

        <div className="grid grid-cols-2 gap-3">
          <label className="text-sm text-card-foreground">
            <span className="mb-1 block text-xs text-muted-foreground">خط العرض</span>
            <input
              type="number"
              value={latitudeDraft}
              step="0.0001"
              onChange={(event) => setLatitudeDraft(event.target.value)}
              onBlur={(event) => void patch({ latitude: Number(event.target.value) })}
              data-selector-name="locations:latitude"
              className="w-full rounded-md border border-input bg-card px-3 py-2 text-sm text-card-foreground"
            />
          </label>

          <label className="text-sm text-card-foreground">
            <span className="mb-1 block text-xs text-muted-foreground">خط الطول</span>
            <input
              type="number"
              value={longitudeDraft}
              step="0.0001"
              onChange={(event) => setLongitudeDraft(event.target.value)}
              onBlur={(event) => void patch({ longitude: Number(event.target.value) })}
              data-selector-name="locations:longitude"
              className="w-full rounded-md border border-input bg-card px-3 py-2 text-sm text-card-foreground"
            />
          </label>
        </div>
      </div>
    </div>
  );
}
