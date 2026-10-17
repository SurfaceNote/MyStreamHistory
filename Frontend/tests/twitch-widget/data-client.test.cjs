const { test } = require('node:test');
const assert = require('node:assert/strict');
const { adapt, createClient, artwork, httpsUrl, sortEntries, visibleSocial } = require('../../public/widgets/games/data-client.js');

const settings = { apiBaseUrl: 'https://api.mystreamhistory.com', siteBaseUrl: 'https://mystreamhistory.com' };
const profile = { twitchId: 106010980, displayName: 'kination', avatar: 'https://static-cdn.jtvnw.net/avatar.png' };
const statistics = {
  totalStreamedHours: 42.5,
  categories: [{ twitchCategoryId: 'category-1', twitchId: '3412', name: 'GTA III', totalHours: 30,
    boxArtUrl: 'https://static-cdn.jtvnw.net/ttv-boxart/3412-{width}x{height}.jpg' }],
  playthroughs: [
    { playthroughId: 'run-1', twitchCategoryId: 'category-1', gameName: 'GTA III', title: 'First run', status: 'Completed', totalHours: 12, lastStreamStartedAt: '2026-03-21T21:32:07Z' },
    { playthroughId: 'run-2', twitchCategoryId: 'category-1', gameName: 'GTA III', title: 'Second run', status: 'Completed', totalHours: 10, lastStreamStartedAt: '2026-04-01T18:00:00Z' },
    { playthroughId: 'run-3', twitchCategoryTwitchId: '3412', gameName: 'GTA III', title: 'Hardcore', status: 'Playing', totalHours: 8 }
  ]
};
const response = (data, status = 200) => ({ status, ok: status >= 200 && status < 300, json: async () => ({ success: true, data }) });

test('repeated playthroughs preserve category totals and each individual run', () => {
  const data = adapt(profile, statistics, {}, settings.siteBaseUrl);
  assert.equal(data.games.length, 1);
  assert.equal(data.games[0].hours, 30);
  assert.equal(data.playthroughs.length, 3);
  assert.deepEqual(data.playthroughs.map(item => item.hours), [12, 10, 8]);
  assert.equal(data.totalHours, 42.5); // Stream duration is not the sum of rounded game/category durations.
  assert.equal(data.games[0].status, 'playing');
  assert.equal(data.playthroughs[0].lastStreamAt, '2026-03-21T21:32:07Z');
  assert.equal(data.playthroughs[0].completedAt, undefined); // Last stream is not a completion timestamp.
});

test('box art template is expanded before URL parsing; unsafe links are rejected', () => {
  assert.equal(artwork(statistics.categories[0].boxArtUrl), 'https://static-cdn.jtvnw.net/ttv-boxart/3412-285x380.jpg');
  for (const url of ['javascript:alert(1)', 'data:text/html,x', 'http://example.com', 'https://user:password@example.com', 'not a url']) {
    assert.equal(httpsUrl(url), '');
  }
});

test('missing and malformed optional fields produce safe empty values', () => {
  const data = adapt(profile, { categories: [null, { name: 'Chat', totalHours: -2 }], playthroughs: null }, { socialLinks: [null, { socialNetworkType: 'Telegram', fullUrl: 'javascript:x' }] }, settings.siteBaseUrl);
  assert.equal(data.games[0].hours, 0);
  assert.equal(data.games[0].status, 'archived');
  assert.deepEqual(data.playthroughs, []);
  assert.deepEqual(data.streamer.socials, []);
  assert.equal(data.totalHours, 0);
  assert.throws(() => adapt(profile, {}, {}, settings.siteBaseUrl), { kind: 'invalid-response' });
});

test('all public requests use all-time statistics and omit credentials and JWTs', async () => {
  const requests = [];
  const signal = new AbortController().signal;
  const client = createClient(settings, async (url, options) => {
    requests.push({ url, options });
    return response(url.includes('/statistics') ? statistics : url.includes('/social-links') ? { socialLinks: [] } : url.includes('/recent-streams') ? [] : profile);
  });
  const data = await client.load('106010980', signal);
  assert.equal(data.streamer.name, 'kination');
  assert.equal(requests.length, 4);
  assert.ok(requests.some(item => item.url.endsWith('/recent-streams?count=100')));
  assert.ok(requests.some(item => item.url.endsWith('/statistics?period=all')));
  for (const { options } of requests) {
    assert.equal(options.credentials, 'omit');
    assert.deepEqual(options.headers, { Accept: 'application/json' });
    assert.equal(options.signal, signal);
  }
});

