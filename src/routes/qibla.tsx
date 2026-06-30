import { createFileRoute } from "@tanstack/react-router";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";
import { Card, CardTitle } from "@/components/Card";
import { SegmentedControl } from "@/components/SegmentedControl";
import { QiblaCompass } from "@/components/QiblaCompass";
import { cn } from "@/lib/utils";
import { MapPin } from "lucide-react";

export const Route = createFileRoute("/qibla")({
  head: () => ({
    meta: [
      { title: "Qibla — Pray Ad Free" },
      { name: "description", content: "Find the Qibla direction with compass and map modes." },
    ],
  }),
  component: QiblaPage,
});

type Option = { id: string; label: string };
type Snapshot = {
  bearing: number; heading: number;
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
  const { data, refresh } = useSnapshot<Snapshot>("qibla.getSnapshot");
  if (!data) return <div className="h-80 animate-pulse rounded-xl bg-muted" />;
  const L = data.labels;

  const noPerm = data.state === "permissionMissing";

  return (
    <div className="flex flex-col gap-3">
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
          onChange={(id) => mauiCall("qibla.setHeadingMode", { mode: id }).then(refresh)}
          options={data.headingModes}
        />
        <SegmentedControl
          value={data.selectedReadingMode}
          onChange={(id) => mauiCall("qibla.setDisplayMode", { mode: id }).then(refresh)}
          options={data.readingModes}
        />
        <SegmentedControl
          value={data.selectedFilterMode}
          onChange={(id) => mauiCall("qibla.setVisualFilter", { mode: id }).then(refresh)}
          options={data.filterModes}
        />
      </div>

      {noPerm ? (
        <Card className="text-center">
          <div className="font-semibold">{L.permissionMissing}</div>
          <button
            onClick={() => mauiCall("settings.invoke", { action: "requestPermission", payload: { id: "location" } })}
            className="mt-2 rounded-full bg-primary px-4 py-2 text-sm font-medium text-primary-foreground"
          >
            {L.grantPermission}
          </button>
        </Card>
      ) : data.displayMode === "Map" ? (
        <Card className="flex h-72 items-center justify-center text-sm text-muted-foreground">
          Map preview (provided by native)
        </Card>
      ) : (
        <Card className="py-8">
          <QiblaCompass
            bearing={data.bearing}
            needleRotation={data.needleRotation}
            compassRotation={data.compassRotation}
            state={data.state}
            visualFilter={data.visualFilter}
            manual={data.state === "manual"}
            onDrag={(delta) => mauiCall("qibla.adjustManualHeading", { delta })}
            onDragEnd={() => mauiCall("qibla.commitManualHeading").then(refresh)}
          />
        </Card>
      )}
    </div>
  );
}
