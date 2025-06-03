import { HttpParams } from '@angular/common/http';
import { Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { StreamerService } from '../../service/streamer.service';
import { StreamerShortDTO } from '../../models/streamer-short.dto';

@Component({
  selector: 'app-streamer-profile',
  imports: [],
  templateUrl: './streamer-profile.component.html',
  styleUrl: './streamer-profile.component.scss'
})
export class StreamerProfileComponent implements OnInit {
  twitchId!: number;
  streamerService = inject(StreamerService)
  streamerShortDTO!: StreamerShortDTO;
  route = inject(ActivatedRoute);

  ngOnInit(): void {
      this.route.paramMap.subscribe(params => {
        const idParam = params.get('twitchId');
        if (idParam) {
          this.twitchId = +idParam;
        }
      });

      this.loadStreamer();
  }

  loadStreamer(): void {
    this.streamerService.getStreamerByTwitchId(this.twitchId).subscribe({
      next: (data: StreamerShortDTO) => {
        console.log('Received streamer data:', data);
        this.streamerShortDTO = data;
      },
      error: (err) => {
        console.error('Error loading streamer', err);
      }
    });
  }
}
