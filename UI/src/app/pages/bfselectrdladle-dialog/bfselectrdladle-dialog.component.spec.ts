import { ComponentFixture, TestBed } from '@angular/core/testing';

import { BfselectrdladleDialogComponent } from './bfselectrdladle-dialog.component';

describe('BfselectrdladleDialogComponent', () => {
  let component: BfselectrdladleDialogComponent;
  let fixture: ComponentFixture<BfselectrdladleDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [BfselectrdladleDialogComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(BfselectrdladleDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
