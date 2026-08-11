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
  const [download, setDownload] = useState<AppDownload | null>(null);

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
    void loadDeviceDownload().then(setDownload);
  }, []);

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

  if (!info) return <div className="h-40 animate-pulse rounded-xl bg-muted" />;

  return (
    <div>
      <SettingsHeader title={t("about")} />
      <div className="flex flex-col gap-3">
        <Card className="text-center">
          <div className="text-2xl font-bold">{info.name}</div>
          <p className="mt-1 text-sm text-muted-foreground">{info.tagline}</p>
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
          {download ? <a href={download.url} download data-selector-name="about:download-native-app" className="col-span-2 flex items-center justify-center gap-2 rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground"><DownloadCloud className="h-4 w-4" /> {download.label}</a> : null}
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

async function loadDeviceDownload(): Promise<AppDownload | null> {
  if (typeof navigator === "undefined") return null;
  try {
    const response = await fetch("/downloads/manifest.json", { cache: "no-store" });
    if (!response.ok) return null;
    if (!response.headers.get("content-type")?.toLowerCase().includes("json")) return null;
    const manifest = await response.json() as { files?: AppDownload[] };
    const candidates = (manifest.files ?? []).filter((file) => matchesDevice(file));
    for (const candidate of sortDownloads(candidates)) {
      if (await urlExists(candidate.url)) return candidate;
    }
  } catch {
    return null;
  }
  return null;
}

function matchesDevice(file: AppDownload): boolean {
  const userAgent = navigator.userAgent;
  const isAndroid = /Android/i.test(userAgent);
  const isIos = /iPhone|iPad|iPod/i.test(userAgent);
  const isWindows = /Windows/i.test(userAgent);
  if (isAndroid) return file.kind === "apk" || file.platform === "android";
  if (isIos) return file.kind === "ios" || file.platform === "ios";
  if (isWindows) return file.kind === "exe" || file.kind === "zip" || file.platform === "windows" || file.platform === "desktop";
  return file.kind === "zip" || file.platform === "desktop";
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

async function urlExists(url: string): Promise<boolean> {
  try {
    const response = await fetch(url, { method: "HEAD", cache: "no-store" });
    return response.ok && !response.headers.get("content-type")?.toLowerCase().includes("html");
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
