type Preset = {
  id: string;
  name: string;
  repeatMode: string;
  items: { text: string; targetCount: number }[];
};

const presets: Preset[] = [
  { id: "after-prayer", name: "After Prayer", repeatMode: "Sequence", items: [
    { text: "SubhanAllah", targetCount: 33 },
    { text: "Alhamdulillah", targetCount: 33 },
    { text: "Allahu Akbar", targetCount: 34 },
  ]},
  { id: "istighfar", name: "Istighfar", repeatMode: "Loop", items: [
    { text: "Astaghfirullah", targetCount: 100 },
  ]},
  { id: "salawat", name: "Salawat", repeatMode: "Loop", items: [
    { text: "Allahumma salli ala Muhammad", targetCount: 100 },
  ]},
];

let count = 0;
let selectedId = "after-prayer";

function currentItem(): { text: string; target: number; index: number } {
  const p = presets.find((x) => x.id === selectedId)!;
  let remaining = count;
  for (let i = 0; i < p.items.length; i++) {
    if (remaining < p.items[i].targetCount) return { text: p.items[i].text, target: p.items[i].targetCount, index: i };
    remaining -= p.items[i].targetCount;
  }
  const last = p.items[p.items.length - 1];
  return { text: last.text, target: last.targetCount, index: p.items.length - 1 };
}

export function getTasbihMock() {
  const cur = currentItem();
  return {
    count,
    currentPhrase: cur.text,
    progressText: `${count} / ${presets.find((p) => p.id === selectedId)!.items.reduce((a, b) => a + b.targetCount, 0)}`,
    isPresetSelectionEnabled: count === 0,
    selectedPresetId: selectedId,
    presets,
  };
}

export function tasbihIncrement() { count++; }
export function tasbihReset() { count = 0; }
export function tasbihSelectPreset(id: string) { if (count === 0) { selectedId = id; } }
