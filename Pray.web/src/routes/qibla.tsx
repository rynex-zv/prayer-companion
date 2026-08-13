import { createFileRoute } from "@tanstack/react-router";
import { useProjection } from "@/hooks/useProjection";
import { executeCommand, executeProjectionCommand, nativeBackendReady, platformIntents } from "@/client/applicationClient";
import { Card, CardTitle } from "@/components/Card";
import { SegmentedControl } from "@/components/SegmentedControl";
import { QiblaCompass } from "@/components/QiblaCompass";
import { QiblaMap } from "@/components/QiblaMap";
import { cn } from "@/lib/utils";
import { MapPin } from "lucide-react";
import { PageLog } from "@/components/PageLog";
import { usePageLog } from "@/hooks/usePageLog";
import { useCallback, useEffect, useRef } from "react";

export const Route = createFileRoute("/qibla")({
  head: () => ({
    meta: [],
  }),
  component: QiblaPage,
});

type Option = { id: string; label: string };
type Snapshot = {
  bearing: number; heading: number; latitude: number; longitude: number;
  needleRotation: number; compassRotation: number;
  directionLabel: string; locationTitle: string; statusMessage: string;
  selectedHeadingMode: string; selectedReadingMode: string; selectedFilterMode: string;
  displayMode: "Compass" | "Map"; visualFilter: "None" | "Night" | "Contrast";
  state: "searching" | "permissionMissing" | "sensor" | "manual" | "aligned" | "map";
  isAligned?: boolean;
  headingModes: Option[]; readingModes: Option[]; filterModes: Option[];
  labels: Record<string, string>;
};

