import assert from "node:assert/strict";
import { observeRuntimeValue } from "../src/automation/runtimeDefects.ts";
import { canReuseConfirmedGpsLocation, resolveAutomaticLocationSource } from "../src/native/locationResumePolicy.ts";

const metamask = observeRuntimeValue(new Error(
  "Failed to connect to MetaMask\n" +
  "    at connect (chrome-extension://ejbalbakoplchlghecdalmeeeajnimhm/scripts/inpage.js:7:84179)\n" +
  "    at async caller (https://pray.local.rynex.nl/assets/app.js:36:15468)",
));
assert.equal(metamask.source, "browser-extension");

const appError = observeRuntimeValue(new Error(
  "Prayer calculation failed\n    at calculate (https://pray.local.rynex.nl/assets/app.js:42:7)",
));
assert.equal(appError.source, "application");

const sourceOnlyExtension = observeRuntimeValue("Script error", "chrome-extension://example/content.js");
assert.equal(sourceOnlyExtension.source, "browser-extension");

const unknown = observeRuntimeValue("Unhandled rejection without a source");
assert.equal(unknown.source, "application", "Unknown errors must fail closed");

console.info("runtime defect classification tests passed");

assert.equal(resolveAutomaticLocationSource("granted"), "gps");
assert.equal(resolveAutomaticLocationSource("denied", { useGps: true, locationSource: "gps" }), "ip");
assert.equal(resolveAutomaticLocationSource("prompt", { useGps: true, locationSource: "gps" }), "gps");
assert.equal(resolveAutomaticLocationSource("unsupported", { useGps: false, locationSource: "ip" }), "ip");

assert.equal(canReuseConfirmedGpsLocation({
  useGps: true,
  locationSource: "gps",
  latitude: 25.3085,
  longitude: 55.3648,
  country: "AE",
  countryName: "United Arab Emirates",
  city: "Sharjah",
}), true, "A transient reverse-geocode failure must retain the last confirmed GPS location");
assert.equal(canReuseConfirmedGpsLocation({
  useGps: true,
  locationSource: "gps",
  latitude: 25.3085,
  longitude: 55.3648,
  country: "",
  countryName: "",
  city: "",
}), false, "An incomplete GPS location must never be accepted as confirmed");

console.info("location resume decision tests passed");
