import { createFileRoute, redirect } from "@tanstack/react-router";
import { useEffect, useMemo, useRef, useState } from "react";
import { Card } from "@/components/Card";
import { Field } from "@/components/Field";
import { Picker } from "@/components/Picker";
import { SettingsHeader } from "@/components/SettingsHeader";
import { useAppLabels } from "@/hooks/useAppLabels";
import { useProjection } from "@/hooks/useProjection";
import { WIDGET_EDITOR_ENABLED } from "@/domains/widgets/feature";
import { assignWidgetProfile, createWidgetProfile, deleteWidgetProfile, duplicateWidgetProfile, getWidgetPreview, patchWidgetProfile } from "@/domains/widgets/widgetClient";
import type { WidgetCatalogEntry, WidgetDensity, WidgetFamily, WidgetHostCapabilities, WidgetPlatform, WidgetPreview, WidgetProfile, WidgetProfileDocument, WidgetSurface, WidgetTextScale, WidgetTemplate } from "@/domains/widgets/types";

export const Route = createFileRoute("/settings/widgets")({
  beforeLoad: () => { if (!WIDGET_EDITOR_ENABLED) throw redirect({ to: "/settings" }); },
  component: WidgetsPage,
});

const platforms: { id: WidgetPlatform; surface: WidgetSurface; families: WidgetFamily[] }[] = [
  { id: "Android", surface: "Home", families: ["Tiny", "Compact", "Medium", "Large"] },
  { id: "Ios", surface: "LockScreen", families: ["Inline", "Circular", "Rectangular", "Small", "Medium", "Large"] },
  { id: "WindowsSystem", surface: "Board", families: ["Small", "Medium", "Large"] },
  { id: "WindowsCompanion", surface: "Desktop", families: ["Compact", "Medium", "Large", "Schedule"] },
];

const defaultStyle = {
  textScale: "Auto" as WidgetTextScale,
  primaryTextColor: "#FFFFFFFF",
  secondaryTextColor: "#B8FFFFFF",
  backgroundColor: "#FF06252B",
  accentColor: "#FF2EC4A6",
  backgroundOpacity: 92,
  followAppTheme: true,
};

const semanticLabel = (t: (key: string) => string, value: string) => t(`widgetValue_${value}`);

