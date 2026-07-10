import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { HeaderComponent } from "./components/header/header.component";
import { filter } from 'rxjs/operators';
import { SeoData, SeoService } from './service/seo.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, HeaderComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  private router = inject(Router);
  private activatedRoute = inject(ActivatedRoute);
  private seo = inject(SeoService);

  ngOnInit(): void {
    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => {
        // Instant scroll to top without animation
        window.scrollTo(0, 0);
        let route = this.activatedRoute;
        while (route.firstChild) {
          route = route.firstChild;
        }
        const seoData = route.snapshot.data['seo'] as SeoData | undefined;
        if (seoData) {
          this.seo.update(seoData);
        }
      });
  }
}
