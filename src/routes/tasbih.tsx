import { createFileRoute } from "@tanstack/react-router";
import { type PointerEvent, useMemo, useRef, useState } from "react";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";
import { Card } from "@/components/Card";
import { Picker } from "@/components/Picker";
import { RotateCcw } from "lucide-react";
import { cn } from "@/lib/utils";
import { PageLog } from "@/components/PageLog";
import { usePageLog } from "@/hooks/usePageLog";
import { useAppLabels } from "@/hooks/useAppLabels";

export const Route = createFileRoute("/tasbih")({
  head: () => ({
    meta: [
      { title: "Tasbih — Pray Ad Free" },
      { name: "description", content: "Tasbih counter with presets." },
    ],
  }),
  component: TasbihPage,
});

type Preset = {
  id: string;
  name: string;
  repeatMode: string;
  items: {
    text: string;
    targetCount: number;
  }[];
};

type Snapshot = {
  count: number;
  currentPhrase: string;
  progressText: string;
  isPresetSelectionEnabled: boolean;
  selectedPresetId: string;
  presets: Preset[];
};

type TasbihRingProps = {
  count: number;
  currentPhrase: string;
  progressText: string;
  beadCount?: number;
  onIncrement: () => void | Promise<void>;
};

type Point = {
  x: number;
  y: number;
  angle: number;
};

function normalizeAngle(angle: number) {
  return Math.atan2(Math.sin(angle), Math.cos(angle));
}

function getPageDirectionMultiplier() {
  if (typeof document === "undefined") return 1;

  const dir =
    document.documentElement.getAttribute("dir") ||
    window.getComputedStyle(document.documentElement).direction ||
    "ltr";

  return dir.toLowerCase() === "rtl" ? 1 : -1;
}

function TasbihRing({
  count,
  currentPhrase,
  progressText,
  beadCount = 33,
  onIncrement,
}: TasbihRingProps) {
  const safeBeadCount = Math.max(21, Math.min(beadCount, 33));

  const directionMultiplier = useMemo(() => getPageDirectionMultiplier(), []);

  const [dragging, setDragging] = useState(false);
  const [dragDistance, setDragDistance] = useState(0);

  const startY = useRef(0);
  const dragDistanceRef = useRef(0);
  const busyRef = useRef(false);
  const ignoreNextClick = useRef(false);

  const centerX = 160;
  const centerY = 218;
  const radiusX = 116;
  const radiusY = 176;
  const dragStep = 32;
  const swipeThreshold = 22;

  const incrementOnce = async () => {
    if (busyRef.current) return;

    busyRef.current = true;
    try {
      await onIncrement();
    } finally {
      busyRef.current = false;
    }
  };

  const handlePointerDown = (e: PointerEvent<HTMLDivElement>) => {
    e.currentTarget.setPointerCapture(e.pointerId);

    startY.current = e.clientY;
    dragDistanceRef.current = 0;
    ignoreNextClick.current = false;

    setDragging(true);
    setDragDistance(0);
  };

  const handlePointerMove = (e: PointerEvent<HTMLDivElement>) => {
    if (!dragging) return;

    const nextDistance = Math.max(
      -70,
      Math.min(70, e.clientY - startY.current),
    );

    dragDistanceRef.current = nextDistance;
    setDragDistance(nextDistance);
  };

  const handlePointerUp = () => {
    const shouldIncrement = Math.abs(dragDistanceRef.current) > swipeThreshold;

    setDragging(false);
    setDragDistance(0);
    dragDistanceRef.current = 0;

    if (shouldIncrement) {
      ignoreNextClick.current = true;
      void incrementOnce();
    }
  };

  const handleClick = () => {
    if (ignoreNextClick.current) {
      ignoreNextClick.current = false;
      return;
    }

    void incrementOnce();
  };

  const visualOffset =
    directionMultiplier * count + directionMultiplier * (dragDistance / dragStep);

  const points: Point[] = Array.from({ length: safeBeadCount }).map((_, i) => {
    const angle =
      ((i - visualOffset) / safeBeadCount) * Math.PI * 2 - Math.PI / 2;

    return {
      angle,
      x: centerX + Math.cos(angle) * radiusX,
      y: centerY + Math.sin(angle) * radiusY,
    };
  });

  const visibleIndexes = points
    .map((point, index) => {
      const gapAngle = normalizeAngle(point.angle - Math.PI / 2);
      const isInBottomTouchArea = Math.abs(gapAngle) < 0.2;

      return isInBottomTouchArea ? null : index;
    })
    .filter((index): index is number => index !== null);

  let activeIndex = 0;
  let smallestTopDistance = Number.POSITIVE_INFINITY;

  points.forEach((point, index) => {
    const topDistance = Math.abs(normalizeAngle(point.angle + Math.PI / 2));

    if (topDistance < smallestTopDistance) {
      smallestTopDistance = topDistance;
      activeIndex = index;
    }
  });

  return (
    <div
      role="button"
      tabIndex={0}
      onClick={handleClick}
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={handlePointerUp}
      onPointerCancel={handlePointerUp}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          void incrementOnce();
        }
      }}
      className={cn(
        "relative mx-auto h-[455px] w-full max-w-[350px] touch-none select-none outline-none",
        "cursor-pointer rounded-[2rem]",
      )}
      aria-label="Tasbih counter"
    >
      {/* connecting thread */}
      {visibleIndexes.map((index) => {
        const nextIndex = (index + 1) % safeBeadCount;

        if (!visibleIndexes.includes(nextIndex)) return null;

        const a = points[index];
        const b = points[nextIndex];

        const dx = b.x - a.x;
        const dy = b.y - a.y;
        const length = Math.sqrt(dx * dx + dy * dy);
        const angleDeg = Math.atan2(dy, dx) * (180 / Math.PI);

        return (
          <div
            key={`thread-${index}`}
            className="pointer-events-none absolute z-0 h-[2px] origin-left rounded-full bg-primary/25"
            style={{
              left: a.x,
              top: a.y,
              width: length,
              transform: `rotate(${angleDeg}deg)`,
            }}
          />
        );
      })}

      {/* beads */}
      {visibleIndexes.map((index) => {
        const point = points[index];
        const isActive = index === activeIndex;
        const size = isActive ? 34 : 26;

        return (
          <div
            key={`bead-${index}`}
            className={cn(
              "pointer-events-none absolute z-10 rounded-full border shadow-sm",
              "border-primary/55 bg-primary/15",
              isActive && "z-20 border-primary/85 bg-primary/25 shadow-md",
            )}
            style={{
              left: point.x,
              top: point.y,
              width: size,
              height: size,
              transform: "translate(-50%, -50%)",
            }}
          >
            {isActive && (
              <div
                className="absolute rounded-full bg-primary/65"
                style={{
                  left: "50%",
                  top: "50%",
                  width: 12,
                  height: 12,
                  transform: "translate(-50%, -50%)",
                }}
              />
            )}
          </div>
        );
      })}

      {/* small touch guide, not blocking the beads */}
      <div className="pointer-events-none absolute left-1/2 top-[374px] z-20 h-6 w-24 -translate-x-1/2 rounded-full border border-dashed border-primary/45 bg-background/70" />

      {/* center counter */}
      <div className="pointer-events-none absolute left-1/2 top-[218px] z-30 flex h-44 w-44 -translate-x-1/2 -translate-y-1/2 flex-col items-center justify-center rounded-full border border-border bg-background/95 px-4 text-center shadow-sm backdrop-blur">
        <div className="line-clamp-2 text-lg font-bold leading-snug text-primary">
          {currentPhrase}
        </div>

        <div className="mt-1 text-sm font-medium text-muted-foreground">
          {progressText}
        </div>

        <div className="mt-3 text-6xl font-bold tabular-nums text-primary">
          {count}
        </div>
      </div>

      <div className="absolute bottom-1 left-1/2 z-40 -translate-x-1/2 rounded-full border border-border bg-background/95 px-5 py-2 text-center text-sm text-muted-foreground shadow-sm">
        اضغط أو اسحب للأعلى أو للأسفل
      </div>
    </div>
  );
}

