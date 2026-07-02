import { spawn } from 'node:child_process';
import { readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';

const args = new Set(process.argv.slice(2));
const phone = args.has('--phone');

const phoneBridgeBootstrap = `<script>
(function(){
  if (window.mauiWebber) return;
  var callbacks = {};

  function sendMessage(message) {
    var request = encodeURIComponent(JSON.stringify(message));
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

  window.mauiWebber = {
    call: function(method, payload) {
      var id = Date.now().toString(36) + Math.random().toString(36).slice(2);
      var message = { id: id, method: method, payload: payload || {} };
      return new Promise(function(resolve) {
        callbacks[id] = resolve;
        sendMessage(message);
      });
    },
    __resolve: function(id, response) {
      var callback = callbacks[id];
      if (callback) {
        delete callbacks[id];
        callback(response);
      }
    },
    __drain: function() {
      return '[]';
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

await run('vite', ['build', ...(phone ? ['--mode', 'phone'] : [])]);
if (phone) {
  const indexPath = resolve(process.cwd(), 'dist', 'index.html');
  const html = await readFile(indexPath, 'utf8');
  let embeddedHtml = html.replace(/\s+crossorigin(?=[\s>])/g, '');

  const scriptMatch = embeddedHtml.match(/<script\s+type="module"\s+src="([^"]+)"><\/script>/);
  if (scriptMatch) {
    const scriptPath = resolve(process.cwd(), 'dist', scriptMatch[1].replace(/^\.\//, ''));
    const script = (await readFile(scriptPath, 'utf8'))
      .replaceAll('import.meta.url', 'document.baseURI')
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
