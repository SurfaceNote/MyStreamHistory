import { Component } from '@angular/core';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { StreamerProfileComponent } from './app/pages/streamer-profile/streamer-profile.component';
import { StreamDetailComponent } from './app/pages/stream-detail/stream-detail.component';
import { ViewerStatsComponent } from './app/pages/viewer-stats/viewer-stats.component';
import { PlaythroughSettingsComponent } from './app/pages/settings/components/playthrough-settings/playthrough-settings.component';
import { StreamerService } from './app/service/streamer.service';
import { SettingsService } from './app/service/settings.service';
import { AuthService } from './app/auth/auth.service';
const names = ['Elden Ring', 'Hollow Knight', 'Stardew Valley', "Baldur’s Gate 3 — Digital Deluxe Edition", 'Cyberpunk 2077', 'Just Chatting', 'A very long game title to check wrapping on a narrow screen'];
const colors = ['#5d5437','#39465e','#41644a','#613f3e','#656235'];
const cover = (name: string, i: number) => 'data:image/svg+xml,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="144" height="192"><rect width="144" height="192" fill="'+colors[i % colors.length]+'"/><path d="M0 155 90 25l54 167Z" fill="#ffffff" opacity=".09"/><text x="14" y="165" fill="#ffffff" font-family="sans-serif" font-size="18">'+name.slice(0,12)+'</text></svg>');
const games = names.map((name,i)=>({ twitchCategoryId:String(i), twitchId:String(i), name, boxArtUrl: i===6?'':cover(name,i), totalHours:[43.5,21,15.4,10,6,2,1][i], streamsCount:3, averageViewers:15 }));
const owner = typeof location !== 'undefined' && new URLSearchParams(location.search).has('owner');
const playthroughs = [...games.slice(0,4), games[0], games[4], games[6]].map((g,i)=>({playthroughId:String(i),id:String(i),title:i===0?'Elden Ring 2026-09-20':g.name,status:['Playing','Completed','Planned','Dropped','Completed','Playing','Planned'][i],streamsCount:[7,12,0,3,9,4,0][i],runNumber:i===4?1:i===0?2:1,runsCount:i===0||i===4?2:1,twitchCategoryId:g.twitchId,twitchCategoryTwitchId:g.twitchId,gameName:g.name,boxArtUrl:g.boxArtUrl,totalHours:g.totalHours,uniqueViewersCount:124,firstStreamStartedAt:'2026-09-20T12:00:00Z',lastStreamStartedAt:'2026-10-15T12:00:00Z',autoAddNewStreams:i===0,streamCategories:games.slice(0,3).map((_,j)=>({streamCategoryId:String(j),categoryStartedAt:'2026-10-15T12:00:00Z',streamTitle:'Exploring the next area',gameName:g.name}))}));
const streamer={twitchId:1,displayName:'Preview channel',avatar:'/logo.png'};
const streams=games.slice(0,3).map((g,i)=>({id:String(i),streamerLogin:'preview',streamerDisplayName:streamer.displayName,streamerAvatarUrl:'/logo.png',twitchUserId:1,startedAt:'2026-10-15T12:00:00Z',endedAt:'2026-10-15T17:00:00Z',isLive:false,uniqueViewersCount:124,gameName:g.name,categories:(i===0?games:games.slice(i,i+1)).map((g,j)=>({...g,streamCategoryId:String(j),durationMinutes:60,uniqueViewers:42,startedAt:'2026-10-15T12:00:00Z',endedAt:'2026-10-15T13:00:00Z'})),viewers:[],audience:[]}));
const people = ['NightOwl','Vasya_Pupkin','kotik_v_tapkah','LongNameForAChatAccount123','Mira','Gleb'];
const viewer = (name: string, i: number, extra: any = {}) => ({twitchUserId:String(100+i),displayName:name,login:name.toLowerCase(),profileImageUrl:'/logo.png',minutesWatched:40+i*17,chatPoints:i*1.5,...extra});
const recap = {streamSessionId:'0',isLive:false,viewerDataAvailable:true,previousStreamsCount:31,comparisonStreamsCount:10,durationMinutes:300,averageDurationMinutes:215,peakViewers:14,averageViewers:11.3,chatAccounts:23,averageChatAccounts:17.4,newCount:9,returningCount:2,
 newViewers:people.slice(0,4).map((n,i)=>viewer(n,i)),returningViewers:[viewer('Mira',4,{missedStreams:6}),viewer('Gleb',5,{missedStreams:3})],
 streakViewers:[viewer('NightOwl',0,{streak:12}),viewer('kotik_v_tapkah',2,{streak:5})],topChatters:people.slice(0,5).map((n,i)=>viewer(n,i,{chatPoints:40-i*7})),
 playthroughs:[{playthroughId:'0',gameName:'Elden Ring',boxArtUrl:games[0].boxArtUrl,status:'Playing',streamNumber:7,streamsCount:7}]};