function WidgetsPage() {
  const t = useAppLabels();
  const catalogQuery = useProjection<WidgetCatalogEntry[]>("widgets.getCatalog", undefined, "widgets.catalog");
  const profilesQuery = useProjection<WidgetProfileDocument>("widgets.getProfiles", undefined, "widgets.profiles");
  const [selectedId, setSelectedId] = useState("");
  const [draft, setDraft] = useState<WidgetProfile | null>(null);
  const [platform, setPlatform] = useState<WidgetPlatform>("Android");
  const [family, setFamily] = useState<WidgetFamily>("Medium");
  const [language, setLanguage] = useState<"en" | "ar">("en");
  const [preview, setPreview] = useState<WidgetPreview | null>(null);
  const [status, setStatus] = useState("");
  const [busy, setBusy] = useState(false);
  const busyRef = useRef(false);
  const suppressPreviewUntilRef = useRef(0);
  const profiles = profilesQuery.data?.profiles ?? [];
  const catalog = catalogQuery.data ?? [];
  const platformConfig = platforms.find((item) => item.id === platform) ?? platforms[0];
  const template = catalog.find((item) => item.template === draft?.template);

  useEffect(() => {
    const nextId = selectedId && profiles.some((item) => item.id === selectedId) ? selectedId : profiles[0]?.id ?? "";
    if (nextId !== selectedId) setSelectedId(nextId);
    const source = profiles.find((item) => item.id === nextId);
    if (source) setDraft(structuredClone(source));
  }, [profilesQuery.data?.revision, selectedId]);

  useEffect(() => {
    if (!platformConfig.families.includes(family)) setFamily(platformConfig.families[0]);
  }, [platform, family, platformConfig]);

  const capabilities = useMemo<WidgetHostCapabilities>(() => ({
    platform,
    surface: platformConfig.surface,
    family,
    widthDp: family === "Large" || family === "Schedule" ? 360 : family === "Medium" || family === "Rectangular" ? 300 : 170,
    heightDp: family === "Large" || family === "Schedule" ? 300 : family === "Medium" || family === "Rectangular" ? 180 : 80,
    maxTextItems: family === "Large" || family === "Schedule" ? 12 : family === "Medium" || family === "Rectangular" ? 7 : 4,
    maxActions: platform === "WindowsSystem" ? 1 : 2,
    supportsBackgroundColor: !(platform === "Ios" && platformConfig.surface === "LockScreen"),
    supportsBackgroundOpacity: platform !== "WindowsSystem",
    supportsFullColor: !(platform === "Ios" && platformConfig.surface === "LockScreen"),
    supportsLiveCountdown: platform !== "WindowsSystem",
    isAuthenticated: true,
  }), [platform, platformConfig.surface, family]);

  const previewInputKey = draft ? JSON.stringify({
    template: draft.template,
    density: draft.density,
    projection: draft.projection,
    style: draft.style,
    privacy: draft.privacy,
    capabilities,
    language,
  }) : "";

  useEffect(() => {
    if (!draft) return;
    const timer = window.setTimeout(() => {
      if (busyRef.current || performance.now() < suppressPreviewUntilRef.current) return;
      void getWidgetPreview(draft, capabilities, language).then((result) => {
        if (result.ok) { setPreview(result.data); setStatus(""); }
        else { setPreview(null); setStatus(result.error.message); }
      });
    }, 120);
    return () => window.clearTimeout(timer);
  }, [previewInputKey]);

  const mutate = async (operation: () => ReturnType<typeof patchWidgetProfile>) => {
    if (busy) return;
    busyRef.current = true;
    setBusy(true);
    const result = await operation();
    busyRef.current = false;
    setBusy(false);
    if (!result.ok) { setStatus(result.error.message); return; }
    profilesQuery.setData(result.data.document);
    if (result.data.preview) {
      setPreview(result.data.preview);
      suppressPreviewUntilRef.current = performance.now() + 300;
    }
    setStatus(t("widgetSaved"));
  };

  const create = async () => {
    if (busy) return;
    busyRef.current = true;
    setBusy(true);
    const result = await createWidgetProfile((catalog[0]?.template ?? "NextPrayer") as WidgetTemplate, t("widgetNextPrayer"), capabilities, language);
    busyRef.current = false;
    setBusy(false);
    if (!result.ok) { setStatus(result.error.message); return; }
    profilesQuery.setData(result.data.document);
    const id = (result.data.profile as WidgetProfile).id;
    setSelectedId(id);
    if (result.data.preview) {
      setPreview(result.data.preview);
      suppressPreviewUntilRef.current = performance.now() + 300;
    }
  };

  const save = () => draft && mutate(() => patchWidgetProfile(draft.id, {
    expectedRevision: draft.revision,
    name: draft.name,
    density: draft.density,
    projection: draft.projection,
    style: draft.style,
    privacy: draft.privacy,
  }, capabilities, language));

  const restore = () => {
    if (!draft || !template) return;
    const reset = { ...draft, density: "Auto" as WidgetDensity, projection: [...template.defaultProjection], style: defaultStyle, privacy: { hideLocationOnLockScreen: true, hideLocationSourceOnLockScreen: true } };
    setDraft(reset);
    void mutate(() => patchWidgetProfile(draft.id, { expectedRevision: draft.revision, density: reset.density, projection: reset.projection, style: reset.style, privacy: reset.privacy }, capabilities, language));
  };

  if (!draft || !template) return <div className="h-40 animate-pulse rounded-xl bg-muted" />;
  const tree = preview?.renderTree;
  const saveBlocked = !tree || tree.status !== "ready" || tree.omittedProjection.length > 0;

  return (
    <div data-selector-name="widgets:page">
      <SettingsHeader title={t("widgets")} />
      <div className="grid gap-3 xl:grid-cols-[minmax(0,1fr)_minmax(320px,0.8fr)]">
        <div className="space-y-3">
          <Card className="space-y-3">
            <div className="flex flex-wrap gap-2">
              <button type="button" onClick={() => void create()} disabled={busy} className="rounded-md bg-primary px-3 py-2 text-sm font-semibold text-primary-foreground" data-selector-name="widgets:create">{t("widgetCreate")}</button>
              <button type="button" onClick={() => void duplicateWidgetProfile(draft.id).then((result) => { if (result.ok) profilesQuery.setData(result.data.document); else setStatus(result.error.message); })} disabled={busy} className="rounded-md border border-border px-3 py-2 text-sm" data-selector-name="widgets:duplicate">{t("widgetDuplicate")}</button>
              {!draft.isBuiltIn ? <button type="button" onClick={() => void deleteWidgetProfile(draft.id).then((result) => { if (result.ok) profilesQuery.setData(result.data.document); else setStatus(result.error.message); })} disabled={busy} className="rounded-md border border-destructive px-3 py-2 text-sm text-destructive" data-selector-name="widgets:delete">{t("widgetDelete")}</button> : null}
            </div>
            <Field label={t("widgetProfiles")}><Picker value={draft.id} onChange={setSelectedId} selectorName="widgets:profile">{profiles.map((profile) => <option key={profile.id} value={profile.id}>{profile.isBuiltIn ? t(profile.name) : profile.name}</option>)}</Picker></Field>
            <Field label={t("widgetProfileName")}><input value={draft.name} onChange={(event) => setDraft({ ...draft, name: event.target.value })} className="w-full rounded-md border border-border bg-background px-3 py-2" data-selector-name="widgets:name" /></Field>
            <Field label={t("widgetDensity")}><Picker value={draft.density} onChange={(value) => setDraft({ ...draft, density: value as WidgetDensity })} selectorName="widgets:density">{["Auto", "Compact", "Standard", "Detailed"].map((value) => <option key={value} value={value}>{semanticLabel(t, value)}</option>)}</Picker></Field>
          </Card>

          <Card className="space-y-2">
            <div className="text-sm font-semibold">{t("widgetInstalledInstances")}</div>
            {(profilesQuery.data?.assignments.length ?? 0) === 0 ? <p className="text-sm text-muted-foreground">{t("widgetNoInstances")}</p> : profilesQuery.data?.assignments.map((assignment) => (
              <Field key={assignment.instanceId} label={`${assignment.platform} · ${assignment.family}`}>
                <Picker value={assignment.profileId} onChange={(profileId) => void assignWidgetProfile({ ...assignment, profileId }).then((result) => { if (result.ok) profilesQuery.setData(result.data.document); else setStatus(result.error.message); })} selectorName={`widgets:instance:${assignment.instanceId}`}>
                  {profiles.map((profile) => <option key={profile.id} value={profile.id}>{profile.isBuiltIn ? t(profile.name) : profile.name}</option>)}
                </Picker>
              </Field>
            ))}
          </Card>

          <Card className="space-y-3">
            <div className="text-sm font-semibold">{t("widgetProjection")}</div>
            <div className="grid gap-2 sm:grid-cols-2">
              {template.allowedProjection.map((field) => {
                const required = template.requiredProjection.includes(field);
                return <label key={field} className="flex items-center gap-2 rounded-md border border-border p-2 text-sm"><input type="checkbox" checked={draft.projection.includes(field)} disabled={required} onChange={(event) => setDraft({ ...draft, projection: event.target.checked ? [...draft.projection, field] : draft.projection.filter((item) => item !== field) })} data-selector-name={`widgets:projection:${field}`} />{t(`widgetField_${field}`)}</label>;
              })}
            </div>
          </Card>

          <Card className="grid gap-3 sm:grid-cols-2">
            <Field label={t("widgetTextScale")}><Picker value={draft.style.textScale} onChange={(value) => setDraft({ ...draft, style: { ...draft.style, textScale: value as WidgetTextScale } })} selectorName="widgets:text-scale">{["Auto", "Small", "Normal", "Large", "ExtraLarge"].map((value) => <option key={value} value={value}>{semanticLabel(t, value)}</option>)}</Picker></Field>
            {(["primaryTextColor", "secondaryTextColor", "backgroundColor", "accentColor"] as const).map((key) => <Field key={key} label={t(key === "primaryTextColor" ? "widgetPrimaryText" : key === "secondaryTextColor" ? "widgetSecondaryText" : key === "backgroundColor" ? "widgetBackground" : "widgetAccent")}><input type="color" value={`#${draft.style[key].slice(-6)}`} onChange={(event) => setDraft({ ...draft, style: { ...draft.style, [key]: `#FF${event.target.value.slice(1).toUpperCase()}` } })} className="h-10 w-full" data-selector-name={`widgets:${key}`} /></Field>)}
            <Field label={`${t("widgetOpacity")}: ${draft.style.backgroundOpacity}%`}><input type="range" min="0" max="100" value={draft.style.backgroundOpacity} onChange={(event) => setDraft({ ...draft, style: { ...draft.style, backgroundOpacity: Number(event.target.value) } })} className="w-full" data-selector-name="widgets:opacity" /></Field>
            <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={draft.style.followAppTheme} onChange={(event) => setDraft({ ...draft, style: { ...draft.style, followAppTheme: event.target.checked } })} data-selector-name="widgets:follow-theme" />{t("widgetFollowTheme")}</label>
            <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={draft.privacy.hideLocationOnLockScreen} onChange={(event) => setDraft({ ...draft, privacy: { ...draft.privacy, hideLocationOnLockScreen: event.target.checked } })} data-selector-name="widgets:privacy-location" />{t("widgetHideLocation")}</label>
            <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={draft.privacy.hideLocationSourceOnLockScreen} onChange={(event) => setDraft({ ...draft, privacy: { ...draft.privacy, hideLocationSourceOnLockScreen: event.target.checked } })} data-selector-name="widgets:privacy-source" />{t("widgetHideSource")}</label>
            <button type="button" onClick={() => setDraft({ ...draft, style: { ...draft.style, primaryTextColor: "#FFFFFFFF", secondaryTextColor: "#D9FFFFFF", backgroundColor: "#FF06252B" } })} className="rounded-md border border-border px-3 py-2 text-sm" data-selector-name="widgets:auto-contrast">{t("widgetAutoContrast")}</button>
          </Card>

          <div className="flex flex-wrap gap-2"><button type="button" onClick={() => void save()} disabled={busy || saveBlocked} className="rounded-md bg-primary px-4 py-2 font-semibold text-primary-foreground disabled:opacity-50" data-selector-name="widgets:save">{t("widgetSave")}</button><button type="button" onClick={restore} disabled={busy} className="rounded-md border border-border px-4 py-2" data-selector-name="widgets:restore">{t("widgetRestore")}</button></div>
          {status ? <p role="status" className="text-sm text-destructive" data-selector-name="widgets:status">{status}</p> : null}
        </div>

        <Card className="h-fit space-y-3 xl:sticky xl:top-4">
          <div className="text-sm font-semibold">{t("widgetPreview")}</div>
          <div className="grid grid-cols-2 gap-2">
            <Field label={t("widgetPlatform")}><Picker value={platform} onChange={(value) => setPlatform(value as WidgetPlatform)} selectorName="widgets:platform">{platforms.map((item) => <option key={item.id} value={item.id}>{semanticLabel(t, item.id)}</option>)}</Picker></Field>
            <Field label={t("widgetDimension")}><Picker value={family} onChange={(value) => setFamily(value as WidgetFamily)} selectorName="widgets:family">{platformConfig.families.map((item) => <option key={item} value={item}>{semanticLabel(t, item)}</option>)}</Picker></Field>
            <Field label={t("widgetPreviewLanguage")}><Picker value={language} onChange={(value) => setLanguage(value as "en" | "ar")} selectorName="widgets:language"><option value="en">English</option><option value="ar">العربية</option></Picker></Field>
          </div>
          <p className="text-xs text-muted-foreground">{t("widgetSimulationNotice")}</p>
          <div className="overflow-hidden rounded-2xl border border-border p-4" dir={tree?.isRtl ? "rtl" : "ltr"} style={{ color: `#${draft.style.primaryTextColor.slice(-6)}`, backgroundColor: `#${draft.style.backgroundColor.slice(-6)}`, opacity: draft.style.backgroundOpacity / 100 }} data-selector-name="widgets:preview-canvas">
            {tree?.status === "error" ? <p role="alert">{tree.error}</p> : <div className="space-y-2">{tree?.texts.map((item) => <div key={item.key} aria-label={item.accessibilityLabel} className={item.role === "title" || item.role === "time" || item.role === "bearing" ? "text-xl font-bold" : "text-sm"}>{item.text}</div>)}{tree?.rows.map((row) => <div key={row.key} aria-label={row.accessibilityLabel} className="flex justify-between gap-3 text-sm"><span>{row.label}</span><span dir="ltr">{row.value}</span></div>)}{tree?.progress !== undefined ? <progress value={tree.progress} max={1} className="w-full" /> : null}<div className="flex gap-2">{tree?.actions.map((action) => <button key={action.id} type="button" aria-label={action.accessibilityLabel} className="rounded-md bg-primary px-2 py-1 text-xs text-primary-foreground">{action.label}</button>)}</div></div>}
          </div>
          {tree?.warnings.map((warning) => <p key={warning} className="text-xs text-amber-500">{warning}</p>)}
          {tree?.omittedProjection.length ? <p className="text-xs text-destructive" role="alert" data-selector-name="widgets:overflow">{t("widgetOverflow")}: {tree.omittedProjection.map((item) => t(`widgetField_${item}`)).join(", ")}</p> : null}
        </Card>
      </div>
    </div>
  );
}
