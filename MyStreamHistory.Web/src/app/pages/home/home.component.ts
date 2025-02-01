import { Component } from '@angular/core';
import { SmallListBlockComponent } from "../../components/blocks/small-list-block/small-list-block.component";
import { StreamerShortDTO } from '../../models/streamer-short.dto';

@Component({
  selector: 'app-home',
  imports: [SmallListBlockComponent],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent {
  streamers: StreamerShortDTO[] = [
    {id: 1, name: "test1", avatarUrl: "https://static-cdn.jtvnw.net/jtv_user_pictures/098fd779-669e-4c6c-a41e-88ec7ece943c-profile_image-50x50.png"},
    {id: 2, name: "test2", avatarUrl: "https://static-cdn.jtvnw.net/jtv_user_pictures/098fd779-669e-4c6c-a41e-88ec7ece943c-profile_image-50x50.png"},
    {id: 3, name: "test3", avatarUrl: "https://static-cdn.jtvnw.net/jtv_user_pictures/098fd779-669e-4c6c-a41e-88ec7ece943c-profile_image-50x50.png"},
    {id: 4, name: "test4", avatarUrl: "https://static-cdn.jtvnw.net/jtv_user_pictures/098fd779-669e-4c6c-a41e-88ec7ece943c-profile_image-50x50.png"},
    {id: 5, name: "test5", avatarUrl: "https://static-cdn.jtvnw.net/jtv_user_pictures/098fd779-669e-4c6c-a41e-88ec7ece943c-profile_image-50x50.png"},
    {id: 6, name: "test6", avatarUrl: "https://static-cdn.jtvnw.net/jtv_user_pictures/098fd779-669e-4c6c-a41e-88ec7ece943c-profile_image-50x50.png"},
    {id: 7, name: "test7", avatarUrl: "https://static-cdn.jtvnw.net/jtv_user_pictures/098fd779-669e-4c6c-a41e-88ec7ece943c-profile_image-50x50.png"},
    {id: 8, name: "test8", avatarUrl: "https://static-cdn.jtvnw.net/jtv_user_pictures/098fd779-669e-4c6c-a41e-88ec7ece943c-profile_image-50x50.png"},
    {id: 9, name: "test9", avatarUrl: "https://static-cdn.jtvnw.net/jtv_user_pictures/098fd779-669e-4c6c-a41e-88ec7ece943c-profile_image-50x50.png"},
    {id: 10, name: "test10", avatarUrl: "https://static-cdn.jtvnw.net/jtv_user_pictures/098fd779-669e-4c6c-a41e-88ec7ece943c-profile_image-50x50.png"},
  ];
}
