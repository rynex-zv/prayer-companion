export type LocationPermissionState = PermissionState | "unsupported";

export type ConfirmedGpsLocation = {
  useGps: boolean;
  locationSource?: "gps" | "ip" | "manual" | "";
  latitude: number;
  longitude: number;
  country?: string;
  countryName?: string;
  city?: string;
};

export function resolveAutomaticLocationSource(
  permission: LocationPermissionState,
  current?: Pick<ConfirmedGpsLocation, "useGps" | "locationSource">,
): "gps" | "ip" {
  if (permission === "granted") return "gps";
  if (permission === "denied") return "ip";
  return current?.useGps || current?.locationSource === "gps" ? "gps" : "ip";
}

export function canReuseConfirmedGpsLocation(current: ConfirmedGpsLocation): boolean {
  return current.useGps && current.locationSource === "gps" &&
    hasUsableCoordinates(current.latitude, current.longitude) &&
    Boolean(current.country && (current.city || current.countryName));
}

function hasUsableCoordinates(latitude?: number, longitude?: number): boolean {
  return Number.isFinite(latitude) && Number.isFinite(longitude) &&
    Math.abs(latitude ?? 0) <= 90 && Math.abs(longitude ?? 0) <= 180 &&
    (Math.abs(latitude ?? 0) > 0.000001 || Math.abs(longitude ?? 0) > 0.000001);
}
