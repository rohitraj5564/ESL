import { Component, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';

export interface DialogData {
  message?: string;
  IsDelete?: boolean;
  IsSave?: boolean;
  IsUpdate?: boolean;
  IsAlert?: boolean;
  IsSuccess?: boolean;
  IsError?: boolean;
  [key: string]: any;
}

@Component({
  selector: 'app-confirm-dialog-esl',
  templateUrl: './confirm-dialog.component.html',
  styleUrls: ['./confirm-dialog.component.css'],
  standalone: false
})
export class ConfirmDialogComponent {
  constructor(
    public dialogRef: MatDialogRef<ConfirmDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: DialogData
  ) {}

  close(result: boolean = true): void {
    this.dialogRef.close(result);
  }
}
