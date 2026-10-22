import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { ActivatedRoute, Router, provideRouter, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { Chart } from 'chart.js';
import { StreamDetailComponent } from './stream-detail.component';
import { StreamerService } from '../../service/streamer.service';
import { SeoService } from '../../service/seo.service';

describe('Stream audience chart', () => {
  const data: any = {
    id: 'stream', twitchUserId: 1, streamerDisplayName: 'Demo', categories: [], viewers: [],
    startedAt: '2026-10-17T12:02:00Z', endedAt: '2026-10-17T12:17:00Z',
    audience: [
      { startedAt: '2026-10-17T12:02:00Z', averageViewers: 45.6, sampleCount: 5 },
      { startedAt: '2026-10-17T12:07:00Z', averageViewers: null, sampleCount: 0 },
      { startedAt: '2026-10-17T12:12:00Z', averageViewers: 0, sampleCount: 5 }
    ]
  };
  function create(details: any, platform = 'browser') {
    TestBed.configureTestingModule({ imports: [StreamDetailComponent], providers: [
      provideRouter([]),
      { provide: PLATFORM_ID, useValue: platform },
      { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ streamId: 'stream' })) } },
      { provide: StreamerService, useValue: { getStreamDetails: () => of(details) } },
      { provide: SeoService, useValue: { update: () => {} } }
    ] });
    const f = TestBed.createComponent(StreamDetailComponent); f.detectChanges(); return f;
  }
  it('renders averages on an elapsed-time axis with gaps and genuine zeroes', () => {
    const f = create(data);
    const canvas = f.nativeElement.querySelector('canvas');
    const chart = Chart.getChart(canvas)!;
    expect(chart.options.scales!['x']!.type).toBe('linear');
    expect(chart.data.datasets[0].data).toEqual([{ x: 0, y: 45.6 }, { x: 5, y: null }, { x: 10, y: 0 }] as any);
    expect(f.nativeElement.querySelector('table')).toBeNull();
    f.destroy(); expect(Chart.getChart(canvas)).toBeUndefined();
  });
  it('places category changes at their exact time and includes both categories in a mixed bucket', () => {
    const category = (id: string, game: string, from: string, to: string) => ({ streamCategoryId: id, twitchId: game, name: game, startedAt: from, endedAt: to, boxArtUrl: '', durationMinutes: 5, uniqueViewers: 1 });
    const f = create({ ...data, categories: [
      category('a', 'Chatting', '2026-10-17T12:02:00Z', '2026-10-17T12:07:00Z'),
      category('b', 'Chatting', '2026-10-17T12:07:00Z', '2026-10-17T12:09:00Z'),
      category('c', 'Game', '2026-10-17T12:09:00Z', '2026-10-17T12:13:00Z'),
      category('d', 'Chatting', '2026-10-17T12:13:00Z', '2026-10-17T12:17:00Z')
    ] });
    const segments = f.componentInstance.categorySegments;
    expect(segments.map(s => [s.start, s.end])).toEqual([[0, 7], [7, 11], [11, 15]]);
    expect(segments[0].color).toBe(segments[2].color);
    expect(f.componentInstance.categoriesInBucket(1)).toEqual(['Chatting', 'Game']);
    expect(f.componentInstance.categoriesInBucket(2)).toEqual(['Game', 'Chatting']);
    expect(f.nativeElement.querySelectorAll('.category-legend li').length).toBe(3);
    expect(f.nativeElement.textContent).toContain('0:07');
    f.destroy();
  });
  it('does not synthesize an audience curve from the last recorded viewer count', () => {
    const f = create({ ...data, audience: [], viewerCount: 100 });
    expect(f.nativeElement.querySelector('canvas')).toBeNull();
    expect(f.nativeElement.textContent).toContain('No audience history');
    f.destroy();
  });
  it('does not attempt to acquire a canvas context during server rendering', () => {
    const f = create(data, 'server');
    const canvas = f.nativeElement.querySelector('canvas');
    expect(canvas).not.toBeNull();
    expect(Chart.getChart(canvas)).toBeUndefined();
    f.destroy();
  });
});
