import { Component } from '@angular/core';
import { SmallListBlockComponent } from "../../components/blocks/small-list-block/small-list-block.component";
import { StreamerShortDTO } from '../../models/streamer-short.dto';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-home',
  imports: [SmallListBlockComponent],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent {
  streamers: StreamerShortDTO[] = [];
  private apiUrl = 'https://localhost:7278/api/Streamer';

  constructor(private http: HttpClient) {}

  ngOnInit(): void {
    this.loadStreamers();
  }

  loadStreamers(): void {
    this.http.get<StreamerShortDTO[]>(this.apiUrl).subscribe({
      next: (data) => this.streamers = data,
      error: (err) => console.error("error loading streamers:", err)
    });
  }
}
