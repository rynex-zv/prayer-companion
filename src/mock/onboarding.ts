import type { TestConfig } from "@/app/TEST";

export function getOnboardingMock(state: TestConfig) {
  return {
    completed: state.onboardingCompleted,
    steps: ["language", "permissions", "location"],
    language: state.language,
    permissionsScenario: state.permissionsScenario,
    vpnWarning: false,
    canUseInternet: true,
    canUseGps: false,
  };
}
