import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminSession, adminHttp } from './admin-session';
import { environment } from '../../../src/environments/environment';

describe('Independent administrative HTTP session', () => {
  let session: AdminSession;
  let http: HttpTestingController;
  let client: HttpClient;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(withInterceptors([adminHttp])), provideHttpClientTesting()] });
    session = TestBed.inject(AdminSession); http = TestBed.inject(HttpTestingController); client = TestBed.inject(HttpClient);
  });
  afterEach(() => http.verify());
  it('rewrites reused feature requests to the same origin and strips bearer tokens', () => {
    session.token = 'csrf-value';
    client.post(environment.api_url + '/content/admin/articles', {}, { headers: { Authorization: 'Bearer old-twitch-token' } }).subscribe();
    const request = http.expectOne('/api/content/admin/articles');
    expect(request.request.headers.has('Authorization')).toBeFalse();
    expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('csrf-value');
    expect(request.request.withCredentials).toBeTrue();
    request.flush({});
  });
  it('does not send CSRF tokens to unrelated origins or on GET', () => {
    session.token = 'csrf-value';
    client.get('/api/auth/session').subscribe();
    const local = http.expectOne('/api/auth/session');
    expect(local.request.headers.has('X-CSRF-TOKEN')).toBeFalse(); local.flush({ username: 'operator' });
    client.get('https://unrelated.invalid/resource').subscribe();
    const external = http.expectOne('https://unrelated.invalid/resource');
    expect(external.request.headers.has('X-CSRF-TOKEN')).toBeFalse();
    expect(external.request.withCredentials).toBeFalse(); external.flush({});
  });
  it('refreshes the identity-bound CSRF token after login without storing the password', async () => {
    const promise = session.login('operator', 'local-test-password');
    http.expectOne('/api/auth/csrf').flush({ token: 'anonymous' });
    await Promise.resolve(); await Promise.resolve();
    const login = http.expectOne('/api/auth/login');
    expect(login.request.headers.get('X-CSRF-TOKEN')).toBe('anonymous');
    login.flush({ username: 'operator' });
    await Promise.resolve(); await Promise.resolve();
    http.expectOne('/api/auth/csrf').flush({ token: 'authenticated' });
    await promise;
    expect(session.username()).toBe('operator'); expect(session.token).toBe('authenticated');
  });
  it('rejects a missing or expired session', async () => {
    session.username.set('previous');
    const result = session.check();
    http.expectOne('/api/auth/session').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(await result).toBeFalse(); expect(session.username()).toBeNull();
  });
});
