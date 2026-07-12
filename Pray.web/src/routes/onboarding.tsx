import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { useProjection } from "@/hooks/useProjection";
import { executeCommand, platformIntents, updateSettingsSection } from "@/client/applicationClient";
import { Card } from "@/components/Card";
import { CoordinateInput } from "@/components/CoordinateInput";
import { Field } from "@/components/Field";
import { AlertTriangle, ChevronLeft, ChevronRight } from "lucide-react";
import { PageLog } from "@/components/PageLog";
import { useAppLabels } from "@/hooks/useAppLabels";
import { setLanguage, setOnboardingCompleted, useAppStore } from "@/state/appStore";

export const Route = createFileRoute("/onboarding")({
  head: () => ({
    meta: [
      { name: "robots", content: "noindex" },
    ],
  }),
  component: OnboardingPage,
});

type Snapshot = {
  completed?: boolean; steps?: string[]; language: string;
  languages?: { code: string; name: string }[];
  permissionsScenario?: string; vpnWarning?: boolean;
  canUseInternet?: boolean; canUseGps?: boolean;
  title?: string; subtitle?: string;
  permissions?: PermissionItem[] | { items?: PermissionItem[]; alarmMode?: PermissionItem };
  location?: LocationSettings;
};

type Country = { code: string; name: string; cities: string[] };
type Place = { country: string; countryCode: string; city: string; latitude: number; longitude: number };
type PermissionItem = {
  id?: string;
  isGranted?: boolean;
  title?: string;
  name?: string;
  description?: string;
  status?: string;
  action?: string;
};
type LocationSettings = {
  useGps?: boolean;
  latitude?: number;
  longitude?: number;
  country?: string;
  countryName?: string;
  city?: string;
  vpnWarning?: boolean;
  countries?: Country[];
  places?: Place[];
};
type LocationConfirmation = { value: LocationSettings; calculated?: LocationSettings };

