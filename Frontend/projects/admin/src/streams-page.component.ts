import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { EMPTY, Subject, catchError, distinctUntilChanged, map, startWith, switchMap, tap } from 'rxjs';
import { ApiResponse } from '../../../src/app/core/api/api-response.model';
import { unwrapData } from '../../../src/app/core/api/api-operators';

export interface AdminStream {
  id: string;
  streamerLogin: string;
  streamerDisplayName: string;
  streamerAvatarUrl: string | null;
  startedAt: string;
  endedAt: string | null;
  isLive: boolean;
  durationHours: number | null;
  categories: string[];
  uniqueViewers: number | null;
}
export interface AdminStreamsPage { items: AdminStream[]; page: number; pageSize: number; totalCount: number; }

@Component({
  selector: 'admin-streams-page',
  templateUrl: './streams-page.component.html',
  styleUrl: './streams-page.component.scss'
})
export class StreamsPageComponent {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly refresh = new Subject<void>();
  private readonly number = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 });
  private readonly date = new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'long', year: 'numeric' });
  private readonly clock = new Intl.DateTimeFormat('ru-RU', { hour: '2-digit', minute: '2-digit' });
  readonly timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
  data: AdminStreamsPage | null = null;
  groups: { day: string; streams: AdminStream[] }[] = [];
  loading = true;
  error = '';
  get totalPages(): number { return Math.max(1, Math.ceil((this.data?.totalCount ?? 0) / (this.data?.pageSize ?? 25))); }

  constructor() {
    this.route.queryParamMap.pipe(
      map(params => { const page = Number(params.get('page')); return Number.isSafeInteger(page) && page > 0 ? page : 1; }),
      distinctUntilChanged(),
      switchMap(page => this.refresh.pipe(startWith(undefined),
        tap(() => { this.loading = true; this.error = ''; }),
        switchMap(() => this.http.get<ApiResponse<AdminStreamsPage>>('/api/admin/streams', { params: { page, pageSize: 25 } }).pipe(
          unwrapData<AdminStreamsPage>(),
          catchError(() => { this.loading = false; this.error = 'Не удалось загрузить список стримов. Попробуйте ещё раз.'; return EMPTY; })
        )))),
      takeUntilDestroyed(inject(DestroyRef))
    ).subscribe(data => {
      this.data = data;
      const groups = new Map<string, AdminStream[]>();
      for (const stream of data.items) {
        const day = this.date.format(new Date(stream.startedAt));
        if (!groups.has(day)) groups.set(day, []);
        groups.get(day)!.push(stream);
      }
      this.groups = Array.from(groups, ([day, streams]) => ({ day, streams }));
      this.loading = false;
    });
  }
  retry(): void { this.refresh.next(); }
  goToPage(page: number): void {
    if (!this.loading && page >= 1 && page <= this.totalPages)
      void this.router.navigate([], { relativeTo: this.route, queryParams: { page }, queryParamsHandling: 'merge' });
  }
  time(value: string): string { return this.clock.format(new Date(value)); }
  duration(hours: number | null): string { return hours === null ? 'Нет данных' : `${this.number.format(hours)} ч`; }
  viewers(count: number | null): string { return count === null ? 'Нет данных' : this.number.format(count); }
  streamUrl(id: string): string { return `https://mystreamhistory.com/ru/stream/${encodeURIComponent(id)}`; }
}
