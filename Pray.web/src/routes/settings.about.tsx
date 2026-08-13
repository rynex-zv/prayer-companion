import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { Card } from "@/components/Card";
import { SettingsHeader } from "@/components/SettingsHeader";
import { executeCommand, nativeBackendReady, platformIntents } from "@/client/applicationClient";
import { Mail, Phone, Globe, Bug, DownloadCloud, DatabaseZap } from "lucide-react";
import { usePageLog } from "@/hooks/usePageLog";
import { useAppLabels } from "@/hooks/useAppLabels";
import { useProjection } from "@/hooks/useProjection";
import { clearApplicationCaches } from "@/lib/siteDataReset";
import { formatWebVersionLabel, resolveAppAssetUrl, useWebVersion } from "@/hooks/useWebVersion";
import { useWebUpdate } from "@/hooks/useWebUpdate";

export const Route = createFileRoute("/settings/about")({
  component: AboutPage,
});

function AboutPage() {
  usePageLog("settings.about");
  const t = useAppLabels();
  const { data: info } = useProjection<AboutSnapshot>("settings.getSnapshot", { section: "about" });
  const [pullStatus, setPullStatus] = useState("");
  const [isPullingRemote, setIsPullingRemote] = useState(false);
  const [remoteUrl, setRemoteUrl] = useState("");
  const [isClearingData, setIsClearingData] = useState(false);
  const webVersion = useWebVersion();
  const webUpdate = useWebUpdate();
  const [downloadState, setDownloadState] = useState<DownloadState>({ status: "loading", platform: detectDevicePlatform() });

  useEffect(() => {
    console.info("[pray.about] mounted");
    void executeCommand<{ url?: string }>("mauiWebber.getRemoteUrl").then((res) => {
      console.info("[pray.about] getRemoteUrl result", res);
      if (res.ok && res.data.url) {
        setRemoteUrl(res.data.url);
      }
    });
  }, []);

  useEffect(() => {
    if (info?.remoteWebUrl) {
      setRemoteUrl(info.remoteWebUrl);
    }
  }, [info?.remoteWebUrl]);

  useEffect(() => {
    if (!info) return;
    void loadDeviceDownload(info.defaultRemoteWebUrl).then(setDownloadState);
  }, [info]);

  const saveRemoteUrl = async (url: string) => {
    console.info("[pray.about] saveRemoteUrl start", { url });
    setPullStatus(t("savingRemoteWebUrl"));
    const res = await executeCommand<{ url?: string }>("mauiWebber.setRemoteUrl", { url });
    console.info("[pray.about] saveRemoteUrl result", res);
    if (!res.ok) {
      setPullStatus(res.error || t("invalidRemoteWebUrl"));
      return false;
    }

    setRemoteUrl(res.data.url ?? url);
    setPullStatus(`${t("remoteWebUrlSaved")}: ${res.data.url ?? url}`);
    return true;
  };

  const pullRemote = async () => {
    if (isPullingRemote) return;
    console.info("[pray.about] pullRemote start", { remoteUrl });
    setIsPullingRemote(true);
    try {
      setPullStatus(t("pullingLatestWebVersion"));
      const saved = await saveRemoteUrl(remoteUrl);
      console.info("[pray.about] pullRemote saved", { saved });
      if (!saved) {
        return;
      }

      console.info("[pray.about] pullRemote callNative start");
      const res = await withTimeout(executeCommand<{
        status?: string;
        version?: string;
        lastPulledVersion?: string;
        url?: string;
        error?: string;
      }>("mauiWebber.pullRemote"), 45000, `${t("webUpdateFailed")} ${t("lastPulledVersion")}: ${t("unknown")}`);
      console.info("[pray.about] pullRemote callNative result", res);
      const data = "data" in res ? res.data : undefined;
      const version = data?.lastPulledVersion ?? data?.version ?? t("unknown");
      if (!res.ok) {
        setPullStatus(`${res.error || t("webUpdateFailed")} ${t("lastPulledVersion")}: ${version}`);
        return;
      }

      if (data?.status === "notAvailable") {
        setPullStatus(data.error ?? t("webUpdateFailed"));
        return;
      }
      if (data?.status === "same") {
        setPullStatus(`${t("sameVersion")} ${t("lastPulledVersion")}: ${version}`);
        return;
      }

      setPullStatus(`${t("pulledLatestWebVersion")} ${t("lastPulledVersion")}: ${version}`);
    } catch (error) {
      console.error("[pray.about] pullRemote error", error);
      setPullStatus(error instanceof Error ? error.message : `${t("webUpdateFailed")} ${t("lastPulledVersion")}: ${t("unknown")}`);
    } finally {
      console.info("[pray.about] pullRemote end");
      setIsPullingRemote(false);
    }
  };

  const clearSiteData = async () => {
    if (isClearingData) return;
    setIsClearingData(true);
    setPullStatus(t("clearingAppData"));
    try {
      await clearApplicationCaches();
    } catch (error) {
      console.error("[pray.about] clear site data failed", error);
      setPullStatus(error instanceof Error ? error.message : t("clearAppDataFailed"));
      setIsClearingData(false);
    }
  };
  const applyWebUpdate = async () => {
    try {
      await webUpdate.apply();
    } catch (error) {
      setPullStatus(error instanceof Error ? error.message : t("webUpdateFailed"));
    }
  };

  if (!info) return <div className="h-40 animate-pulse rounded-xl bg-muted" />;

  return (
    <div>
      <SettingsHeader title={t("about")} />
      <div className="flex flex-col gap-3">
        <Card className="text-center">
          <div className="text-2xl font-bold">{info.name}</div>
          <p className="mt-1 text-sm text-muted-foreground">{info.tagline}</p>
          {webVersion ? <p className="mt-2 text-xs text-muted-foreground" data-selector-name="about:web-version" dir="ltr">{formatWebVersionLabel(t, webVersion)}</p> : null}
          {webUpdate.status === "ready" || webUpdate.status === "applying" ? (
            <button
              type="button"
              onClick={() => void applyWebUpdate()}
              disabled={webUpdate.status === "applying"}
              data-selector-name="about:web-update"
              className="mx-auto mt-3 flex items-center justify-center gap-2 rounded-md bg-primary px-3 py-2 text-sm font-semibold text-primary-foreground disabled:opacity-60"
            >
              <DownloadCloud className="h-4 w-4" />
              {webUpdate.status === "applying"
                ? t("webUpdateApplying")
                : t("webUpdateButton").replace("{0}", webUpdate.availableVersion || webUpdate.latestVersion)}
            </button>
          ) : null}
        </Card>
        <Card className="space-y-2 text-sm">
          <p>{info.privacy}</p>
          <p>{info.source}</p>
          <p className="text-muted-foreground">{t("maintainedBy")} <span className="font-medium text-foreground">{info.maintainer}</span></p>
        </Card>
        <Card className="space-y-2">
          <div className="text-sm font-semibold">{info.contact}</div>
          <div className="space-y-1 text-sm">
            <div className="flex items-center gap-2"><Mail className="h-4 w-4 text-primary" />{info.email}</div>
            <div className="flex items-center gap-2"><Phone className="h-4 w-4 text-primary" />{info.phone}</div>
            <div className="flex items-center gap-2"><Globe className="h-4 w-4 text-primary" />{info.website}</div>
            <p className="text-xs text-muted-foreground">{info.websiteNote}</p>
          </div>
        </Card>
        <Card className="space-y-3">
          <div>
            <div className="text-sm font-semibold">{t("appStorage")}</div>
            <p className="mt-1 text-xs text-muted-foreground">{t("clearAppDataDescription")}</p>
          </div>
          <p className="text-xs text-muted-foreground">
            {t("backendRestoreHint")}
          </p>
          <button
            type="button"
            onClick={() => void clearSiteData()}
            disabled={isClearingData}
            data-selector-name="about:clear-site-data"
            className="flex w-full items-center justify-center gap-2 rounded-md bg-destructive px-3 py-2 text-sm font-medium text-destructive-foreground disabled:opacity-60"
          >
            <DatabaseZap className="h-4 w-4" />
            {isClearingData ? t("clearingAppData") : t("clearAppCache")}
          </button>
        </Card>
        <div className="grid grid-cols-2 gap-2">
          <button onClick={() => platformIntents.openEmail(info.email)} className="flex items-center justify-center gap-2 rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground"><Mail className="h-4 w-4" /> {t("emailRynex")}</button>
          <button onClick={() => platformIntents.call(info.phone)} className="flex items-center justify-center gap-2 rounded-md bg-secondary px-3 py-2 text-sm font-medium"><Phone className="h-4 w-4" /> {t("callRynex")} {info.phone}</button>
          <button onClick={() => platformIntents.openUrl(info.website)} className="flex items-center justify-center gap-2 rounded-md bg-secondary px-3 py-2 text-sm font-medium"><Globe className="h-4 w-4" /> {t("openWebsite")}</button>
          <button onClick={() => platformIntents.reportIssue()} className="flex items-center justify-center gap-2 rounded-md bg-secondary px-3 py-2 text-sm font-medium"><Bug className="h-4 w-4" /> {t("report")}</button>
          <DownloadControl state={downloadState} label={t} />
          {nativeBackendReady() ? <button onClick={pullRemote} disabled={isPullingRemote} data-selector-name="about:pull-remote-web" className="col-span-2 flex items-center justify-center gap-2 rounded-md border border-border bg-card px-3 py-2 text-sm font-medium disabled:opacity-60"><DownloadCloud className="h-4 w-4" /> {isPullingRemote ? t("pulling") : t("pullLatestWebVersion")}</button> : null}
        </div>
        <div className="grid gap-2">
          <label className="text-xs font-medium text-muted-foreground" htmlFor="remote-web-url">{t("remoteWebBundleUrl")}</label>
          <div className="grid grid-cols-[1fr_auto] gap-2">
            <input
              id="remote-web-url"
              value={remoteUrl}
              onChange={(event) => setRemoteUrl(event.currentTarget.value)}
              data-selector-name="about:remote-web-url"
              className="min-h-10 rounded-md border border-input bg-card px-3 py-2 text-sm"
              inputMode="url"
              spellCheck={false}
            />
            <button
              type="button"
              onClick={() => void saveRemoteUrl(remoteUrl)}
              data-selector-name="about:save-remote-web-url"
              className="rounded-md border border-border bg-card px-3 py-2 text-sm font-medium"
            >
              {t("save")}
            </button>
          </div>
          <button
            type="button"
            onClick={() => void saveRemoteUrl(info.defaultRemoteWebUrl)}
            data-selector-name="about:reset-remote-web-url"
            className="justify-self-start rounded-md bg-secondary px-3 py-2 text-xs font-medium"
          >
            {t("resetToDefault")}
          </button>
        </div>
        {pullStatus ? <div data-selector-name="about:pull-remote-status" className="text-center text-xs text-muted-foreground">{pullStatus}</div> : null}
      </div>
    </div>
  );
}