function QiblaPage() {
  usePageLog("qibla");
  const { data, setData } = useProjection<Snapshot>("qibla.getSnapshot");
  const lastSensorSent = useRef(0);
  const smoothedHeading = useRef<number | null>(null);
  const queuedHeading = useRef<number | null>(null);
  const headingRequestActive = useRef(false);

  const applyQibla = useCallback(async (method: string, payload?: unknown) => {
    const res = await executeProjectionCommand<Snapshot>(method, "qibla.snapshot", payload);
    if (res.ok) {
      setData(res.data);
    }
  }, [setData]);

  const queueSensorHeading = useCallback((heading: number) => {
    queuedHeading.current = heading;
    if (headingRequestActive.current) return;
    headingRequestActive.current = true;

    void (async () => {
      try {
        while (queuedHeading.current !== null) {
          const next = queuedHeading.current;
          queuedHeading.current = null;
          const response = await executeProjectionCommand<Snapshot>("qibla.updateHeading", "qibla.snapshot", { heading: next });
          if (response.ok) setData(response.data);
        }
      } finally {
        headingRequestActive.current = false;
      }
    })();
  }, [setData]);

  useEffect(() => {
    if (!data || data.selectedHeadingMode !== "auto") {
      return;
    }

    if (nativeBackendReady()) {
      let disposed = false;
      let pollId: number | undefined;
      void executeProjectionCommand<Snapshot>("qibla.startSensor", "qibla.snapshot").then((response) => {
        if (!disposed && response.ok) setData(response.data);
      });
      pollId = globalThis.setInterval(() => {
        void executeCommand<Snapshot>("qibla.getSnapshot").then((response) => {
          if (!disposed && response.ok) setData(response.data);
        });
      }, 250);
      return () => {
        disposed = true;
        if (pollId !== undefined) globalThis.clearInterval(pollId);
        void executeCommand("qibla.stopSensor");
      };
    }

    const acceptRawHeading = (rawHeading: number) => {
      const now = performance.now();
      if (now - lastSensorSent.current < 100) {
        return;
      }
      if (!Number.isFinite(rawHeading)) return;

      lastSensorSent.current = now;
      const normalized = ((rawHeading % 360) + 360) % 360;
      const current = smoothedHeading.current;
      if (current === null) {
        smoothedHeading.current = normalized;
        queueSensorHeading(normalized);
        return;
      }

      const delta = ((normalized - current + 540) % 360) - 180;
      if (Math.abs(delta) < 0.8) return;
      const next = ((current + delta * 0.18) % 360 + 360) % 360;
      smoothedHeading.current = next;
      queueSensorHeading(next);
    };

    const onOrientation = (event: DeviceOrientationEvent) => {
      const webkitHeading = (event as DeviceOrientationEvent & { webkitCompassHeading?: number }).webkitCompassHeading;
      const isAbsoluteEvent = event.type === "deviceorientationabsolute" || event.absolute === true;
      // A relative alpha starts from an arbitrary browser-defined zero and is
      // not a compass. Ignore it instead of presenting a precise-looking but
      // incorrect Qibla direction.
      if (!isAbsoluteEvent && typeof webkitHeading !== "number") return;
      const screenAngle = globalThis.screen?.orientation?.angle ?? 0;
      if (typeof webkitHeading === "number") acceptRawHeading(webkitHeading + screenAngle);
      else if (typeof event.alpha === "number") acceptRawHeading(360 - event.alpha + screenAngle);
    };

    type OrientationSensor = EventTarget & { quaternion?: readonly number[]; start(): void; stop(): void };
    const SensorConstructor = (globalThis as typeof globalThis & {
      AbsoluteOrientationSensor?: new (options: { frequency: number; referenceFrame: "device" }) => OrientationSensor;
    }).AbsoluteOrientationSensor;
    let absoluteSensor: OrientationSensor | undefined;
    let fallbackAttached = false;
    const attachFallback = () => {
      if (fallbackAttached) return;
      fallbackAttached = true;
      window.addEventListener("deviceorientationabsolute", onOrientation);
      window.addEventListener("deviceorientation", onOrientation);
    };
    if (SensorConstructor) {
      try {
        absoluteSensor = new SensorConstructor({ frequency: 10, referenceFrame: "device" });
        absoluteSensor.addEventListener("reading", () => {
          const q = absoluteSensor?.quaternion;
          if (!q || q.length < 4) return;
          const [x, y, z, w] = q;
          const yaw = Math.atan2(2 * ((w * z) + (x * y)), 1 - (2 * ((y * y) + (z * z)))) * 180 / Math.PI;
          const screenAngle = globalThis.screen?.orientation?.angle ?? 0;
          acceptRawHeading(360 - yaw + screenAngle);
        });
        absoluteSensor.addEventListener("error", attachFallback, { once: true });
        absoluteSensor.start();
      } catch {
        absoluteSensor = undefined;
      }
    }
    if (!absoluteSensor) attachFallback();
    return () => {
      window.removeEventListener("deviceorientationabsolute", onOrientation);
      window.removeEventListener("deviceorientation", onOrientation);
      absoluteSensor?.stop();
      queuedHeading.current = null;
      smoothedHeading.current = null;
    };
  }, [data?.selectedHeadingMode, queueSensorHeading, setData]);

  if (!data) return <div className="h-80 animate-pulse rounded-xl bg-muted" />;
  const L = data.labels;

  const noPerm = data.state === "permissionMissing";

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-end">
        <PageLog page="qibla" />
      </div>
      <Card>
        <CardTitle>{L.qiblaDirection}</CardTitle>
        <div className="mt-1 flex items-baseline justify-between">
          <div className="text-4xl font-bold tabular-nums" dir="ltr">{Math.round(data.bearing)}°</div>
          <div className="text-sm font-medium text-primary">{data.directionLabel}</div>
        </div>
        <div data-selector-name="qibla:device-heading" className="mt-1 text-xs text-muted-foreground" dir="ltr">
          {L.compass ?? "Compass"}: {Math.round(data.heading)}°
        </div>
        <div className="mt-1 flex items-center gap-1 text-xs text-muted-foreground">
          <MapPin className="h-3 w-3" />{data.locationTitle}
        </div>
        {data.statusMessage && (
          <div className={cn("mt-2 text-xs", data.isAligned ? "text-success" : "text-muted-foreground")}>
            {data.statusMessage}
          </div>
        )}
      </Card>

      <div className="flex flex-wrap gap-2">
        <SegmentedControl
          selectorPrefix="qibla:heading"
          value={data.selectedHeadingMode}
          onChange={(id) => applyQibla("qibla.setHeadingMode", { mode: id })}
          options={data.headingModes}
        />
        <SegmentedControl
          selectorPrefix="qibla:reading"
          value={data.selectedReadingMode}
          onChange={(id) => applyQibla("qibla.setDisplayMode", { mode: id })}
          options={data.readingModes}
        />
        <SegmentedControl
          selectorPrefix="qibla:filter"
          value={data.selectedFilterMode}
          onChange={(id) => applyQibla("qibla.setVisualFilter", { mode: id })}
          options={data.filterModes}
        />
      </div>

      {noPerm ? (
        <Card className="text-center">
          <div className="font-semibold">{L.permissionMissing}</div>
          <button
            onClick={() => platformIntents.requestPermission("location")}
            className="mt-2 rounded-full bg-primary px-4 py-2 text-sm font-medium text-primary-foreground"
          >
            {L.grantPermission}
          </button>
        </Card>
      ) : data.displayMode === "Map" ? (
        <Card className="p-2">
          <QiblaMap
            latitude={data.latitude}
            longitude={data.longitude}
            bearing={data.bearing}
            locationTitle={data.locationTitle}
            labels={{
              zoomIn: L.qiblaMapZoomIn,
              zoomOut: L.qiblaMapZoomOut,
              attribution: L.qiblaMapAttribution,
            }}
          />
        </Card>
      ) : (
        <Card className="py-8">
          <QiblaCompass
            bearing={data.bearing}
            needleRotation={data.needleRotation}
            compassRotation={data.compassRotation}
            state={data.state}
            visualFilter={data.visualFilter}
            cardinalLabels={{
              north: L.cardinalNorth,
              east: L.cardinalEast,
              south: L.cardinalSouth,
              west: L.cardinalWest,
            }}
            manual={data.state === "manual"}
            onDrag={() => undefined}
            onDragEnd={async (delta) => {
              if (delta !== 0) {
                await applyQibla("qibla.adjustManualHeading", { delta });
              }
              await applyQibla("qibla.commitManualHeading");
            }}
          />
        </Card>
      )}
    </div>
  );
}
