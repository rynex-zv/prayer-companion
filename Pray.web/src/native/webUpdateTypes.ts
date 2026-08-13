export type WebVersionMetadata = {
  version: string;
  build?: number;
  legacyVersion?: string;
  cacheEpoch?: number;
  minimumSupportedVersion?: string;
  serviceWorker?: string;
  manifest?: string;
  generatedAt?: string;
};

export type WebUpdateStatus =
  | "idle"
  | "checking"
  | "current"
  | "downloading"
  | "ready"
  | "applying"
  | "unsupported"
  | "error";

export type WebUpdateSnapshot = {
  status: WebUpdateStatus;
  currentVersion: string;
  latestVersion: string;
  availableVersion: string;
  required: boolean;
  error: string;
};

export type WebUpdateApi = {
  getSnapshot: () => WebUpdateSnapshot;
  subscribe: (listener: (snapshot: WebUpdateSnapshot) => void) => () => void;
  apply: () => Promise<void>;
  checkNow: () => Promise<void>;
};

declare global {
  interface Window {
    __prayWebUpdate?: WebUpdateApi;
  }
}

export {};