type AboutSnapshot = {
  name: string;
  tagline: string;
  privacy: string;
  source: string;
  maintainer: string;
  contact: string;
  email: string;
  phone: string;
  website: string;
  websiteNote: string;
  remoteWebUrl: string;
  defaultRemoteWebUrl: string;
};

type AppDownload = {
  platform: "windows" | "android" | "ios" | "desktop";
  kind: "exe" | "zip" | "apk" | "ios";
  url: string;
  label: string;
  version?: string;
};

type DevicePlatform = "windows" | "android" | "ios" | "desktop";

type DownloadState =
  | { status: "loading"; platform: DevicePlatform }
  | { status: "ready"; platform: DevicePlatform; download: AppDownload }
  | { status: "up-to-date"; platform: DevicePlatform; download: AppDownload }
  | { status: "unavailable" | "error"; platform: DevicePlatform };

function DownloadControl({ state, label }: { state: DownloadState; label: (key: string) => string }) {
  if (state.status === "ready") {
    return <a href={state.download.url} download data-selector-name="about:download-native-app" className="col-span-2 flex items-center justify-center gap-2 rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground"><DownloadCloud className="h-4 w-4" /> {downloadLabel(state.download, label)}</a>;
  }

  if (state.status === "up-to-date") {
    return <div data-selector-name="about:download-native-app-status" className="col-span-2 rounded-md border border-border bg-card px-3 py-2 text-center text-sm text-muted-foreground">{label("sameVersion")} ({state.download.version ?? platformLabel(state.platform)})</div>;
  }

  return (
    <div data-selector-name="about:download-native-app-status" className="col-span-2 rounded-md border border-border bg-card px-3 py-2 text-center text-sm text-muted-foreground">
      {state.status === "loading" ? label("checkingDeviceDownload") : `${label("downloadUnavailableForDevice")} (${platformLabel(state.platform)})`}
    </div>
  );
}

