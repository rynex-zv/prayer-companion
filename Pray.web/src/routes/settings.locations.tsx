import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { CoordinateInput } from "@/components/CoordinateInput";
import { SettingsHeader } from "@/components/SettingsHeader";
import { OptionButtons, SectionBlock, StatusLine, ToggleSetting } from "@/components/SettingsFormControls";
import { useAppLabels } from "@/hooks/useAppLabels";
import { useProjection } from "@/hooks/useProjection";
import { platformIntents, updateSettingsSection } from "@/client/applicationClient";

export const Route = createFileRoute("/settings/locations")({
  component: LocationsPage,
});

type Country = { code: string; name: string; cities: string[] };
type Place = { country: string; countryCode: string; city: string; latitude: number; longitude: number };
type LocationSettings = {
  useGps: boolean;
  latitude: number;
  longitude: number;
  country: string;
  countryName?: string;
  city: string;
  vpnWarning: boolean;
  qiblaReadingMode: string;
  qiblaFilterMode: string;
  qiblaReadingModes: { id: string; label: string }[];
  qiblaFilterModes: { id: string; label: string }[];
  countries: Country[];
  places?: Place[];
};
type LocationConfirmation = { value: LocationSettings; calculated?: LocationSettings };

function LocationsPage() {
  const t = useAppLabels();
  const { data, setData, refresh } = useProjection<LocationSettings>("settings.getSnapshot", { section: "locations" }, "settings.locations");
  const [status, setStatus] = useState("ready");
  const [gpsMessage, setGpsMessage] = useState("");
  const [isRefreshingGps, setIsRefreshingGps] = useState(false);
  if (!data) return null;

  const country = data.countries.find((item) => item.code === data.country);
  const places = data.places ?? [];
  const countryOptions = data.countries.map((item) => ({ id: item.code, label: item.name }));
  if (!data.country) countryOptions.unshift({ id: "", label: "—" });
  if (data.country && !countryOptions.some((item) => item.id === data.country)) {
    countryOptions.push({ id: data.country, label: data.countryName || data.country });
  }
  const cityOptions = (country?.cities ?? []).map((city) => ({ id: city, label: city }));
  if (!data.city) cityOptions.unshift({ id: "", label: "—" });
  if (data.city && !cityOptions.some((item) => item.id === data.city)) {
    cityOptions.push({ id: data.city, label: data.city });
  }
  const patch = async (next: LocationSettings, resolveCoordinates = false) => {
    setData(next);
    setStatus("saving");
    const response = await updateSettingsSection<LocationConfirmation, LocationSettings>("locations", next);
    if (!response.ok) {
      setStatus("error");
      return false;
    }

    const confirmed = response.data.calculated ?? next;
    setData(confirmed);
    if (resolveCoordinates) {
      const resolved = await platformIntents.reverseGeocode<LocationSettings>(next.latitude, next.longitude);
      if (resolved.ok) {
        setData(resolved.data);
      }
    }
    setStatus("saved");
    return true;
  };
  const patchCountry = (code: string) => {
    const nextCountry = data.countries.find((item) => item.code === code);
    const firstCity = nextCountry?.cities[0] ?? "";
    const place = places.find((item) =>
      item.countryCode.toLowerCase() === code.toLowerCase() &&
      item.city.toLowerCase() === firstCity.toLowerCase());
    void patch({
      ...data,
      useGps: false,
      country: code,
      countryName: nextCountry?.name ?? place?.country ?? data.countryName,
      city: firstCity,
      latitude: place?.latitude ?? data.latitude,
      longitude: place?.longitude ?? data.longitude,
    });
  };
  const patchCity = (city: string) => {
    const place = places.find((item) =>
      item.countryCode.toLowerCase() === data.country.toLowerCase() &&
      item.city.toLowerCase() === city.toLowerCase());
    void patch({
      ...data,
      useGps: false,
      countryName: place?.country ?? data.countryName,
      city,
      latitude: place?.latitude ?? data.latitude,
      longitude: place?.longitude ?? data.longitude,
    });
  };
  const refreshGps = () => {
    if (isRefreshingGps) return;
    setIsRefreshingGps(true);
    setGpsMessage("");
    setStatus("refreshing");
    void platformIntents.refreshLocation<LocationSettings | { location?: LocationSettings }>().then((res) => {
      setStatus(res.ok ? "saved" : "error");
      if (!res.ok) {
        setGpsMessage(res.error);
      } else {
        const refreshed = "useGps" in res.data ? res.data : res.data.location;
        if (refreshed) setData(refreshed);
      }
      return refresh(true);
    }).catch((error) => {
      setStatus("error");
      setGpsMessage(error instanceof Error ? error.message : t("status_error"));
    }).finally(() => {
      setIsRefreshingGps(false);
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
        <ToggleSetting label={t("useGps")} checked={data.useGps} onChange={(useGps) => void patch({ ...data, useGps })} selectorName="locations:gps-toggle" onLabel={t("enabled")} offLabel={t("disabled")} />
        <button type="button" onClick={refreshGps} disabled={isRefreshingGps} data-selector-name="locations:refresh-gps" className="rounded-md border border-border bg-card px-3 py-2 text-sm font-medium text-card-foreground disabled:opacity-60">
          {isRefreshingGps ? t("status_refreshing") : t("refreshGps")}
        </button>
        {gpsMessage ? <div className="text-xs text-muted-foreground" data-selector-name="locations:gps-message">{gpsMessage}</div> : null}
      </SectionBlock>

      <SectionBlock title={t("locations")}>
        <OptionButtons
          label={t("country")}
          value={data.country}
          selectorName="locations:country"
          options={countryOptions}
          onChange={patchCountry}
        />
        {country || data.city ? (
          <OptionButtons
            label={t("city")}
            value={data.city}
            selectorName="locations:city"
            options={cityOptions}
            onChange={patchCity}
          />
        ) : null}
        <div className="grid grid-cols-2 gap-3">
          <CoordinateInput label={t("latitude")} selectorName="locations:latitude" value={data.latitude} onCommit={(latitude) => void patch({ ...data, useGps: false, latitude }, true)} />
          <CoordinateInput label={t("longitude")} selectorName="locations:longitude" value={data.longitude} onCommit={(longitude) => void patch({ ...data, useGps: false, longitude }, true)} />
        </div>
      </SectionBlock>

      <SectionBlock title={t("qiblaPreferences")}>
        <OptionButtons
          label={t("compassReadingMode")}
          value={data.qiblaReadingMode}
          selectorName="locations:qibla-reading-mode"
          options={(data.qiblaReadingModes ?? []).map((item) => ({ id: item.id, label: item.label }))}
          onChange={(qiblaReadingMode) => void patch({ ...data, qiblaReadingMode })}
        />
        <OptionButtons
          label={t("compassFilter")}
          value={data.qiblaFilterMode}
          selectorName="locations:qibla-filter-mode"
          options={(data.qiblaFilterModes ?? []).map((item) => ({ id: item.id, label: item.label }))}
          onChange={(qiblaFilterMode) => void patch({ ...data, qiblaFilterMode })}
        />
      </SectionBlock>
    </div>
  );
}
