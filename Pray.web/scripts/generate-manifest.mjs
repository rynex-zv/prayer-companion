import { createHash } from 'node:crypto';
import { readdir, readFile, writeFile } from 'node:fs/promises';
import { join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = new URL('../dist/', import.meta.url);
const rootPath = fileURLToPath(root);
const target = process.argv.includes('--phone') ? 'phone' : 'web';

async function walk(dir) {
  const entries = await readdir(dir, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) {
      files.push(...await walk(path));
    } else if (entry.name !== 'webber-manifest.json') {
      files.push(path);
    }
  }
  return files;
}

const files = [];
for (const file of await walk(rootPath)) {
  const bytes = await readFile(file);
  files.push({
    path: relative(rootPath, file).replaceAll('\\', '/'),
    sha256: createHash('sha256').update(bytes).digest('hex')
  });
}

files.sort((a, b) => a.path.localeCompare(b.path));

await writeFile(
  join(rootPath, 'webber-manifest.json'),
  JSON.stringify({
    version: new Date().toISOString().replace(/[-:.TZ]/g, ''),
    target,
    entry: 'index.html',
    files
  }, null, 2)
);