async function loadDeviceDownload(remoteBaseUrl: string): Promise<DownloadState> {
  const platform = detectDevicePlatform();
  if (typeof navigator === "undefined") return { status: "unavailable", platform };
  try {
    const manifestUrl = resolveLiveDownloadManifestUrl(remoteBaseUrl);
    const response = await fetch(manifestUrl, { cache: "no-store" });
    if (!response.ok) return { status: "error", platform };
    if (!response.headers.get("content-type")?.toLowerCase().includes("json")) return { status: "error", platform };
    const manifest = await response.json() as { files?: AppDownload[] };
    const candidates = (manifest.files ?? []).filter((file) => matchesDevice(file, platform)).map((file) => ({
      ...file,
      url: new URL(file.url, manifestUrl).toString(),
    }));
    for (const candidate of sortDownloads(candidates)) {
      if (await urlExists(candidate.url)) {
        return { status: "ready", platform, download: candidate };
      }
    }
  } catch {
    return { status: "error", platform };
  }
  return { status: "unavailable", platform };
}

function resolveLiveDownloadManifestUrl(remoteBaseUrl: string): string {
  const baseUrl = nativeBackendReady()
    ? new URL("downloads/manifest.json", ensureTrailingSlash(remoteBaseUrl))
    : new URL(resolveAppAssetUrl("downloads/manifest.json"), document.baseURI);
  baseUrl.searchParams.set("native-download-check", String(Date.now()));
  return baseUrl.toString();
}

