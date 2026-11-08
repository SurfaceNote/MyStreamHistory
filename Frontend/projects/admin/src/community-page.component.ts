import { HttpClient } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CommunityComponent } from '../../../src/app/features/community/community.component';
import { StreamerShortDTO } from '../../../src/app/models/streamer-short.dto';
import { ApiResponse } from '../../../src/app/core/api/api-response.model';
import { unwrapData } from '../../../src/app/core/api/api-operators';

@Component({
  selector: 'admin-community-page', imports: [CommunityComponent],
  template: `<section class="panel-page"><p class="eyebrow">АУДИТОРИЯ</p><h1>Community</h1>
    <p class="muted">Административный просмотр. Настройки и исключения канала доступны его владельцу на основном сайте.</p>
    @if (error) { <p role="alert" class="notice error">{{ error }}</p><button (click)="load()">Повторить</button> }
    @if (!channelId) { <label>Выберите канал<select (change)="select($any($event.target).value)"><option value="">Выберите канал</option>
      @for (channel of channels; track channel.twitchId) { <option [value]="channel.twitchId">{{ channel.displayName }}</option> }
    </select></label> }
    @if (loaded && channels.length === 0) { <p>Каналы пока отсутствуют.</p> }
    </section>@if (channelId) { <app-community /> }`
})
export class CommunityPageComponent {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  channels: StreamerShortDTO[] = []; error = ''; loaded = false;
  get channelId(): string | null { return this.route.snapshot.queryParamMap.get('channelId'); }
  constructor() { this.load(); }
  select(id: string): void { if (id) void this.router.navigate([], { relativeTo: this.route, queryParams: { channelId: id } }); }
  load(): void {
    this.error = '';
    this.http.get<ApiResponse<StreamerShortDTO[]>>('/api/user/all').pipe(unwrapData<StreamerShortDTO[]>()).subscribe({
      next: channels => { this.channels = [...channels].sort((a, b) => a.displayName.localeCompare(b.displayName)); this.loaded = true; },
      error: () => { this.error = 'Не удалось загрузить список каналов.'; }
    });
  }
}
