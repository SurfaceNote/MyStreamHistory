import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SmallListBlockStreamersComponent } from './small-list-block-streamers.component';

describe('SmallListBlockStreamersComponent', () => {
  let component: SmallListBlockStreamersComponent;
  let fixture: ComponentFixture<SmallListBlockStreamersComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SmallListBlockStreamersComponent]
    })
      .compileComponents();

    fixture = TestBed.createComponent(SmallListBlockStreamersComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
