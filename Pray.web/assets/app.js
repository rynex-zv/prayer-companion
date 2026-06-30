const $ = (id) => document.getElementById(id);
let renderTraceSent = false;
let firstRender = true;

if ('scrollRestoration' in history) {
  history.scrollRestoration = 'manual';
}

const demoSnapshot = {
  locationTitle: 'Amsterdam, Netherlands',
  hijriDate: '15 Muharram 1448',
  gregorianDate: new Date().toLocaleDateString(undefined, { weekday: 'long', day: '2-digit', month: 'short', year: 'numeric' }),
  nextPrayerName: 'Dhuhr',
  nextPrayerClock: '13:44',
  nextPrayerBaseClock: '',
  showNextPrayerBaseClock: false,
  nextPrayerDayLabel: 'Today',
  isRtl: false,
  labels: {
    nextPrayer: 'Next Prayer',
    timeLeft: 'Time Left',
    todayPrayerTimes: 'Today Prayer Times',
    iftar: 'Iftar',
    imsak: 'Imsak',
    refresh: 'Refresh',
    refreshing: 'Refreshing...',
    base: 'Base'
  },
  countdown: '01:23:45',
  statusMessage: 'Browser preview uses sample data. In the app this is filled by C#.',
  imsakTime: '03:16',
  iftarTime: '22:35',
  isImsakNext: false,
  isIftarNext: true,
  nextFastingCountdown: '09:12:30',
  todayTimings: [
    { id: 'Fajr', name: 'Fajr', time: '03:26', isNext: false },
    { id: 'Sunrise', name: 'Sunrise', time: '05:22', isNext: false },
    { id: 'Dhuhr', name: 'Dhuhr', time: '13:44', isNext: true },
    { id: 'Asr', name: 'Asr', time: '18:08', isNext: false },
    { id: 'Maghrib', name: 'Maghrib', time: '22:35', isNext: false },
    { id: 'Isha', name: 'Isha', time: '23:48', isNext: false }
  ]
};

async function callNative(method, payload = {}) {
  if (!window.mauiWebber || !window.mauiWebber.call) {
    return { ok: false, error: 'MauiWebber bridge is not ready.' };
  }
  return window.mauiWebber.call(method, payload);
}

function trace(name, detail = {}) {
  if (window.mauiWebber && window.mauiWebber.call) {
    window.mauiWebber.call('mauiWebber.trace', { name, at: performance.now(), ...detail });
  }
}

function text(id, value) {
  const element = $(id);
  if (element) element.textContent = value || '';
}

function render(snapshot) {
  const labels = snapshot.labels || {};
  document.documentElement.dir = snapshot.isRtl ? 'rtl' : 'ltr';
  text('nextPrayerLabel', labels.nextPrayer || 'NEXT PRAYER');
  text('timeLeftLabel', labels.timeLeft || 'TIME LEFT');
  text('todayPrayerTimesLabel', labels.todayPrayerTimes || 'TODAY PRAYER TIMES');
  text('iftarLabel', labels.iftar || 'Iftar');
  text('imsakLabel', labels.imsak || 'Imsak');
  text('refreshButton', labels.refresh || 'Refresh');
  text('locationTitle', snapshot.locationTitle);
  text('gregorianDate', snapshot.gregorianDate);
  text('hijriDate', snapshot.hijriDate);
  text('nextPrayerName', snapshot.nextPrayerName);
  text('nextPrayerClock', snapshot.nextPrayerClock);
  text('nextPrayerBaseClock', snapshot.showNextPrayerBaseClock ? `${labels.base || 'Base'} ${snapshot.nextPrayerBaseClock}` : '');
  text('countdown', snapshot.countdown);
  text('nextPrayerDayLabel', snapshot.nextPrayerDayLabel);
  text('iftarTime', snapshot.iftarTime);
  text('imsakTime', snapshot.imsakTime);
  text('iftarCountdown', snapshot.isIftarNext ? snapshot.nextFastingCountdown : '');
  text('imsakCountdown', snapshot.isImsakNext ? snapshot.nextFastingCountdown : '');
  text('statusMessage', snapshot.statusMessage);

  $('iftarCard').classList.toggle('active', !!snapshot.isIftarNext);
  $('imsakCard').classList.toggle('active', !!snapshot.isImsakNext);

  const timings = $('todayTimings');
  timings.innerHTML = '';
  for (const row of snapshot.todayTimings || []) {
    const item = document.createElement('div');
    item.className = `timing-row ${row.isNext ? 'next' : ''}`;
    item.innerHTML = `
      <span class="next-dot"></span>
      <span></span>
      <strong></strong>
      ${row.showBaseTime ? '<small></small>' : ''}
    `;
    item.children[1].textContent = row.name || '';
    item.children[2].textContent = row.time || '';
    if (row.showBaseTime && item.children[3]) item.children[3].textContent = row.baseTime || '';
    timings.appendChild(item);
  }
  if (!renderTraceSent) {
    renderTraceSent = true;
    trace('renderComplete', { timingCount: (snapshot.todayTimings || []).length });
  }
  if (firstRender) {
    firstRender = false;
    requestAnimationFrame(() => window.scrollTo(0, 0));
  }
}

async function load(refresh = false) {
  const button = $('refreshButton');
  if (refresh) {
    button.disabled = true;
    button.textContent = button.dataset.refreshingLabel || 'Refreshing...';
  }

  const response = await callNative(refresh ? 'today.refresh' : 'today.getSnapshot');
  button.disabled = false;

  if (!response.ok) {
    button.textContent = button.dataset.refreshLabel || 'Refresh';
    text('statusMessage', response.error || 'Native call failed.');
    return;
  }

  const labels = response.data?.labels || {};
  button.dataset.refreshLabel = labels.refresh || 'Refresh';
  button.dataset.refreshingLabel = labels.refreshing || 'Refreshing...';
  render(response.data || {});
  if (refresh) {
    trace('manualRefreshRendered');
  }
}

$('refreshButton').addEventListener('click', () => load(true));
window.addEventListener('mauiwebber:ready', () => {
  load(false);
  window.setInterval(() => load(false), 1000);
});

if (location.protocol === 'http:' || location.protocol === 'https:') {
  render(demoSnapshot);
}
