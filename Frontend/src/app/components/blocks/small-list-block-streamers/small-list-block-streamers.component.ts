import { TranslatePipe, LocalPathPipe } from '../../../i18n/translate.pipe';
import { TranslationKey } from '../../../i18n/translations';
import { LocaleService, LocalizedMessage } from '../../../i18n/locale.service';
import { Component, inject, Input } from '@angular/core';
import { StreamerShortDTO } from '../../../models/streamer-short.dto';
import { StreamerListType } from '../../../enums/streamer-list-type.enum';
import { StreamerService } from '../../../service/streamer.service';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-small-list-block-streamers',
  imports: [TranslatePipe, LocalPathPipe, RouterModule],
  templateUrl: './small-list-block-streamers.component.html',
  styleUrl: './small-list-block-streamers.component.scss'
})
export class SmallListBlockStreamersComponent {
  readonly locale = inject(LocaleService);
  @Input() streamersListType: StreamerListType = StreamerListType.NewStreamers;
  streamers: StreamerShortDTO[] = [];
  isLoadingStreamers = true;
  fontIcon: string = '';
  title: TranslationKey = 'ui.streamers';
  streamerService = inject(StreamerService)

  ngOnInit(): void {
    this.setBlockProperties();
    this.loadStreamers();
  }

  setBlockProperties(): void {
    switch (this.streamersListType) {
      case StreamerListType.NewStreamers:
        this.fontIcon = 'fa-solid fa-headset';
        this.title = 'directory.newStreamers';
        break;
      case StreamerListType.PopularStreamers:
        this.fontIcon = 'fa-solid fa-headset';
        this.title = 'directory.popularStreamers';
        break;
      case StreamerListType.LiveStreamers:
        this.fontIcon = 'fa-solid fa-video';
        this.title = 'directory.liveStreamers';
        break;
      default:
        this.fontIcon = '';
        this.title = 'ui.streamers';
    }
  }

  loadStreamers(): void {
    this.streamerService.getStreamers(this.streamersListType).subscribe({
      next: (data: StreamerShortDTO[]) => {
        console.log('Received streamers data:', data);
        this.streamers = data;
        this.isLoadingStreamers = false;
      },
      error: (err) => {
        console.error('Error loading streamers', err);
        this.isLoadingStreamers = false;
      }
    });
  }
}
