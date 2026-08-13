import { useEffect, useState } from "react";
import { useWebUpdate } from "./useWebUpdate";

let cachedVersion = "";
let pendingVersion: Promise<string> | null = null;

export function useWebVersion() {
  const update = useWebUpdate();
  const [version, setVersion] = useState(update.currentVersion || cachedVersion);

  useEffect(() => {
    if (update.currentVersion) {
      setVersion(update.currentVersion);
      return undefined;
    }
    let active = true;
    void readWebVersion().then((value) => {
      if (active) setVersion(value);
    });
    return () => {
      active = false;
    };
  }, [update.currentVersion]);

  return version;
}

export function formatWebVersionLabel(label: (key: string) => string, version: string): string {
  const template = label("currentWebVersion");
  if (!template || template === "currentWebVersion") return `${label("version")} ${version}`;
  return template.replace("{0}", version);
}

function readWebVersion(): Promise<string> {
  if (cachedVersion) return Promise.resolve(cachedVersion);
  if (pendingVersion) return pendingVersion;

  pendingVersion = fetch(resolveAppAssetUrl("version.web.json"), { cache: "no-store" })
    .then(async (response) => {
      const contentType = response.headers.get("content-type")?.toLowerCase() ?? "";
      if (!response.ok || contentType.includes("html")) return "";
      const metadata = await response.json() as { version?: unknown };
      return typeof metadata.version === "string" ? metadata.version.trim() : "";
    })
    .then(async (version) => {
      if (version) return version;
      const response = await fetch(resolveAppAssetUrl("version.web.info"), { cache: "no-store" });
      const contentType = response.headers.get("content-type")?.toLowerCase() ?? "";
      if (!response.ok || contentType.includes("html")) return "";
      return (await response.text()).trim();
    })
    .then((version) => {
      cachedVersion = version;
      return version;
    })
    .catch(() => "")
    .finally(() => {
      pendingVersion = null;
    });

  return pendingVersion;
}

export function resolveAppAssetUrl(relativePath: string): URL {
  const normalized = relativePath.replace(/^\/+/, "");
  const base = import.meta.env.BASE_URL;
  if (base === "./" || base.startsWith("../")) return new URL(normalized, document.baseURI);
  return new URL(`${base}${normalized}`, window.location.origin);
}
