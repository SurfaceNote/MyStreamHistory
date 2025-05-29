import { Component, Input } from '@angular/core';
import { StreamerShortDTO } from '../../../models/streamer-short.dto';
import { StreamerListType } from '../../../enums/streamer-list-type.enum';
import { StreamerService } from '../../../service/streamer.service';

@Component({
  selector: 'app-small-list-block-streamers',
  imports: [],
  templateUrl: './small-list-block-streamers.component.html',
  styleUrl: './small-list-block-streamers.component.scss'
})
export class SmallListBlockStreamersComponent {
  @Input() streamersListType: StreamerListType = StreamerListType.NewStreamers;
  streamers: StreamerShortDTO[] = [];
  isLoadingStreamers = true;
  fontIcon: string = '';
  title: string = 'Title';

  constructor(private streamerService: StreamerService){}

  ngOnInit(): void {
    this.setBlockProperties();
    this.loadStreamers();
  }

  setBlockProperties(): void {
    switch (this.streamersListType) {
      case StreamerListType.NewStreamers:
        this.fontIcon = 'fa-solid fa-headset';
        this.title = 'New Streamers';
        break;
      case StreamerListType.PopularStreamers:
        this.fontIcon = 'fa-solid fa-headset';
        this.title = 'Popular Streamers';
        break;
      case StreamerListType.LiveStreamers:
        this.fontIcon = 'fa-solid fa-video';
        this.title = 'Live Streamers';
        break;
      default:
        this.fontIcon = '';
        this.title = 'Title';
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
