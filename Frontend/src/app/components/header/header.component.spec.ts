import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Subject, of } from 'rxjs';
import { HeaderComponent } from './header.component';
import { AuthService } from '../../auth/auth.service';

describe('Header authentication', () => {
  for (const token of ['stored-session', null]) {
    it(`shows only a placeholder until auth resolves to ${token ? 'profile' : 'login'}`, () => {
      const tokens = new Subject<string | null>();
      TestBed.configureTestingModule({
        imports: [HeaderComponent],
        providers: [
          provideRouter([]),
          {provide: AuthService, useValue: {
            getAccessTokenObservable: () => tokens.asObservable(),
            getUsernameObservable: () => of('kination'),
            getUsernameFromToken: () => 'kination',
            getTwitchIdFromToken: () => '106010980',
            isAdmin: () => false
          }}
        ]
      });
      const fixture = TestBed.createComponent(HeaderComponent);
      fixture.detectChanges();
      const element: HTMLElement = fixture.nativeElement;
      expect(element.querySelector('.auth-placeholder')).not.toBeNull();
      expect(getComputedStyle(element.querySelector('.auth-placeholder')!).visibility).toBe('hidden');
      expect(element.querySelector('app-login-component')).toBeNull();
      expect(element.querySelector('.user-menu')).toBeNull();
      tokens.next(token);
      fixture.detectChanges();
      expect(element.querySelector('.auth-placeholder')).toBeNull();
      expect(!!element.querySelector('app-login-component')).toBe(!token);
      expect(!!element.querySelector('.user-menu')).toBe(!!token);
      fixture.destroy();
    });
  }
});
