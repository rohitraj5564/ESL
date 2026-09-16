import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CastAssignmentReportComponent } from './cast-assignment-report.component';

describe('CastAssignmentReportComponent', () => {
  let component: CastAssignmentReportComponent;
  let fixture: ComponentFixture<CastAssignmentReportComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [CastAssignmentReportComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CastAssignmentReportComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
