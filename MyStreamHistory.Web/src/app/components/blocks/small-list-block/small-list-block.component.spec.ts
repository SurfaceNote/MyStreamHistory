import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SmallListBlockComponent } from './small-list-block.component';

describe('SmallListBlockComponent', () => {
  let component: SmallListBlockComponent;
  let fixture: ComponentFixture<SmallListBlockComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SmallListBlockComponent]
    })
      .compileComponents();

    fixture = TestBed.createComponent(SmallListBlockComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
