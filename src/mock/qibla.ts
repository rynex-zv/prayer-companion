import type { TestConfig } from "@/app/TEST";
import { translations, type Lang } from "./translations";

export function getQiblaMock(state: TestConfig, manualDelta = 0) {
  const labels = translations[state.language as Lang];
  const bearing = 119; // Mecca bearing from Amsterdam
  const baseHeading = state.qiblaState === "manual" ? 100 + manualDelta : 95;
  const heading = state.qiblaState === "searching" ? 0 : baseHeading;
  const needleRotation = bearing - heading;
  const isAligned = state.qiblaState === "aligned" || Math.abs(needleRotation % 360) < 5;

  const headingModes = [
    { id: "auto", label: labels.auto },
    { id: "manual", label: labels.manual },
  ];
  const readingModes = [
    { id: "compass", label: labels.compass },
    { id: "map", label: labels.map },
  ];
  const filterModes = [
    { id: "none", label: labels.filter_none },
    { id: "night", label: labels.filter_night },
    { id: "contrast", label: labels.filter_contrast },
  ];

  let status = "";
  if (state.qiblaState === "searching") status = labels.searching;
  if (state.qiblaState === "noPermission") status = labels.permissionMissing;
  if (isAligned && state.qiblaState !== "searching" && state.qiblaState !== "noPermission") status = labels.aligned;

  return {
    bearing, heading, latitude: 52.3676, longitude: 4.9041, needleRotation, compassRotation: -heading,
    directionLabel: "ESE",
    locationTitle: `${state.city}, ${state.country}`,
    statusMessage: status,
    selectedHeadingMode: state.qiblaState === "manual" ? "manual" : "auto",
    selectedReadingMode: state.qiblaState === "map" ? "map" : "compass",
    selectedFilterMode: "none",
    displayMode: state.qiblaState === "map" ? "Map" : "Compass",
    visualFilter: "None",
    state: state.qiblaState === "noPermission" ? "permissionMissing" : state.qiblaState,
    isAligned,
    headingModes, readingModes, filterModes,
    labels,
  };
}
