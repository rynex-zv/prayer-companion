import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { SettingsHeader } from "@/components/SettingsHeader";
import { EditableSetting, OptionButtons, SectionBlock, StatusLine, ToggleSetting } from "@/components/SettingsFormControls";
import { useAppLabels } from "@/hooks/useAppLabels";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";

export const Route = createFileRoute("/settings/locations")({
  component: LocationsPage,
});

type Country = { code: string; name: string; cities: string[] };
type LocationSettings = {
  useGps: boolean;
  latitude: number;
  longitude: number;
  country: string;
  city: string;
  vpnWarning: boolean;
  countries: Country[];
};

function locationLabelKey(prefix: string, value: string) {
  return `${prefix}_${value.replace(/[^A-Za-z0-9]+/g, "")}`;
}

function LocationsPage() {
  const t = useAppLabels();
  const { data, setData, refresh } = useSnapshot<LocationSettings>("settings.getSnapshot", { section: "locations" });
  const [status, setStatus] = useState("ready");
  if (!data) return null;

  const country = data.countries.find((item) => item.code === data.country) ?? data.countries[0];
  const patch = (next: LocationSettings) => {
    setData(next);
    setStatus("saving");
    void mauiCall("settings.patch", { locations: next }).then((res) => setStatus(res.ok ? "saved" : "error"));
  };
  const refreshGps = () => {
    setStatus("refreshing");
    void mauiCall("settings.invoke", { action: "refreshGps" }).then((res) => {
      setStatus(res.ok ? "saved" : "error");
      void refresh();
    });
  };

  return (
    <div data-selector-name="locations:page" className="flex flex-col gap-3">
      <SettingsHeader title={t("locations")} />
      <StatusLine selectorName="locations:status" value={t(`status_${status}`)} />
      {data.vpnWarning ? (
        <div data-selector-name="locations:vpn-warning" className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
          {t("vpnWarning")}
        </div>
      ) : null}

      <SectionBlock title={t("locationAndGps")}>
        <ToggleSetting label={t("useGps")} checked={data.useGps} onChange={(useGps) => patch({ ...data, useGps })} selectorName="locations:gps-toggle" onLabel={t("enabled")} offLabel={t("disabled")} />
        <button type="button" onClick={refreshGps} data-selector-name="locations:refresh-gps" className="rounded-md border border-border bg-card px-3 py-2 text-sm font-medium text-card-foreground">
          {t("refreshGps")}
        </button>
      </SectionBlock>

      <SectionBlock title={t("locations")}>
        <OptionButtons
          label={t("country")}
          value={data.country}
          selectorName="locations:country"
          options={data.countries.map((item) => ({ id: item.code, label: t(locationLabelKey("country", item.code)) }))}
          onChange={(code) => {
            const nextCountry = data.countries.find((item) => item.code === code);
            patch({ ...data, country: code, city: nextCountry?.cities[0] ?? data.city });
          }}
        />
        {country ? (
          <OptionButtons
            label={t("city")}
            value={data.city}
            selectorName="locations:city"
            options={country.cities.map((city) => ({ id: city, label: t(locationLabelKey("city", city)) }))}
            onChange={(city) => patch({ ...data, city })}
          />
        ) : null}
        <div className="grid grid-cols-2 gap-3">
          <EditableSetting label={t("latitude")} selectorName="locations:latitude" value={data.latitude} onChange={(value) => patch({ ...data, latitude: Number(value) || 0 })} />
          <EditableSetting label={t("longitude")} selectorName="locations:longitude" value={data.longitude} onChange={(value) => patch({ ...data, longitude: Number(value) || 0 })} />
        </div>
      </SectionBlock>
    </div>
  );
}
