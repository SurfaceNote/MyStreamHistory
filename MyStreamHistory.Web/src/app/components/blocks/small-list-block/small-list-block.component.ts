import { Component, Input } from '@angular/core';
import { StreamerShortDTO } from '../../../models/streamer-short.dto';

@Component({
  selector: 'app-small-list-block',
  imports: [],
  templateUrl: './small-list-block.component.html',
  styleUrl: './small-list-block.component.scss'
})
export class SmallListBlockComponent {
  @Input() title: string = '';
  @Input() fontIcon: string = '';
  @Input() streamers: StreamerShortDTO[] = [];
}