export const gamePreviewProviders = [
 {provide:ActivatedRoute,useValue:{paramMap:of(convertToParamMap({twitchId:'1',viewerTwitchId:'2',streamId:'0'}))}},
 {provide:AuthService,useValue:{getTwitchIdFromToken:()=>owner?'1':null,isAdmin:()=>false,clearNewChannel:()=>{}}},
 {provide:StreamerService,useValue:{
 getStreamerByTwitchId:()=>of(streamer),getRecentStreams:()=>of(streams),getStreamDetails:()=>of({...streams[0],streamTitle:'Finishing the main story — no spoilers please!',audience:[{startedAt:'2026-10-15T12:00:00Z',averageViewers:9,sampleCount:5},{startedAt:'2026-10-15T13:00:00Z',averageViewers:14,sampleCount:5},{startedAt:'2026-10-15T14:00:00Z',averageViewers:11,sampleCount:5}]}),getStreamRecap:()=>of(recap),getTopViewers:()=>of(['NightOwl','Vasya_Pupkin','kotik_v_tapkah','LongNameForAChatAccount123','Mira','Gleb','Anya','Timur'].map((n,i)=>({id:String(i),viewerId:String(i),streamerTwitchUserId:'1',minutesWatched:3200-i*370,earnedMsgPoints:420-i*45.5,experience:0,lastUpdatedAt:'',streamsWatched:20-i,watchedPercentage:80-i*7,favoriteCategories:[],recentStreams:[],viewer:{id:String(i),twitchUserId:String(100+i),displayName:n,login:n.toLowerCase(),profileImageUrl:'/logo.png',createdAt:''}}))),getSocialLinksByTwitchId:()=>of({socialLinks:[]}),
 getStreamerStatistics:()=>of({totalStreamsCount:32,totalUniqueGamesCount:games.length,totalStreamedHours:98,allGamesTotalHours:98,categories:games,playthroughs,dashboard:{period:'30d',totalStreamsCount:12,totalUniqueGamesCount:7,totalStreamedHours:48,streamedHoursByDay:Array.from({length:30},(_,i)=>({date:new Date(Date.UTC(2026,9,i+1)).toISOString().slice(0,10),value:[0,4,5.5,0,3,0,6,2,0,0,4.5,5,0,3.2,0,6.5,4,0,0,2,5,0,4,3,0,7,5,0,2,4][i]})),streamedHoursByWeek:[],chatPointsByDay:[],viewerCountByDay:[],topCategories:games.slice(0,4)}}),
 getViewerStats:()=>of({viewer:{displayName:'Preview viewer',profileImageUrl:'/logo.png',login:'preview'},minutesWatched:600,streamsWatched:5,watchedPercentage:42,earnedMsgPoints:34,experience:42,lastUpdatedAt:'2026-10-15T12:00:00Z',favoriteCategories:games.map(g=>({...g,minutesWatched:123})),recentStreams:[]})}},
 {provide:SettingsService,useValue:{getCollectionStatus:()=>of({streamsCount:32}),savePlaythrough:(r:any)=>of(r),getPlaythroughs:()=>of({playthroughs,availableGames:games.map((g,i)=>({...g,suggested:i===4||i===2,streamsCount:[3,2,4,2,5,8,1][i],streamCategories:playthroughs[0].streamCategories}))})}}
];
@Component({
 selector:'app-root',
 imports:[StreamerProfileComponent,StreamDetailComponent,ViewerStatsComponent,PlaythroughSettingsComponent],
 template:`<div class="preview-toolbar"><strong>Game layouts · test data</strong>@for (item of ['Profile','Stream','Viewer','Settings']; track item) {<button type="button" (click)="view=item">{{item}}</button>}</div>
 @switch(view) {
 @case('Profile') {<app-streamer-profile/>}
 @case('Stream') {<app-stream-detail/>}
 @case('Viewer') {<app-viewer-stats/>}
 @case('Settings') {<div class="preview-settings"><app-playthrough-settings/></div>}
 }`,
 styles:`.preview-toolbar {display:flex;gap:12px;align-items:center;flex-wrap:wrap;padding:16px var(--page-gutter);border-bottom:1px solid #343438;font-size:13px}.preview-toolbar button {background:#29292f;border:1px solid #44444b;color:#eee;padding:8px 14px;border-radius:4px;cursor:pointer}.preview-settings{padding:32px var(--page-gutter)}`
})
export class GameLayoutPreviewComponent { view='Profile'; }

