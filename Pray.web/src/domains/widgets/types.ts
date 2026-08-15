export type WidgetTemplate = "NextPrayer" | "DailyPrayer" | "Fasting" | "Tasbih" | "DateAndPrayer" | "QiblaBearing";
export type WidgetDensity = "Auto" | "Compact" | "Standard" | "Detailed";
export type WidgetTextScale = "Auto" | "Small" | "Normal" | "Large" | "ExtraLarge";
export type WidgetPlatform = "Preview" | "Android" | "Ios" | "WindowsSystem" | "WindowsCompanion";
export type WidgetSurface = "Home" | "LockScreen" | "Board" | "Desktop" | "Preview";
export type WidgetFamily = "Inline" | "Circular" | "Tiny" | "Compact" | "Small" | "Rectangular" | "Medium" | "Large" | "Schedule";

export type WidgetStyle = {
  textScale: WidgetTextScale;
  primaryTextColor: string;
  secondaryTextColor: string;
  backgroundColor: string;
  accentColor: string;
  backgroundOpacity: number;
  followAppTheme: boolean;
};

export type WidgetPrivacy = {
  hideLocationOnLockScreen: boolean;
  hideLocationSourceOnLockScreen: boolean;
};

export type WidgetProfile = {
  id: string;
  name: string;
  template: WidgetTemplate;
  revision: number;
  density: WidgetDensity;
  projection: string[];
  style: WidgetStyle;
  privacy: WidgetPrivacy;
  isBuiltIn: boolean;
};

export type WidgetCatalogEntry = {
  template: WidgetTemplate;
  nameKey: string;
  requiredProjection: string[];
  defaultProjection: string[];
  allowedProjection: string[];
};

export type WidgetInstanceAssignment = {
  instanceId: string;
  profileId: string;
  platform: WidgetPlatform;
  surface: WidgetSurface;
  family: WidgetFamily;
  minWidthDp: number;
  maxWidthDp: number;
  minHeightDp: number;
  maxHeightDp: number;
};

export type WidgetProfileDocument = {
  schemaVersion: number;
  revision: number;
  profiles: WidgetProfile[];
  assignments: WidgetInstanceAssignment[];
};

export type WidgetHostCapabilities = {
  platform: WidgetPlatform;
  surface: WidgetSurface;
  family: WidgetFamily;
  widthDp: number;
  heightDp: number;
  maxTextItems: number;
  maxActions: number;
  supportsBackgroundColor: boolean;
  supportsBackgroundOpacity: boolean;
  supportsFullColor: boolean;
  supportsLiveCountdown: boolean;
  isAuthenticated: boolean;
};

export type WidgetRenderTree = {
  profileId: string;
  profileRevision: number;
  status: string;
  error: string;
  isRtl: boolean;
  family: WidgetFamily;
  style: WidgetStyle;
  texts: { key: string; text: string; role: string; required: boolean; accessibilityLabel: string }[];
  rows: { key: string; label: string; value: string; highlighted: boolean; accessibilityLabel: string }[];
  actions: { id: string; label: string; deepLink: string; accessibilityLabel: string }[];
  countdownTargetUnixMilliseconds?: number;
  progress?: number;
  omittedProjection: string[];
  warnings: string[];
};

export type WidgetPreview = {
  profile: WidgetProfile;
  projection: { generatedAtUnixMilliseconds: number; status: string; error: string };
  renderTree: WidgetRenderTree;
};
