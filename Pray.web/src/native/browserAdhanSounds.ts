const DATABASE = "prayer-companion-adhan-sounds";
const STORE = "sounds";
const SCHEMA_VERSION = 1;

type StoredAdhanSound = {
  id: string;
  label: string;
  fileName: string;
  mimeType: string;
  size: number;
  createdAt: number;
  blob: Blob;
};

let activeAudio: HTMLAudioElement | undefined;

export type BrowserAdhanSoundInfo = {
  id: string;
  label: string;
};

export async function pickAndStoreBrowserAdhanSound(labels: Record<string, string>): Promise<BrowserAdhanSoundInfo | null> {
  const file = await pickAudioFile();
  if (!file) return null;

  const generatedId = globalThis.crypto?.randomUUID?.() ?? `${Date.now()}_${Math.random().toString(16).slice(2)}`;
  const id = `adhan_custom_${generatedId}`.replace(/-/g, "");
  const fallbackLabel = file.name.replace(/\.[^.]+$/, "").trim() || labels.addCustomSound || "Custom sound";
  const label = window.prompt(labels.CustomAdhanNamePrompt || labels.addCustomSound || "Name this sound", fallbackLabel)?.trim() || fallbackLabel;
  await putSound({
    id,
    label,
    fileName: file.name,
    mimeType: file.type || "audio/*",
    size: file.size,
    createdAt: Date.now(),
    blob: file,
  });
  return { id, label };
}

export async function playBrowserAdhanSound(id: string, volume = 100): Promise<void> {
  stopActiveAudio();
  const custom = id.startsWith("adhan_custom_") ? await getSound(id) : undefined;
  if (!custom) {
    await playDefaultNotificationTone(volume);
    return;
  }

  const url = URL.createObjectURL(custom.blob);
  const audio = new Audio(url);
  activeAudio = audio;
  audio.volume = clampVolume(volume);
  audio.onended = () => URL.revokeObjectURL(url);
  audio.onerror = () => URL.revokeObjectURL(url);
  await audio.play();
}

export async function removeBrowserAdhanSound(id: string): Promise<void> {
  const db = await openDatabase();
  await new Promise<void>((resolve, reject) => {
    const transaction = db.transaction(STORE, "readwrite");
    transaction.objectStore(STORE).delete(id);
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error);
    transaction.onabort = () => reject(transaction.error);
  }).finally(() => db.close());
}

function pickAudioFile(): Promise<File | null> {
  return new Promise((resolve) => {
    const input = document.createElement("input");
    input.type = "file";
    input.accept = "audio/*,.mp3,.wav,.m4a,.aac,.ogg,.flac,.opus,.amr,.3gp,.mp4,.aiff,.aif,.caf";
    input.style.position = "fixed";
    input.style.left = "-10000px";
    input.style.top = "-10000px";
    document.body.appendChild(input);
    input.addEventListener("change", () => {
      const file = input.files?.[0] ?? null;
      input.remove();
      resolve(file);
    }, { once: true });
    input.addEventListener("cancel", () => {
      input.remove();
      resolve(null);
    }, { once: true });
    input.click();
  });
}

function putSound(sound: StoredAdhanSound): Promise<void> {
  return openDatabase().then((db) => new Promise<void>((resolve, reject) => {
    const transaction = db.transaction(STORE, "readwrite");
    transaction.objectStore(STORE).put(sound, sound.id);
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error);
    transaction.onabort = () => reject(transaction.error);
  }).finally(() => db.close()));
}

function getSound(id: string): Promise<StoredAdhanSound | undefined> {
  return openDatabase().then((db) => new Promise<StoredAdhanSound | undefined>((resolve, reject) => {
    const request = db.transaction(STORE, "readonly").objectStore(STORE).get(id);
    request.onsuccess = () => resolve(request.result as StoredAdhanSound | undefined);
    request.onerror = () => reject(request.error);
  }).finally(() => db.close()));
}

function openDatabase(): Promise<IDBDatabase> {
  return new Promise<IDBDatabase>((resolve, reject) => {
    const request = indexedDB.open(DATABASE, SCHEMA_VERSION);
    request.onupgradeneeded = () => {
      if (!request.result.objectStoreNames.contains(STORE)) request.result.createObjectStore(STORE);
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

async function playDefaultNotificationTone(volume: number): Promise<void> {
  const context = new AudioContext();
  const gain = context.createGain();
  gain.gain.value = clampVolume(volume) * 0.25;
  gain.connect(context.destination);

  const first = context.createOscillator();
  first.type = "sine";
  first.frequency.value = 660;
  first.connect(gain);
  first.start();
  first.stop(context.currentTime + 0.18);

  const second = context.createOscillator();
  second.type = "sine";
  second.frequency.value = 880;
  second.connect(gain);
  second.start(context.currentTime + 0.22);
  second.stop(context.currentTime + 0.42);

  await new Promise<void>((resolve) => window.setTimeout(resolve, 480));
  await context.close();
}

function stopActiveAudio(): void {
  if (!activeAudio) return;
  activeAudio.pause();
  activeAudio.src = "";
  activeAudio = undefined;
}

function clampVolume(volume: number): number {
  return Math.max(0, Math.min(1, Number.isFinite(volume) ? volume / 100 : 1));
}
