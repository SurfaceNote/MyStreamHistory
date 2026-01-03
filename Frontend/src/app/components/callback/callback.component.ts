import { Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { switchMap } from 'rxjs';
import { AuthService } from '../../auth/auth.service';

@Component({
  selector: 'app-callback',
  imports: [],
  templateUrl: './callback.component.html',
  styleUrl: './callback.component.scss'
})
export class CallbackComponent implements OnInit {
  private authService = inject(AuthService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  ngOnInit() {
    this.route.queryParams
      .pipe(
        switchMap((params) => {
          const code = params['code'];
          const state = params['state'];
          if (!code || !state) {
            return this.router.navigate(['/']);
          }
          return this.authService.handleTwitchCallback(code, state);
        })
      )
      .subscribe((success) => {
        if (success) {
          this.router.navigate(['/']);
        } else {
          this.router.navigate(['/']);
        }
      });
    }
}
