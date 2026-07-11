#!/usr/bin/env node
// One-shot build orchestrator so we stop re-debugging build steps every turn.
// Flags:
//   --love    : Lovable-managed build (skips DLL/contract regen; emits to .lovable-dist)
//   --dev     : development mode
//   --phone   : phone-embedded HTML build
//   --skip-dll: skip dotnet publish + contract regen even without --love
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const webRoot = resolve(__dirname, '..');
const repoRoot = resolve(webRoot, '..');

const args = new Set(process.argv.slice(2));
const love = args.has('--love') || args.has('-love');
const dev = args.has('--dev');
const phone = args.has('--phone');
const skipDll = love || args.has('--skip-dll');

function run(cmd, cargs, cwd = webRoot) {
  return new Promise((res, rej) => {
    const child = spawn(cmd, cargs, { cwd, stdio: 'inherit', shell: true });
    child.on('exit', (code) =>
      code === 0 ? res() : rej(new Error(`${cmd} ${cargs.join(' ')} failed (exit ${code})`)),
    );
  });
}

async function main() {
  console.log(`[build-all] mode=${love ? 'love' : phone ? 'phone' : 'web'} dev=${dev} skipDll=${skipDll}`);

  if (!skipDll) {
    const bridgeCsproj = resolve(repoRoot, 'PrayAdFree.WebBridge', 'PrayAdFree.WebBridge.csproj');
    if (existsSync(bridgeCsproj)) {
      console.log('[build-all] dotnet publish WebBridge (WASM)…');
      await run('dotnet', ['publish', bridgeCsproj, '-c', 'Release'], repoRoot);
    } else {
      console.warn('[build-all] WebBridge csproj not found, skipping WASM publish');
    }

    const contractsCsproj = resolve(repoRoot, 'tools', 'generate-web-contracts', 'GenerateWebContracts.csproj');
    if (existsSync(contractsCsproj)) {
      console.log('[build-all] regenerating web contracts…');
      await run('dotnet', ['run', '--project', contractsCsproj, '-c', 'Release'], repoRoot);
    } else {
      console.warn('[build-all] contract generator not found, skipping');
    }
  }

  const buildArgs = [];
  if (love) buildArgs.push('--love');
  if (dev) buildArgs.push('--dev');
  if (phone) buildArgs.push('--phone');
  console.log(`[build-all] node scripts/build.mjs ${buildArgs.join(' ')}`);
  await run('node', ['scripts/build.mjs', ...buildArgs]);

  console.log('[build-all] ✓ done');
}

main().catch((err) => {
  console.error('[build-all] ✗', err.message);
  process.exit(1);
});
