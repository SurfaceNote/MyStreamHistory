import { HttpParams } from '@angular/common/http';
import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { StreamerService } from '../../service/streamer.service';
import { StreamerShortDTO } from '../../models/streamer-short.dto';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-streamer-profile',
  imports: [],
  templateUrl: './streamer-profile.component.html',
  styleUrl: './streamer-profile.component.scss'
})
export class StreamerProfileComponent implements OnInit, OnDestroy {
  twitchId!: number;
  streamerShortDTO!: StreamerShortDTO;
  private routeSub: Subscription | null = null;

  private streamerService = inject(StreamerService)
  private route = inject(ActivatedRoute);

  ngOnInit(): void {
    this.routeSub = this.route.paramMap.subscribe(params => {
      const idParam = params.get('twitchId');
      if (idParam) {
        this.twitchId = +idParam;
        this.loadStreamer();
      }
    });
  }

  ngOnDestroy(): void {
    if (this.routeSub) {
      this.routeSub.unsubscribe();
    }
  }

  loadStreamer(): void {
    this.streamerService.getStreamerByTwitchId(this.twitchId).subscribe({
      next: (data: StreamerShortDTO) => {
        this.streamerShortDTO = data;
      },
      error: (err) => {
        console.error('Error loading streamer', err);
      }
    });
  }
}
