import { Component } from '@angular/core';
import { SmallListBlockComponent } from "../../components/blocks/small-list-block/small-list-block.component";

@Component({
  selector: 'app-home',
  imports: [SmallListBlockComponent],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent {

}
