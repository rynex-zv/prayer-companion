import { createFileRoute } from "@tanstack/react-router";
import { useProjection } from "@/hooks/useProjection";
import { executeCommand, platformIntents } from "@/client/applicationClient";
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

  const applyQibla = useCallback(async (method: string, payload?: unknown) => {
    const res = await executeCommand<Snapshot>(method, payload);
    if (res.ok) {
      setData(res.data);
    }
  }, [setData]);

  useEffect(() => {
    if (!data || data.selectedHeadingMode !== "auto") {
      return;
    }

    const onOrientation = (event: DeviceOrientationEvent) => {
      const now = performance.now();
      if (now - lastSensorSent.current < 250) {
        return;
      }

      const webkitHeading = (event as DeviceOrientationEvent & { webkitCompassHeading?: number }).webkitCompassHeading;
      const heading = typeof webkitHeading === "number"
        ? webkitHeading
        : typeof event.alpha === "number"
          ? 360 - event.alpha
          : null;

      if (heading === null || Number.isNaN(heading)) {
        return;
      }

      lastSensorSent.current = now;
      void applyQibla("qibla.updateHeading", { heading });
    };

    window.addEventListener("deviceorientationabsolute", onOrientation);
    window.addEventListener("deviceorientation", onOrientation);
    return () => {
      window.removeEventListener("deviceorientationabsolute", onOrientation);
      window.removeEventListener("deviceorientation", onOrientation);
    };
  }, [applyQibla, data]);

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
          value={data.selectedHeadingMode}
          onChange={(id) => applyQibla("qibla.setHeadingMode", { mode: id })}
          options={data.headingModes}
        />
        <SegmentedControl
          value={data.selectedReadingMode}
          onChange={(id) => applyQibla("qibla.setDisplayMode", { mode: id })}
          options={data.readingModes}
        />
        <SegmentedControl
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
