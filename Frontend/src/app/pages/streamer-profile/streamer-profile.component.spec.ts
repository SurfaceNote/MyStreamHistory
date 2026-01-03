import { ComponentFixture, TestBed } from '@angular/core/testing';

import { StreamerProfileComponent } from './streamer-profile.component';

describe('StreamerProfileComponent', () => {
  let component: StreamerProfileComponent;
  let fixture: ComponentFixture<StreamerProfileComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StreamerProfileComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(StreamerProfileComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
