import { Component } from '@angular/core';
import { SmallListBlockStreamersComponent } from "../../components/blocks/small-list-block-streamers/small-list-block-streamers.component";
import { StreamerShortDTO } from '../../models/streamer-short.dto';
import { HttpClient } from '@angular/common/http';
import { StreamerListType } from '../../enums/streamer-list-type.enum';

@Component({
  selector: 'app-home',
  imports: [SmallListBlockStreamersComponent],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent {
  streamers: StreamerShortDTO[] = [];
  isLoadingStreamers = true;
  StreamerListType = StreamerListType;

  constructor() {}
}