test('recent sort uses timestamps, retains API order for ties and puts unknown dates last', () => {
  const entries = [
    { name: 'Old, many hours', hours: 100, lastStreamAt: '2026-04-01T00:00:00Z' },
    { name: 'Unknown', hours: 200 },
    { name: 'Latest', hours: 1, lastStreamAt: '2026-10-01T00:00:00Z' },
    { name: 'Latest tie', hours: 2, lastStreamAt: '2026-10-01T03:00:00+03:00' }
  ];
  assert.deepEqual(sortEntries(entries, 'recent').map(item => item.name), ['Latest', 'Latest tie', 'Old, many hours', 'Unknown']);
  assert.equal(entries[0].name, 'Old, many hours');
  assert.equal(sortEntries(entries, 'hours')[0].name, 'Unknown');
});

test('recent streams give unconfigured categories their actual last streamed date', () => {
  const stats = { categories: [{ twitchId: '509658', name: 'Just Chatting', totalHours: 7 }], playthroughs: [] };
  const streams = [
    { startedAt: '2026-05-01T00:00:00Z', categories: [{ twitchId: '509658' }] },
    { startedAt: '2026-09-30T00:00:00Z', categories: [{ twitchId: '509658' }] }
  ];
  const data = adapt(profile, stats, {}, settings.siteBaseUrl, streams);
  assert.equal(data.games[0].lastStreamAt, '2026-09-30T00:00:00Z');
  assert.equal(data.games[0].hours, 7);
});

test('YouTube links are excluded by network name and by host, including renamed links', () => {
  for (const social of [
    { name: 'YouTube', url: 'https://example.com' },
    { name: 'Video', url: 'https://www.youtube.com/channel/x' },
    { name: 'Link', url: 'https://youtu.be/x' },
    { name: 'Embed', url: 'https://www.youtube-nocookie.com/x' }
  ]) assert.equal(visibleSocial(social), false);
  assert.equal(visibleSocial({ name: 'Telegram', url: 'https://t.me/kination_ru' }), true);
});

test('recent activity failure preserves the library and marks unavailable dates', async () => {
  const client = createClient(settings, async url => {
    if (url.includes('/recent-streams')) throw new Error('offline');
    return response(url.includes('/statistics') ? statistics : url.includes('/social-links') ? {} : profile);
  });
  const data = await client.load('106010980');
  assert.equal(data.recentUnavailable, true);
  assert.equal(data.games.length, 1);
});

test('social-link service failure does not discard the game history', async () => {
  const client = createClient(settings, async url => {
    if (url.includes('/social-links')) throw new Error('network unavailable');
    return response(url.includes('/statistics') ? statistics : profile);
  });
  const data = await client.load('106010980');
  assert.equal(data.socialsUnavailable, true);
  assert.equal(data.games.length, 1);
});

test('unknown channels and API errors are classified without displaying raw server text', async () => {
  const client = createClient(settings, async () => ({ status: 400, ok: false,
    json: async () => ({ success: false, errors: ['User with TwitchId 106010980 not found'] }) }));
  await assert.rejects(client.load('106010980'), { kind: 'not-found', status: 400 });
  const broken = createClient(settings, async () => ({ status: 503, ok: false, json: async () => { throw new Error('HTML'); } }));
  await assert.rejects(broken.load('106010980'), { kind: 'http-error', status: 503 });
});

test('invalid channel and mismatched channel responses cannot render another profile', async () => {
  let calls = 0;
  const client = createClient(settings, async url => {
    calls++;
    return response(url.includes('/statistics') ? statistics : url.includes('/social-links') ? {} : { ...profile, twitchId: 7 });
  });
  await assert.rejects(client.load('../settings'), { kind: 'invalid-channel' });
  assert.equal(calls, 0);
  await assert.rejects(client.load('106010980'), { kind: 'invalid-response' });
});
