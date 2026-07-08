import { spawn } from 'node:child_process';
import { cp, readFile, rm, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';

const args = new Set(process.argv.slice(2));
const phone = args.has('--phone');

const phoneBridgeBootstrap = `<script>
(function(){
  if (window.mauiWebber) return;
  var appendJSlog = true;
  var callbacks = {};

  function normalizeLogArg(arg) {
    if (arg instanceof Error) {
      return { name: arg.name, message: arg.message, stack: arg.stack };
    }
    if (typeof arg === 'string' || typeof arg === 'number' || typeof arg === 'boolean' || arg == null) {
      return arg;
    }
    try {
      return JSON.parse(JSON.stringify(arg));
    } catch (_) {
      try { return String(arg); } catch (_) { return '[unserializable]'; }
    }
  }

  function appendJsLog(level, args) {
    if (!appendJSlog || !window.mauiWebber || typeof window.mauiWebber.call !== 'function') return;
    try {
      window.mauiWebber.call('mauiWebber.trace', {
        name: 'console.' + level,
        level: level,
        args: Array.prototype.slice.call(args || []).map(normalizeLogArg),
        location: String(window.location && window.location.href || ''),
        at: performance.now()
      });
    } catch (_) {
    }
  }

  ['debug', 'log', 'info', 'warn', 'error'].forEach(function(level) {
    var original = console[level];
    console[level] = function() {
      if (typeof original === 'function') {
        original.apply(console, arguments);
      }
      appendJsLog(level, arguments);
    };
  });

  window.addEventListener('error', function(event) {
    appendJsLog('error', [{
      message: event.message,
      source: event.filename,
      line: event.lineno,
      column: event.colno,
      error: normalizeLogArg(event.error)
    }]);
  });

  window.addEventListener('unhandledrejection', function(event) {
    appendJsLog('error', [{
      message: 'Unhandled promise rejection',
      reason: normalizeLogArg(event.reason)
    }]);
  });

  function receiveResponse(message) {
    var data = message && message.data;
    if (typeof data === 'string') {
      try { data = JSON.parse(data); } catch (_) { return; }
    }

    if (!data || data.__mauiWebberResponse !== true) return;
    window.__lastNativeResponse = data;
    window.mauiWebber.__resolve(data.id, data.response);
  }

  if (window.chrome && window.chrome.webview && typeof window.chrome.webview.addEventListener === 'function') {
    window.chrome.webview.addEventListener('message', receiveResponse);
  }

  function sendMessage(message) {
    var request = encodeURIComponent(JSON.stringify(message));
    if (window.mauiWebberNative && typeof window.mauiWebberNative.postMessage === 'function') {
      window.mauiWebberNative.postMessage(JSON.stringify(message));
      return;
    }

    if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
      window.chrome.webview.postMessage(JSON.stringify(message));
      return;
    }

    var frame = document.createElement('iframe');
    frame.style.display = 'none';
    frame.src = 'https://mauiwebber.local/rpc/' + request;
    document.documentElement.appendChild(frame);
    setTimeout(function() {
      if (frame.parentNode) frame.parentNode.removeChild(frame);
    }, 1000);
  }

  function traceBridgeResolve(id, found, pendingBefore, pendingAfter) {
    if (typeof id === 'string' && id.indexOf('trace-') === 0) return;
    sendMessage({
      id: 'trace-' + Date.now().toString(36) + Math.random().toString(36).slice(2),
      method: 'mauiWebber.trace',
      payload: {
        name: 'bridge.resolve',
        id: id,
        found: found,
        pendingBefore: pendingBefore,
        pendingAfter: pendingAfter,
        at: performance.now()
      }
    });
  }

  window.mauiWebber = {
    call: function(method, payload) {
      var id = Date.now().toString(36) + Math.random().toString(36).slice(2);
      var message = { id: id, method: method, payload: payload || {} };
      if (method === 'mauiWebber.trace') {
        sendMessage(message);
        return Promise.resolve({ ok: true, data: { accepted: true } });
      }
      return new Promise(function(resolve) {
        callbacks[id] = resolve;
        sendMessage(message);
      });
    },
    __resolve: function(id, response) {
      var pendingBefore = Object.keys(callbacks).length;
      var callback = callbacks[id];
      if (callback) {
        delete callbacks[id];
        traceBridgeResolve(id, true, pendingBefore, Object.keys(callbacks).length);
        callback(response);
        return;
      }
      traceBridgeResolve(id, false, pendingBefore, Object.keys(callbacks).length);
    },
    __drain: function() {
      return '[]';
    },
    __debugPending: function() {
      return Object.keys(callbacks);
    },
    __navigate: null,
    navigation: null
  };
  window.dispatchEvent(new CustomEvent('mauiwebber:ready'));
})();
</script>`;

function run(command, commandArgs) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, commandArgs, {
      cwd: process.cwd(),
      shell: true,
      stdio: 'inherit',
      env: {
        ...process.env,
        PRAY_WEB_TARGET: phone ? 'phone' : 'web'
      }
    });

    child.on('exit', (code) => {
      if (code === 0) {
        resolve();
        return;
      }

      reject(new Error(`${command} ${commandArgs.join(' ')} failed with exit code ${code}`));
    });
  });
}