function ensureTrailingSlash(value: string): string {
  return value.endsWith("/") ? value : `${value}/`;
}

function detectDevicePlatform(): DevicePlatform {
  if (typeof navigator === "undefined") return "desktop";
  const userAgent = navigator.userAgent;
  if (/Android/i.test(userAgent)) return "android";
  if (/iPhone|iPad|iPod/i.test(userAgent)) return "ios";
  if (/Windows/i.test(userAgent)) return "windows";
  return "desktop";
}

function matchesDevice(file: AppDownload, platform: DevicePlatform): boolean {
  if (platform === "android") return file.kind === "apk" && file.platform === "android";
  if (platform === "ios") return file.kind === "ios" && file.platform === "ios";
  if (platform === "windows") return file.platform === "windows" && (file.kind === "exe" || file.kind === "zip");
  return file.platform === "desktop" && file.kind === "zip";
}

function platformLabel(platform: DevicePlatform): string {
  if (platform === "android") return "Android";
  if (platform === "ios") return "iOS";
  if (platform === "windows") return "Windows";
  return "Desktop";
}

function sortDownloads(files: AppDownload[]): AppDownload[] {
  const priority = new Map<AppDownload["kind"], number>([
    ["apk", 0],
    ["ios", 0],
    ["exe", 0],
    ["zip", 1],
  ]);
  return [...files].sort((a, b) => (priority.get(a.kind) ?? 9) - (priority.get(b.kind) ?? 9));
}

function downloadLabel(download: AppDownload, label: (key: string) => string): string {
  const name = download.kind === "apk" || download.platform === "android" ? label("downloadAndroidApk")
    : download.kind === "exe" ? label("downloadWindowsExe")
    : download.kind === "zip" ? label("downloadDesktopZip")
    : download.kind === "ios" || download.platform === "ios" ? label("downloadIosBuild")
    : download.label;
  const version = formatNativeDownloadVersion(download.version, label);
  return version ? `${name} — ${version}` : name;
}

function formatNativeDownloadVersion(version: string | undefined, label: (key: string) => string): string {
  const match = version?.match(/^\s*([0-9]+(?:\.[0-9]+){1,3})\s*\(\s*web\s*([0-9]+)\s*\)\s*$/i);
  if (!match) return version ?? "";
  return `${formatTemplate(label("nativeAppVersion"), match[1])} / ${formatTemplate(label("embeddedWebVersion"), match[2])}`;
}

function formatTemplate(template: string, value: string): string {
  return template && template !== "nativeAppVersion" && template !== "embeddedWebVersion"
    ? template.replace("{0}", value)
    : value;
}

async function urlExists(url: string): Promise<boolean> {
  try {
    const resolvedUrl = new URL(url, document.baseURI);
    resolvedUrl.searchParams.set("native-download-head", String(Date.now()));
    const response = await fetch(resolvedUrl, { method: "HEAD", cache: "no-store" });
    const contentType = response.headers.get("content-type")?.toLowerCase() ?? "";
    return response.ok && !contentType.includes("html") && !contentType.includes("json");
  } catch {
    return false;
  }
}

function withTimeout<T>(promise: Promise<T>, timeoutMs: number, error: string): Promise<T> {
  return new Promise((resolve, reject) => {
    const timeout = window.setTimeout(() => reject(new Error(error)), timeoutMs);
    promise.then(
      (value) => {
        window.clearTimeout(timeout);
        resolve(value);
      },
      (reason) => {
        window.clearTimeout(timeout);
        reject(reason);
      },
    );
  });
}
