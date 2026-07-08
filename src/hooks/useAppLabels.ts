import { getLabel, languageProxy, useAppStore } from "@/state/appStore";

export type LabelProxy = typeof languageProxy;

export function createLabelProxy() {
  return languageProxy;
}

export function useAppLabels() {
  useAppStore((state) => state.languageObject.updatedAt);
  return getLabel;
}

export function refreshShellLabels() {
  window.dispatchEvent(new CustomEvent("prayadfree:shell-refresh"));
}
