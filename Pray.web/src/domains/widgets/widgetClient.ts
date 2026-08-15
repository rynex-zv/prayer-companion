import { appClient } from "@/client/appClient";
import type { WidgetHostCapabilities, WidgetInstanceAssignment, WidgetPreview, WidgetProfile, WidgetProfileDocument, WidgetTemplate } from "./types";

const projectionKey = "widgets.profiles";

export async function createWidgetProfile(template: WidgetTemplate, name: string, previewCapabilities: WidgetHostCapabilities, previewLanguage: "en" | "ar") {
  return appClient.command<{ profile: unknown; document: WidgetProfileDocument; preview?: WidgetPreview }>({
    name: "widgets.createProfile", domain: "widgets", payload: { template, name, previewCapabilities, previewLanguage }, projectionKey,
    projectionData: (data) => (data as { document: WidgetProfileDocument }).document,
  });
}

export async function patchWidgetProfile(id: string, patch: Record<string, unknown>, previewCapabilities?: WidgetHostCapabilities, previewLanguage?: "en" | "ar") {
  return appClient.command<{ profile: unknown; document: WidgetProfileDocument; preview?: WidgetPreview }>({
    name: "widgets.updateProfile", domain: "widgets", payload: { id, patch, previewCapabilities, previewLanguage }, projectionKey,
    projectionData: (data) => (data as { document: WidgetProfileDocument }).document,
  });
}

export async function duplicateWidgetProfile(id: string, name?: string) {
  return appClient.command<{ profile: unknown; document: WidgetProfileDocument }>({
    name: "widgets.duplicateProfile", domain: "widgets", payload: { id, name }, projectionKey,
    projectionData: (data) => (data as { document: WidgetProfileDocument }).document,
  });
}

export async function deleteWidgetProfile(id: string) {
  return appClient.command<{ document: WidgetProfileDocument }>({
    name: "widgets.deleteProfile", domain: "widgets", payload: { id }, projectionKey,
    projectionData: (data) => (data as { document: WidgetProfileDocument }).document,
  });
}

export async function assignWidgetProfile(assignment: WidgetInstanceAssignment) {
  return appClient.command<{ assignment: WidgetInstanceAssignment; document: WidgetProfileDocument }>({
    name: "widgets.assignProfile", domain: "widgets", payload: { assignment }, projectionKey,
    projectionData: (data) => (data as { document: WidgetProfileDocument }).document,
  });
}

export async function getWidgetPreview(profile: WidgetProfile, capabilities: WidgetHostCapabilities, language: "en" | "ar") {
  return appClient.query<WidgetPreview>({
    name: "widgets.getPreview", domain: "widgetPreview", payload: { profileId: profile.id, profile, capabilities, language },
    projectionKey: `widgets.preview.${profile.id}.${profile.revision}.${capabilities.platform}.${capabilities.surface}.${capabilities.family}.${language}`,
  });
}
