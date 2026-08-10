import assert from "node:assert/strict";
import { observeRuntimeValue } from "../src/automation/runtimeDefects.ts";

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
