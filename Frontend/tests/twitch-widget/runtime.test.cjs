const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const runtime = fs.readFileSync(path.join(__dirname, '../../public/widgets/games/twitch.js'), 'utf8');
const flush = () => new Promise(resolve => setImmediate(resolve));

function setup(loader, withHelper = true) {
  const callbacks = {}, events = {}, calls = [], timers = new Map();
  const elements = {};
  let timerId = 0;
  const widget = Object.fromEntries(['setState', 'setStatus', 'setData', 'applyContextTheme']
    .map(method => [method, (...args) => calls.push({ method, args })]));
  const helper = Object.fromEntries(['onAuthorized', 'onContext', 'onVisibilityChanged', 'onError']
    .map(method => [method, callback => { callbacks[method] = callback; }]));
  const document = { visibilityState: 'visible',
    getElementById(id) { return elements[id] ||= { value: '', addEventListener(name, callback) { events[`${id}:${name}`] = callback; } }; },
    addEventListener(name, callback) { events[name] = callback; } };
  const window = { MyStreamHistoryWidget: widget,
    MyStreamHistorySettings: { refreshIntervalMs: 300000, requestTimeoutMs: 15000 },
    MyStreamHistoryData: { createClient: () => ({ load: loader }) },
    addEventListener(name, callback) { events[name] = callback; }, location: { reload() {} } };
  if (withHelper) window.Twitch = { ext: helper };
  vm.runInNewContext(runtime, { window, document, AbortController, Intl, Date,
    setTimeout(callback, ms) { const id = ++timerId; timers.set(id, { callback, ms }); return id; },
    clearTimeout(id) { timers.delete(id); } });
  return { callbacks, calls, timers, document, events };
}

test('standalone production viewer requests no data without a Twitch channel', () => {
  let requests = 0;
  const { calls } = setup(async () => { requests++; }, false);
  assert.equal(requests, 0);
  assert.ok(calls.some(call => call.method === 'setState' && call.args[0].title === 'Open this panel on Twitch'));
});

test('token refresh does not reload data; polling pauses when panel is hidden', async () => {
  let requests = 0;
  const state = setup(async () => { requests++; return { games: [] }; });
  state.callbacks.onAuthorized({ channelId: '106010980', token: 'never-forward-this' });
  await flush();
  state.callbacks.onAuthorized({ channelId: '106010980', token: 'refreshed' });
  assert.equal(requests, 1);
  assert.ok([...state.timers.values()].some(timer => timer.ms === 300000));
  state.callbacks.onVisibilityChanged(false);
  assert.equal(state.timers.size, 0);
  state.callbacks.onVisibilityChanged(true);
  assert.ok([...state.timers.values()].some(timer => timer.ms === 300000));
});

test('late response for a previous channel cannot overwrite the current channel', async () => {
  const pending = [];
  const state = setup((channelId, signal) => new Promise(resolve => pending.push({ channelId, signal, resolve })));
  state.callbacks.onAuthorized({ channelId: '1' });
  state.callbacks.onAuthorized({ channelId: '2' });
  assert.equal(pending[0].signal.aborted, true);
  pending[1].resolve({ streamer: { name: 'second' } });
  await flush();
  pending[0].resolve({ streamer: { name: 'first' } });
  await flush();
  const rendered = state.calls.filter(call => call.method === 'setData');
  assert.equal(rendered.length, 1);
  assert.equal(rendered[0].args[0].streamer.name, 'second');
});

test('automatic refresh failure keeps loaded data without a manual refresh control', async () => {
  let requests = 0;
  const state = setup(async () => { if (++requests > 1) throw new Error('offline'); return { games: [] }; });
  state.callbacks.onAuthorized({ channelId: '1' });
  await flush();
  const initialStates = state.calls.filter(call => call.method === 'setState').length;
  assert.equal(state.events['refreshData:click'], undefined);
  await [...state.timers.values()].find(timer => timer.ms === 300000).callback();
  assert.equal(state.calls.filter(call => call.method === 'setState').length, initialStates);
  assert.equal(state.calls.filter(call => call.method === 'setData').length, 1);
  const last = state.calls.filter(call => call.method === 'setStatus').at(-1);
  assert.equal(last.args[1].stale, true);
});

test('request timeout releases busy state and offers retry', async () => {
  const state = setup((id, signal) => new Promise((resolve, reject) => {
    signal.addEventListener('abort', () => reject(new Error('aborted')));
  }));
  state.callbacks.onAuthorized({ channelId: '1' });
  const timeout = [...state.timers.values()].find(timer => timer.ms === 15000);
  timeout.callback();
  await flush();
  assert.ok(state.calls.some(call => call.method === 'setState' && call.args[0].retry === true));
  assert.equal(state.calls.filter(call => call.method === 'setStatus').at(-1).args[0], 'Service temporarily unavailable');
});