function TasbihPage() {
  usePageLog("tasbih");

  const t = useAppLabels();
  const { data, refresh } = useSnapshot<Snapshot>("tasbih.getSnapshot");

  if (!data) {
    return <div className="h-80 animate-pulse rounded-xl bg-muted" />;
  }

  const selectedPreset = data.presets.find(
    (p) => p.id === data.selectedPresetId,
  );

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-end">
        <PageLog page="tasbih" />
      </div>

      <Card className="flex flex-col items-center gap-4 py-6">
        <TasbihRing
          count={data.count}
          currentPhrase={data.currentPhrase}
          progressText={data.progressText}
          beadCount={33}
          onIncrement={() => mauiCall("tasbih.increment").then(refresh)}
        />

        <button
          onClick={() => mauiCall("tasbih.reset").then(refresh)}
          className="inline-flex items-center gap-2 rounded-full border border-border bg-card px-4 py-2 text-sm font-medium hover:bg-muted"
        >
          <RotateCcw className="h-4 w-4" />
          {t("reset")}
        </button>
      </Card>

      <Card>
        <div className="mb-2 text-sm font-semibold">{t("presets")}</div>

        <Picker
          value={data.selectedPresetId}
          onChange={(id) =>
            mauiCall("tasbih.selectPreset", { id }).then(refresh)
          }
        >
          {data.presets.map((p) => (
            <option
              key={p.id}
              value={p.id}
              disabled={
                !data.isPresetSelectionEnabled &&
                p.id !== data.selectedPresetId
              }
            >
              {p.name}
            </option>
          ))}
        </Picker>

        {!data.isPresetSelectionEnabled && (
          <p className="mt-2 text-xs text-muted-foreground">
            {t("resetToChangePreset")}
          </p>
        )}

        <ul className="mt-3 space-y-1.5">
          {selectedPreset?.items.map((it, i) => (
            <li
              key={i}
              className="flex items-center justify-between rounded-md bg-muted/60 px-3 py-2 text-sm"
            >
              <span>{it.text}</span>
              <span className="text-xs font-semibold tabular-nums text-muted-foreground">
                ×{it.targetCount}
              </span>
            </li>
          ))}
        </ul>
      </Card>
    </div>
  );
}