function OnboardingPage() {
  const t = useAppLabels();
  const { data, refresh, setData } = useProjection<Snapshot>("onboarding.getSnapshot");
  const [step, setStep] = useState(0);
  const [finishError, setFinishError] = useState("");
  const navigate = useNavigate();
  const language = useAppStore((state) => state.language);
  const languages = useAppStore((state) => state.languages);
  const direction = useAppStore((state) => state.direction);
  if (!data) return null;

  const steps = data.steps?.length ? data.steps : [t("language"), t("permissions"), t("locationAndGps")];
  const cur = steps[step];
  const locationVpnWarning = data.vpnWarning ?? data.location?.vpnWarning ?? false;
  const languageOptions = languages.length ? languages : (data.languages ?? []);
  const selectedLanguage = language || data.language || "en";
  const permissionItems = Array.isArray(data.permissions) ? data.permissions : (data.permissions?.items ?? []);
  const permissionSummary = permissionItems.length
    ? `${permissionItems.filter((permission) => permission.isGranted === true).length} / ${permissionItems.length}`
    : t("permissionStatus");
  const locationPermission = permissionItems.find((permission) => permission.id?.toLowerCase() === "location");
  const locationPermissionGranted = locationPermission?.isGranted === true;
  const NextIcon = direction === "rtl" ? ChevronLeft : ChevronRight;
  const patchLocation = async (location: LocationSettings, resolveCoordinates = false) => {
    setData({ ...data, location });
    const response = await updateSettingsSection<LocationConfirmation, LocationSettings>("locations", location);
    if (!response.ok) {
      setFinishError(t("status_error"));
      return false;
    }

    const confirmed = response.data.calculated ?? location;
    setData({ ...data, location: confirmed });
    if (resolveCoordinates) {
      const resolved = await platformIntents.reverseGeocode<LocationSettings>(location.latitude!, location.longitude!);
      if (resolved.ok) {
        setData({ ...data, location: resolved.data });
      }
    }
    setFinishError("");
    return true;
  };
  const refreshGpsFromNative = async () => {
    const response = await platformIntents.refreshLocation<LocationSettings | { location?: LocationSettings }>();
    if (!response.ok) {
      setFinishError(response.error);
      return false;
    }

    const location = (response.data as { location?: LocationSettings }).location ?? response.data as LocationSettings;
    if (!hasUsableCoordinates(location?.latitude, location?.longitude)) {
      setFinishError(t("onboardingLocationInvalid"));
      return false;
    }
    setData({ ...data, location });
    setFinishError("");
    return true;
  };
  const requestLocationPermission = async () => {
    const response = await platformIntents.requestPermission("Location");
    if (!response.ok) {
      setFinishError(response.error);
      return false;
    }

    await refresh();
    return refreshGpsFromNative();
  };
  const refreshGps = async () => {
    if (!locationPermissionGranted) {
      await requestLocationPermission();
      return;
    }

    await refreshGpsFromNative();
  };
  const places = data.location?.places ?? [];
  const currentCountry = data.location?.countries?.find((item) => item.code === data.location?.country);
  const patchCountry = (countryCode: string) => {
    if (!data.location) return;
    const location = data.location;
    const country = location.countries?.find((item) => item.code === countryCode);
    const firstCity = country?.cities[0] ?? "";
    const place = places.find((item) =>
      item.countryCode.toLowerCase() === countryCode.toLowerCase() &&
      item.city.toLowerCase() === firstCity.toLowerCase());
    void patchLocation({
      ...location,
      useGps: false,
      country: countryCode,
      countryName: country?.name ?? place?.country ?? location.countryName,
      city: firstCity,
      latitude: place?.latitude ?? location.latitude,
      longitude: place?.longitude ?? location.longitude,
    });
  };
  const patchCity = (city: string) => {
    if (!data.location) return;
    const location = data.location;
    const place = places.find((item) =>
      item.countryCode.toLowerCase() === (location.country ?? "").toLowerCase() &&
      item.city.toLowerCase() === city.toLowerCase());
    void patchLocation({
      ...location,
      useGps: false,
      countryName: place?.country ?? location.countryName,
      city,
      latitude: place?.latitude ?? location.latitude,
      longitude: place?.longitude ?? location.longitude,
    });
  };
  return (
    <div className="flex h-[calc(100vh-7rem)] min-h-0 flex-col" dir={direction} data-selector-name="onboarding:page">
      <div className="mb-4 flex shrink-0 items-center gap-1" data-selector-name="onboarding:progress">
        {steps.map((_, i) => (
          <div key={i} data-selector-name={`onboarding:progress:${i}`} className={`h-1 flex-1 rounded-full ${i <= step ? "bg-primary" : "bg-muted"}`} />
        ))}
      </div>

      <Card className="min-h-0 flex-1 space-y-4 overflow-y-auto" data-selector-name="onboarding:card">
        <div className="text-xs uppercase tracking-wider text-muted-foreground" data-selector-name="onboarding:step-label">
          {t("stepProgress")} {step + 1} {t("of")} {steps.length}
        </div>
        <div className="flex items-center justify-between gap-2">
          <h1 className="text-2xl font-bold" data-selector-name="onboarding:title">{cur}</h1>
          <PageLog page="onboarding" />
        </div>

        {step === 0 && (
          <Field label={t("chooseLanguage")} data-selector-name="onboarding:language-field">
            <div className="grid grid-cols-2 gap-2" data-selector-name="onboarding:language-list">
              {languageOptions.map((l) => (
                <button
                  key={l.code}
                  type="button"
                  aria-checked={selectedLanguage === l.code}
                  onClick={() => void setLanguage(l.code).then(() => refresh())}
                  data-selector-name={`onboarding:language:${l.code}`}
                  className={`rounded-md border px-3 py-3 text-sm font-medium ${selectedLanguage === l.code ? "border-primary bg-primary text-primary-foreground" : "border-border bg-card"}`}
                >
                  {l.name}
                </button>
              ))}
            </div>
          </Field>
        )}

        {step === 1 && (
          <div className="space-y-2" data-selector-name="onboarding:permissions-step">
            <p className="text-sm text-muted-foreground" data-selector-name="onboarding:permissions-intro">{t("permissionsIntro")}</p>
            {permissionItems.length ? (
              <div className="space-y-2" data-selector-name="onboarding:permissions-list">
                {permissionItems.map((permission, index) => (
                  <div key={permission.id ?? index} className="rounded-md border border-border bg-background p-3" data-selector-name={`onboarding:permission:${permission.id ?? index}`}>
                    <div className="flex items-center justify-between gap-3">
                      <div>
                        <div className="text-sm font-semibold" data-selector-name={`onboarding:permission-title:${permission.id ?? index}`}>{permission.title ?? permission.name ?? t("permissions")}</div>
                        {permission.description ? <div className="mt-1 text-xs text-muted-foreground" data-selector-name={`onboarding:permission-description:${permission.id ?? index}`}>{permission.description}</div> : null}
                      </div>
                      {permission.status ? <span className="text-xs font-medium text-primary" data-selector-name={`onboarding:permission-status:${permission.id ?? index}`}>{permission.status}</span> : null}
                    </div>
                    <button
                      type="button"
                      onClick={() => platformIntents.requestPermission(permission.id ?? "").then(() => refresh())}
                      data-selector-name={`onboarding:permission-request:${permission.id ?? index}`}
                      className="mt-3 rounded-md border border-border bg-card px-3 py-2 text-xs font-medium"
                    >
                      {t("grantPermissions")}
                    </button>
                  </div>
                ))}
              </div>
            ) : (
              <div className="rounded-lg bg-muted p-3 text-sm" data-selector-name="onboarding:permissions-empty">
                <span data-selector-name="onboarding:permissions-empty-title">{t("permissionStatus")}</span>
                {data.permissionsScenario ? <span className="font-medium" data-selector-name="onboarding:permissions-empty-status">: {data.permissionsScenario}</span> : null}
              </div>
            )}
            <div className="rounded-lg bg-muted/60 p-3 text-sm" data-selector-name="onboarding:permissions-summary">
              {t("permissionStatus")}: <span className="font-medium">{permissionSummary}</span>
            </div>
            <button type="button" data-selector-name="onboarding:permissions-request-all" onClick={() => platformIntents.requestAllPermissions().then(() => refresh())} className="w-full rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground">{t("grantPermissions")}</button>
          </div>
        )}

        {step === 2 && (
          <div className="space-y-2 text-sm" data-selector-name="onboarding:location-step">
            {data.title || data.subtitle ? (
              <>
                {data.title && <p className="font-medium" data-selector-name="onboarding:location-title">{data.title}</p>}
                {data.subtitle && <p className="text-muted-foreground" data-selector-name="onboarding:location-subtitle">{data.subtitle}</p>}
              </>
            ) : data.canUseInternet === false && data.canUseGps === false ? (
              <p className="text-muted-foreground" data-selector-name="onboarding:location-message">{t("locationNoInternetGps")}</p>
            ) : data.canUseInternet ? (
              <p className="text-muted-foreground" data-selector-name="onboarding:location-message">{t("locationNetwork")}</p>
            ) : (
              <p className="text-muted-foreground" data-selector-name="onboarding:location-message">{t("locationGps")}</p>
            )}
            {locationVpnWarning && (
              <div className="flex items-start gap-2 rounded-md border border-warning/40 bg-warning/10 p-3 text-xs" data-selector-name="onboarding:location-vpn-warning">
                <AlertTriangle className="h-4 w-4 text-warning" />
                {t("vpnWarning")}
              </div>
            )}
            {data.location ? (
              <div className="space-y-3 rounded-md border border-border bg-background p-3" data-selector-name="onboarding:location-form">
                <div className="flex items-center justify-between gap-3">
                  <span data-selector-name="onboarding:location:gps-label">{t("useGps")}</span>
                  <button
                    type="button"
                    aria-checked={!!data.location.useGps && locationPermissionGranted}
                    onClick={() => {
                      if (!locationPermissionGranted) {
                        void requestLocationPermission();
                        return;
                      }

                      void patchLocation({ ...data.location, useGps: !data.location?.useGps });
                    }}
                    data-selector-name="onboarding:location:gps"
                    className="rounded-full bg-muted px-3 py-1 text-xs"
                  >
                    {data.location.useGps && locationPermissionGranted ? t("enabled") : t("disabled")}
                  </button>
                </div>
                <button
                  type="button"
                  onClick={() => void refreshGps()}
                  data-selector-name="onboarding:location:refresh-gps"
                  className="w-full rounded-md border border-border bg-card px-3 py-2 text-sm"
                >
                  {locationPermissionGranted ? t("refreshGps") : t("grantPermissions")}
                </button>
                <label className="block text-xs text-muted-foreground">
                  {t("country")}
                  <select
                    value={data.location.country ?? ""}
                    onChange={(event) => patchCountry(event.currentTarget.value)}
                    data-selector-name="onboarding:location:country"
                    className="mt-1 w-full rounded-md border border-input bg-card px-3 py-2 text-sm text-foreground"
                  >
                    {(data.location.countries ?? []).map((country) => <option key={country.code} value={country.code}>{country.name}</option>)}
                  </select>
                </label>
                <label className="block text-xs text-muted-foreground">
                  {t("city")}
                  <select
                    value={data.location.city ?? ""}
                    onChange={(event) => patchCity(event.currentTarget.value)}
                    data-selector-name="onboarding:location:city"
                    className="mt-1 w-full rounded-md border border-input bg-card px-3 py-2 text-sm text-foreground"
                  >
                    {(currentCountry?.cities ?? []).map((city) => <option key={city} value={city}>{city}</option>)}
                  </select>
                </label>
                <div className="grid grid-cols-2 gap-2">
                  <CoordinateInput label={t("latitude")} value={data.location.latitude ?? 0} selectorName="onboarding:location:latitude" onCommit={(latitude) => void patchLocation({ ...data.location, useGps: false, latitude }, true)} />
                  <CoordinateInput label={t("longitude")} value={data.location.longitude ?? 0} selectorName="onboarding:location:longitude" onCommit={(longitude) => void patchLocation({ ...data.location, useGps: false, longitude }, true)} />
                </div>
              </div>
            ) : null}
          </div>
        )}
      </Card>

      <div className="mt-4 flex shrink-0 justify-between" data-selector-name="onboarding:navigation">
        <button type="button" data-selector-name="onboarding:back" onClick={() => setStep((s) => Math.max(0, s - 1))} disabled={step === 0} className="rounded-md px-4 py-2 text-sm font-medium text-muted-foreground disabled:opacity-30">{t("back")}</button>
        <button
          type="button"
          data-selector-name="onboarding:next"
          onClick={async () => {
            if (step !== steps.length - 1) {
              setStep((current) => current + 1);
              return;
            }

            const location = data.location;
            const latitude = location?.latitude ?? 0;
            const longitude = location?.longitude ?? 0;
            if (!hasUsableCoordinates(latitude, longitude)) {
              setFinishError(t("onboardingLocationInvalid"));
              return;
            }

            // The visible default is a real user choice too. Persist it even when
            // the language button was never clicked, so old repository state cannot win.
            await setLanguage(selectedLanguage);

            if (!await patchLocation({ ...location!, useGps: !!location?.useGps && locationPermissionGranted })) {
              return;
            }

            const completed = await executeCommand("onboarding.complete");
            if (!completed.ok) {
              setFinishError(t("status_error"));
              return;
            }

            setData({ ...data, completed: true });
            setOnboardingCompleted(true);
            await navigate({ to: "/", replace: true });
          }}
          className="inline-flex items-center gap-1 rounded-md bg-primary px-5 py-2 text-sm font-medium text-primary-foreground"
        >
          {step === steps.length - 1 ? t("finish") : t("next")} <NextIcon className="h-4 w-4" />
        </button>
      </div>
      {finishError ? <p className="mt-2 shrink-0 text-sm text-destructive" data-selector-name="onboarding:error">{finishError}</p> : null}
    </div>
  );
}

function hasUsableCoordinates(latitude?: number, longitude?: number): boolean {
  return Number.isFinite(latitude) && Number.isFinite(longitude) &&
    Math.abs(latitude ?? 0) <= 90 && Math.abs(longitude ?? 0) <= 180 &&
    (Math.abs(latitude ?? 0) > 0.000001 || Math.abs(longitude ?? 0) > 0.000001);
}
