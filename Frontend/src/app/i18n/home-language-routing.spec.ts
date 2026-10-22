import { TestBed } from '@angular/core/testing';
import { NavigationEnd, Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { NEVER, filter, firstValueFrom, of, take } from 'rxjs';
import { routes } from '../app.routes';
import { AuthService } from '../auth/auth.service';
import { HeaderComponent } from '../components/header/header.component';
import { HomeComponent } from '../pages/home/home.component';
import { StreamerService } from '../service/streamer.service';
import { LocaleService } from './locale.service';
import { LANGUAGES } from './languages';

describe('Localized homepage routing', () => {
  beforeEach(() => {
    localStorage.removeItem('preferred_language');
    document.cookie = 'preferred_language=; Path=/; Max-Age=0';
    spyOnProperty(navigator, 'languages', 'get').and.returnValue(['en-US']);
    TestBed.configureTestingModule({ providers: [
      provideRouter(routes),
      { provide: AuthService, useValue: {
        getAccessTokenObservable: () => of(null), getUsernameObservable: () => of(null)
      } },
      // Keep the directory pending: routing must work independently of its API response.
      { provide: StreamerService, useValue: { getAllStreamers: () => NEVER } }
    ] });
  });
  afterEach(() => {
    localStorage.removeItem('preferred_language');
    document.cookie = 'preferred_language=; Path=/; Max-Age=0';
    document.documentElement.lang = 'en';
    document.head.querySelectorAll('link[rel="alternate"][hreflang], link[rel="canonical"]').forEach(link => link.remove());
  });

  it('opens the homepage when switching through all five languages using the actual menu', async () => {
    const harness = await RouterTestingHarness.create('/?from=menu#games');
    const header = TestBed.createComponent(HeaderComponent);
    header.detectChanges();
    const router = TestBed.inject(Router);
    const locale = TestBed.inject(LocaleService);
    for (const language of ['ru', 'cs', 'de', 'es', 'en']) {
      const end = firstValueFrom(router.events.pipe(filter(event => event instanceof NavigationEnd), take(1)));
      header.nativeElement.querySelector(`.language-menu a[lang="${language}"]`).click();
      await end;
      header.detectChanges();
      harness.detectChanges();
      expect(harness.routeDebugElement?.componentInstance).withContext(language).toBeInstanceOf(HomeComponent);
      expect(harness.routeNativeElement?.querySelector('.intro h1')).withContext(language).not.toBeNull();
      expect(locale.language()).toBe(language);
      expect(router.parseUrl(router.url).queryParams).toEqual({ from: 'menu' });
      expect(router.parseUrl(router.url).fragment).toBe('games');
    }
    header.destroy();
  });

  it('recognizes language roots with and without a trailing slash and still rejects unknown pages', async () => {
    const harness = await RouterTestingHarness.create();
    for (const { code } of LANGUAGES.filter(language => language.code !== 'en')) {
      for (const suffix of ['', '/']) {
        await harness.navigateByUrl('/' + code + suffix);
        expect(harness.routeDebugElement?.componentInstance).withContext(code + suffix).toBeInstanceOf(HomeComponent);
      }
    }
    await harness.navigateByUrl('/ru/does-not-exist');
    expect(harness.routeNativeElement?.textContent).toContain('Страница не найдена');
  });
});
