import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { mkdtemp, rm, mkdir, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, dirname, resolve, basename } from 'node:path';
import { startSsr } from './smoke-localization.mjs';
import { fixtureApi, streamId } from './localization-fixture.mjs';
const chromePath = process.env.CHROME_BIN || 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const runtime = await startSsr();
const profile = await mkdtemp(join(tmpdir(), 'msh-localization-browser-'));
const browser = spawn(chromePath, ['--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check','--remote-debugging-port=0','--user-data-dir=' + profile], { windowsHide:true, stdio:['ignore','pipe','pipe'] });
let socket, browserLog = '', sequence=0;
const pending = new Map(); const errors = []; const apiRequests = [];
function send(method, params={}) {
  const id=++sequence;
  return new Promise((resolve,reject) => {
    const timer=setTimeout(()=>{pending.delete(id);reject(new Error('CDP timeout: '+method));},15000);
    pending.set(id,{resolve:v=>{clearTimeout(timer);resolve(v);},reject}); socket.send(JSON.stringify({id,method,params}));
  });
}
async function evaluate(expression) {
  const result=await send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});
  if(result.exceptionDetails)throw new Error(JSON.stringify(result.exceptionDetails));
  return result.result.value;
}
async function until(expression) {
  for(let i=0;i<100;i++){ if(await evaluate(expression))return; await new Promise(resolve=>setTimeout(resolve,100)); }
  throw new Error('Browser condition timed out: '+expression+'\n'+await evaluate('JSON.stringify({path:location.pathname,text:document.body.innerText})')+'\n'+JSON.stringify(apiRequests.map(request=>request.url))+'\n'+errors.join('\n')+'\n'+runtime.log());
}
try {
  const debugUrl=await new Promise((resolve,reject)=>{
    const timer=setTimeout(()=>reject(new Error('Chrome did not start: '+browserLog)),15000);
    browser.stderr.on('data',chunk=>{browserLog+=chunk;const match=/DevTools listening on ws:\/\/127\.0\.0\.1:(\d+)\//.exec(browserLog);if(match){clearTimeout(timer);resolve('http://127.0.0.1:'+match[1]);}});
    browser.on('error',reject);
  });
  const page=await (await fetch(debugUrl+'/json/new?about:blank',{method:'PUT'})).json();
  socket=new WebSocket(page.webSocketDebuggerUrl);
  await new Promise((resolve,reject)=>{socket.addEventListener('open',resolve,{once:true});socket.addEventListener('error',reject,{once:true});});
  socket.addEventListener('message',async event=>{
    const message=JSON.parse(event.data);
    if(message.id){const call=pending.get(message.id);if(call){pending.delete(message.id);message.error?call.reject(new Error(JSON.stringify(message.error))):call.resolve(message.result);}return;}
    if(message.method==='Runtime.exceptionThrown')errors.push(JSON.stringify(message.params.exceptionDetails));
    if(message.method==='Runtime.consoleAPICalled' && message.params.type==='error')errors.push(message.params.args.map(x=>x.description||x.value).join(' '));
    if(message.method==='Fetch.requestPaused') {
      const {requestId,request}=message.params;
      try {
        if(request.url.startsWith('https://api.mystreamhistory.com/')){
          apiRequests.push(request); const response=fixtureApi(request.url);
          await send('Fetch.fulfillRequest',{requestId,responseCode:response.status||200,responseHeaders:[{name:'Content-Type',value:'application/json'},{name:'Access-Control-Allow-Origin',value:runtime.origin}],body:Buffer.from(JSON.stringify(response.body)).toString('base64')});
        } else if(request.url.startsWith(runtime.origin))await send('Fetch.continueRequest',{requestId});
        else await send('Fetch.fulfillRequest',{requestId,responseCode:200,body:''});
      } catch(error){errors.push(String(error));}
    }
  });
  await send('Runtime.enable'); await send('Page.enable'); await send('Fetch.enable',{patterns:[{urlPattern:'*'}]});
  const preload = await send('Page.addScriptToEvaluateOnNewDocument',{source:`try { localStorage.setItem('preferred_language','de'); localStorage.setItem('access_token','x.'+btoa(JSON.stringify({UserName:'BrowserUser',TwitchId:'123',role:'admin'}))+'.x'); } catch {}`});
  await send('Emulation.setDeviceMetricsOverride', { width:1440, height:1000, deviceScaleFactor:1, mobile:false });
  await send('Page.navigate',{url:runtime.origin+'/ru/'});
  await until(`!!document.querySelector('.profile-link') && document.querySelector('.intro h1')?.textContent.includes('Ваш мир')`);
  assert.equal(await evaluate('document.documentElement.lang'),'ru');
  assert.equal(apiRequests.length,0,'Initial public requests should use the anonymous SSR transfer cache');
  await send('Page.removeScriptToEvaluateOnNewDocument', { identifier: preload.identifier });
  if (process.env.LOCALIZATION_SCREENSHOTS) {
    await mkdir(process.env.LOCALIZATION_SCREENSHOTS, { recursive:true });
    for (const [name,width,height,mobile] of [['desktop',1440,1000,false],['mobile',390,844,true]]) {
      await send('Emulation.setDeviceMetricsOverride',{width,height,deviceScaleFactor:1,mobile});
      await evaluate('new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))');
      const screenshot = await send('Page.captureScreenshot',{format:'png'});
      await writeFile(join(process.env.LOCALIZATION_SCREENSHOTS,name+'.png'),Buffer.from(screenshot.data,'base64'));
      assert.ok(await evaluate('document.documentElement.scrollWidth <= innerWidth + 1'),name+': horizontal page overflow');
    }
    await send('Emulation.setDeviceMetricsOverride',{width:1440,height:1000,deviceScaleFactor:1,mobile:false});
  }
  // A real DOM click verifies that hydration attached handlers and that localized links work.
  await evaluate(`document.querySelector('.streamer-row').click()`);
  await until(`location.pathname==='/ru/profile/123' && !!document.querySelector('.profile-hero h1')`);
  await evaluate(`document.querySelector('.language-menu summary').click(); document.querySelector('.language-menu a[lang="cs"]').click()`);
  await until(`location.pathname==='/cs/profile/123' && document.querySelector('.section-nav')?.textContent.includes('Přehled')`);
  assert.equal(await evaluate('document.documentElement.lang'),'cs');
  assert.equal(await evaluate(`document.querySelector('link[rel="canonical"]').href`),'https://mystreamhistory.com/cs/profile/123');
  await evaluate(`document.querySelector('.stream-top').click()`);
  await until(`location.pathname==='/cs/stream/${streamId}' && !!document.querySelector('.audience-chart canvas')`);
  // A direct page load exercises stream SSR and browser canvas hydration.
  await send('Page.navigate',{url:runtime.origin+'/de/stream/'+streamId});
  await until(`document.documentElement.lang==='de' && !!document.querySelector('.profile-link') && !!document.querySelector('.audience-chart canvas')`);
  await send('Page.navigate',{url:runtime.origin+'/ru/news/partial'});
  await until(`location.pathname==='/ru/news/partial' && document.readyState==='complete' && !!document.querySelector('.article-page h1') && !!document.querySelector('.profile-link')`);
  assert.equal(await evaluate(`document.querySelector('link[hreflang="cs"]')`),null);
  assert.ok(await evaluate(`document.querySelector('.language-menu a[lang="cs"]').textContent.includes('Нет перевода')`));
  assert.equal(await evaluate(`document.querySelector('.language-menu a[lang="cs"]').getAttribute('href')`), '/cs/news');
  await evaluate(`document.querySelector('.language-menu summary').click(); document.querySelector('.language-menu a[lang="cs"]').click()`);
  await until(`location.pathname==='/cs/news' && !!document.querySelector('.article-card')`);
  // Manual choices survive reloads and direct foreign-language links.
  assert.ok(await evaluate(`document.cookie.includes('preferred_language=cs')`));
  await send('Network.enable');
  await send('Network.setExtraHTTPHeaders', { headers: { 'Accept-Language': 'ru-RU,ru;q=0.9' } });
  await evaluate(`localStorage.removeItem('preferred_language')`);
  await send('Page.navigate', { url: runtime.origin + '/?from=entry#games' });
  await until(`['/cs', '/cs/'].includes(location.pathname) && document.readyState==='complete' && !!document.querySelector('.profile-link')`);
  assert.equal(await evaluate('location.search'), '?from=entry');
  assert.equal(await evaluate('location.hash'), '#games');
  await evaluate(`document.querySelector('.language-menu summary').click(); document.querySelector('.language-menu a[lang="en"]').click()`);
  await until(`location.pathname==='/' && document.documentElement.lang==='en' && !!document.querySelector('.intro h1')`);
  await send('Page.navigate', { url: runtime.origin + '/' });
  await until(`location.pathname==='/' && document.readyState==='complete' && !!document.querySelector('.profile-link')`);
  assert.equal(await evaluate('document.documentElement.lang'), 'en');
  assert.ok(await evaluate(`document.cookie.includes('preferred_language=en') && localStorage.getItem('preferred_language')==='en'`));
  await send('Page.navigate', { url: runtime.origin + '/de/news/article' });
  await until(`location.pathname==='/de/news/article' && document.readyState==='complete' && !!document.querySelector('.profile-link')`);
  assert.equal(await evaluate('document.documentElement.lang'), 'de');
  assert.ok(await evaluate(`document.cookie.includes('preferred_language=en')`));
  // Without any saved choice, SSR selects the browser language before rendering.
  await evaluate(`document.cookie='preferred_language=; Path=/; Max-Age=0'; localStorage.removeItem('preferred_language')`);
  await send('Page.navigate', { url: runtime.origin + '/' });
  await until(`['/ru', '/ru/'].includes(location.pathname) && document.readyState==='complete' && !!document.querySelector('.profile-link')`);
  assert.equal(await evaluate('document.documentElement.lang'), 'ru');
  // Shared pages suggest the same object, without changing the direct URL automatically.
  await evaluate(`localStorage.setItem('preferred_language','ru'); document.cookie='preferred_language=ru; Path=/'`);
  await send('Page.navigate', { url: runtime.origin + '/es/profile/123?period=7d#games' });
  await until(`!!document.querySelector('.language-suggestion[lang="ru"]') && !!document.querySelector('.profile-link')`);
  assert.equal(await evaluate('location.pathname'), '/es/profile/123');
  assert.equal(await evaluate(`document.querySelector('.suggestion-open').getAttribute('href')`), '/ru/profile/123?period=7d#games');
  assert.ok(await evaluate(`document.querySelector('.suggestion-open').textContent.includes('Открыть на русском')`));
  await evaluate(`document.querySelector('.suggestion-dismiss').click()`);
  await until(`!document.querySelector('.language-suggestion')`);
  await send('Page.navigate', { url: runtime.origin + '/es/stream/' + streamId });
  await until(`!!document.querySelector('.audience-chart canvas') && !!document.querySelector('.profile-link')`);
  assert.equal(await evaluate(`document.querySelector('.language-suggestion')`), null, 'Dismissed pair should stay hidden after reload');
  await send('Page.navigate', { url: runtime.origin + '/de/profile/123?period=7d#games' });
  await until(`!!document.querySelector('.language-suggestion[lang="ru"]')`);
  await evaluate(`document.querySelector('.suggestion-open').click()`);
  await until(`location.pathname==='/ru/profile/123' && !document.querySelector('.language-suggestion')`);
  assert.equal(await evaluate('location.search + location.hash'), '?period=7d#games');
  assert.ok(await evaluate(`document.cookie.includes('preferred_language=ru') && localStorage.getItem('preferred_language')==='ru'`));
  await evaluate(`document.querySelector('.language-menu summary').click(); document.querySelector('.language-menu a[lang="cs"]').click()`);
  await until(`location.pathname==='/cs/profile/123' && !!document.querySelector('.profile-hero h1')`);
  assert.equal(await evaluate('location.search + location.hash'), '?period=7d#games', 'Language menu must preserve URL suffixes too');
  await evaluate(`document.querySelector('.language-menu summary').click(); document.querySelector('.language-menu a[lang="ru"]').click()`);
  await until(`location.pathname==='/ru/profile/123' && !!document.querySelector('.profile-hero h1')`);
  // An article suggestion opens its actual published translation; independent articles have no offer.
  await send('Page.navigate', { url: runtime.origin + '/news/partial?from=share#body' });
  await until(`!!document.querySelector('.article-page h1') && !!document.querySelector('.language-suggestion[lang="ru"]')`);
  await evaluate(`document.querySelector('.suggestion-open').click()`);
  await until(`location.pathname==='/ru/news/partial' && !!document.querySelector('.article-page h1') && !document.querySelector('.language-suggestion')`);
  assert.equal(await evaluate('location.search + location.hash'), '?from=share#body');
  await send('Page.navigate', { url: runtime.origin + '/de/news/solo' });
  await until(`!!document.querySelector('.article-page h1') && !!document.querySelector('.profile-link')`);
  assert.equal(await evaluate(`document.querySelector('.language-suggestion')`), null);
  assert.ok(apiRequests.every(request=>!Object.keys(request.headers).some(key=>key.toLowerCase()==='authorization')),'Public browser requests included a token');
  assert.deepEqual(errors,[],'Browser errors or hydration failures');
  console.log('Browser localization smoke passed: hydration, transfer cache, language switching, charts, preferences and dismissible language suggestions with published translations.');
} finally {
  try { if(socket?.readyState===WebSocket.OPEN)await send('Browser.close'); } catch { /* Child may already be closed. */ }
  socket?.close(); browser.kill(); runtime.child.kill(); runtime.server.close();
  await new Promise(resolve=>setTimeout(resolve,500));
  if(dirname(resolve(profile))===resolve(tmpdir()) && basename(profile).startsWith('msh-localization-browser-'))await rm(profile,{recursive:true,force:true,maxRetries:5,retryDelay:200});
}
