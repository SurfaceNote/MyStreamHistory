import { TestBed } from '@angular/core/testing';

import { OverlaySettingsComponent } from './overlay-settings.component';
import { AuthService } from '../../../../auth/auth.service';
import { AnalyticsService } from '../../../../service/analytics.service';

describe('OverlaySettingsComponent', () => {
  let component: OverlaySettingsComponent;
  let analytics: jasmine.SpyObj<AnalyticsService>;

  beforeEach(() => {
    analytics = jasmine.createSpyObj<AnalyticsService>('AnalyticsService', ['trackEvent']);
    TestBed.configureTestingModule({
      imports: [OverlaySettingsComponent],
      providers: [
        { provide: AuthService, useValue: { getTwitchIdFromToken: () => '123' } },
        { provide: AnalyticsService, useValue: analytics },
      ],
    });
    component = TestBed.createComponent(OverlaySettingsComponent).componentInstance;
  });

  it('tracks a copied overlay link', async () => {
    spyOn(navigator.clipboard, 'writeText').and.resolveTo();
    await component.copyLink();
    expect(analytics.trackEvent).toHaveBeenCalledOnceWith('overlay_link_copy');
  });

  it('does not track a failed copy', async () => {
    spyOn(navigator.clipboard, 'writeText').and.rejectWith(new Error('denied'));
    await component.copyLink();
    expect(analytics.trackEvent).not.toHaveBeenCalled();
  });
});