if (!phone) {
  await run('dotnet', ['publish', '../PrayAdFree.WebBridge/PrayAdFree.WebBridge.csproj', '-c', 'Release']);
}

await run('vite', ['build', ...(phone ? ['--mode', 'phone'] : [])]);
await cp(resolve(process.cwd(), 'web.config'), resolve(process.cwd(), 'dist', 'web.config'));
if (!phone) {
  const wasmPublishRoot = resolve(process.cwd(), '..', 'PrayAdFree.WebBridge', 'bin', 'Release', 'net10.0', 'publish', 'wwwroot', '_framework');
  const wasmDistRoot = resolve(process.cwd(), 'dist', 'wasm', '_framework');
  await rm(resolve(process.cwd(), 'dist', 'wasm'), { recursive: true, force: true });
  await cp(wasmPublishRoot, wasmDistRoot, { recursive: true });
}
if (phone) {
  const indexPath = resolve(process.cwd(), 'dist', 'index.html');
  const html = await readFile(indexPath, 'utf8');
  let embeddedHtml = html.replace(/\s+crossorigin(?=[\s>])/g, '');

  const scriptMatch = embeddedHtml.match(/<script\s+type="module"\s+src="([^"]+)"><\/script>/);
  if (scriptMatch) {
    const scriptPath = resolve(process.cwd(), 'dist', scriptMatch[1].replace(/^\.\//, ''));
    const script = (await readFile(scriptPath, 'utf8'))
      .replaceAll('import.meta.url', 'document.baseURI')
      .replace(/new URL\("([^"/][^"]+)",document\.baseURI\)/g, 'new URL("assets/$1",document.baseURI)')
      .replaceAll('import.meta.env.MODE', JSON.stringify('phone'))
      .replaceAll('import.meta.env.VITE_BUILD_TARGET', JSON.stringify('phone'));
    embeddedHtml = embeddedHtml.replace(scriptMatch[0], '');
    embeddedHtml = embeddedHtml.replace(
      '</body>',
      () => `${phoneBridgeBootstrap}\n    <script>${script.replaceAll('</script', '<\\/script')}</script>\n  </body>`
    );
  }

  const styleMatch = embeddedHtml.match(/<link\s+rel="stylesheet"\s+href="([^"]+)">/);
  if (styleMatch) {
    const stylePath = resolve(process.cwd(), 'dist', styleMatch[1].replace(/^\.\//, ''));
    const style = (await readFile(stylePath, 'utf8')).replace(
      /url\((['"]?)\.\/([^)'"]+)\1\)/g,
      'url($1assets/$2$1)'
    );
    embeddedHtml = embeddedHtml.replace(styleMatch[0], () => `<style>${style.replaceAll('</style', '<\\/style')}</style>`);
  }

  await writeFile(indexPath, embeddedHtml, 'utf8');
}
await run('node', ['scripts/generate-manifest.mjs', ...(phone ? ['--phone'] : [])]);
await run('node', ['scripts/sync-maui-assets.mjs', ...(phone ? ['--phone'] : [])]);
