// NGINX_BIN points at an existing portable nginx installation; no download is performed by this test.
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { readFile, writeFile } from 'node:fs/promises';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createServer } from 'node:net';
import https from 'node:https';
import { startSsr, smokeSsr } from './smoke-localization.mjs';
const root=fileURLToPath(new URL('../../',import.meta.url));
const binary=process.env.NGINX_BIN;
assert.ok(binary,'Set NGINX_BIN to an installed nginx executable.');
const prefix=dirname(resolve(binary)).replaceAll('\\','/')+'/';
const runtime=await startSsr();
async function freePort(){const s=createServer();await new Promise(r=>s.listen(0,'127.0.0.1',r));const p=s.address().port;await new Promise(r=>s.close(r));return p;}
const httpPort=await freePort(),httpsPort=await freePort();
let config=await readFile(new URL('../nginx/default.conf',import.meta.url),'utf8');
config=config.replace('listen 80;','listen '+httpPort+';').replace('listen 443 ssl http2;','listen '+httpsPort+' ssl http2;')
 .replace('/etc/letsencrypt/live/mystreamhistory.com/fullchain.pem',resolve(root,'Frontend/localhost.pem').replaceAll('\\','/'))
 .replace('/etc/letsencrypt/live/mystreamhistory.com/privkey.pem',resolve(root,'Frontend/localhost-key.pem').replaceAll('\\','/'))
 .replace('/usr/share/nginx/html',resolve(root,'Frontend/dist/my-stream-history.web/browser').replaceAll('\\','/'))
 .replaceAll('http://mystreamhistoryssr:4000',runtime.origin).replaceAll('https://api.mystreamhistory.com',runtime.url);
const configPath=resolve(root,'artifacts/nginx-localization-smoke.conf').replaceAll('\\','/');
await writeFile(configPath,`daemon off;\nmaster_process off;\nevents {}\nhttp { include ${prefix}conf/mime.types;\n${config}\n}`);
const args=['-p',prefix,'-c',configPath];
const validation=spawnSync(binary,[...args,'-t'],{windowsHide:true,encoding:'utf8'});
if(validation.status!==0){runtime.child.kill();runtime.server.close();throw new Error(validation.stderr);}
console.log(validation.stderr.trim());
const nginx=spawn(binary,args,{windowsHide:true,stdio:'ignore'});
const origin='https://127.0.0.1:'+httpsPort;
function request(url,options={}){return new Promise((resolve,reject)=>{const req=https.request(url,{rejectUnauthorized:false,headers:options.headers},res=>{let body='';res.on('data',chunk=>body+=chunk);res.on('end',()=>resolve({status:res.statusCode,headers:new Headers(Object.entries(res.headers).flatMap(([name,value])=>value===undefined?[]:[[name,String(value)]])),text:async()=>body}));});req.on('error',reject);req.end();});}
try {
 for(let i=0;i<100;i++){try{if((await request(origin+'/settings')).status===200)break;}catch{}if(i===99)throw new Error('Nginx did not start');await new Promise(r=>setTimeout(r,100));}
 await smokeSsr({...runtime,origin,request});
 const sitemap=await request(origin+'/sitemap.xml'); assert.equal(sitemap.status,200); assert.ok((await sitemap.text()).includes('/es/news'));
 const js=await readFile(new URL('../dist/my-stream-history.web/browser/index.csr.html',import.meta.url),'utf8');
 const asset=js.match(/src="((?!https?:)[^\"]+\.js)"/)[1];
 assert.equal((await request(origin+'/'+asset)).status,200);
 console.log('Nginx routing smoke passed against the production configuration with local upstreams and test certificates.');
} finally {
 spawnSync(binary,[...args,'-s','quit'],{windowsHide:true});nginx.kill();runtime.child.kill();runtime.server.close();
}
