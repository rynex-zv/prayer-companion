import { useEffect, useRef, useState } from "react";
import { DatabaseZap } from "lucide-react";
import { useAppLabels } from "@/hooks/useAppLabels";
import { clearApplicationCaches } from "@/lib/siteDataReset";

const REQUIRED_SHAKES = 5;
const SHAKE_THRESHOLD = 24;
const SHAKE_COOLDOWN_MS = 400;
const SHAKE_SEQUENCE_TIMEOUT_MS = 12_000;

export function ShakeDataResetButton() {
  const t = useAppLabels();
  const [revealed, setRevealed] = useState(false);
  const [clearing, setClearing] = useState(false);
  const sequence = useRef({ count: 0, lastShakeAt: 0, startedAt: 0 });

  useEffect(() => {
    const reveal = () => setRevealed(true);
    const onMotion = (event: DeviceMotionEvent) => {
      const acceleration = event.accelerationIncludingGravity ?? event.acceleration;
      if (!acceleration) return;

      const magnitude = Math.sqrt(
        (acceleration.x ?? 0) ** 2 +
        (acceleration.y ?? 0) ** 2 +
        (acceleration.z ?? 0) ** 2,
      );
      if (magnitude < SHAKE_THRESHOLD) return;

      const now = Date.now();
      if (now - sequence.current.lastShakeAt < SHAKE_COOLDOWN_MS) return;
      if (!sequence.current.startedAt || now - sequence.current.startedAt > SHAKE_SEQUENCE_TIMEOUT_MS) {
        sequence.current = { count: 0, lastShakeAt: 0, startedAt: now };
      }

      sequence.current.count += 1;
      sequence.current.lastShakeAt = now;
      if (sequence.current.count >= REQUIRED_SHAKES) reveal();
    };

    window.addEventListener("prayercompanion:shake-unlock", reveal);
    window.addEventListener("devicemotion", onMotion);
    return () => {
      window.removeEventListener("prayercompanion:shake-unlock", reveal);
      window.removeEventListener("devicemotion", onMotion);
    };
  }, []);

  if (!revealed) return null;

  const clearFromBackend = async () => {
    if (clearing) return;
    setClearing(true);
    try {
      await clearApplicationCaches();
    } catch (error) {
      console.error("[pray.cache] shake reset failed", error);
      setClearing(false);
    }
  };

  return (
    <button
      type="button"
      onClick={() => void clearFromBackend()}
      disabled={clearing}
      data-selector-name="app:shake-clear-cache"
      className="absolute bottom-20 left-1/2 z-50 flex -translate-x-1/2 items-center gap-2 whitespace-nowrap rounded-full bg-destructive px-4 py-3 text-sm font-semibold text-destructive-foreground shadow-xl disabled:opacity-60"
    >
      <DatabaseZap className="h-4 w-4" />
      {clearing ? t("clearingAppData") : t("clearCacheFromBackend")}
    </button>
  );
}

declare global {
  interface WindowEventMap {
    "prayercompanion:shake-unlock": Event;
  }
}
