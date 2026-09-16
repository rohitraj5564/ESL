import { Component, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-date-range-dialog',
  standalone: false,
  templateUrl: './date-range-dialog.component.html',
  styleUrl: './date-range-dialog.component.css'
})
export class DateRangeDialogComponent {

  fromDate: Date | null = null;
  toDate: Date | null = null;
  maxDate: Date = new Date(); 

  constructor(
    public dialogRef: MatDialogRef<DateRangeDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: any
  ) { }

  onCancel(): void {
    this.dialogRef.close(null);
  }

  onConfirm(): void {
    if (!this.fromDate || !this.toDate) {
      Swal.fire({
        icon: 'warning',
        title: 'Missing Dates',
        text: 'Please select both From and To dates.',
        confirmButtonColor: '#005ca8'
      });
      return;
    }

    if (this.fromDate > this.toDate) {
      Swal.fire({
        icon: 'warning',
        title: 'Invalid Range',
        text: 'From date cannot be later than To date.',
        confirmButtonColor: '#005ca8'
      });
      return;
    }

    this.dialogRef.close({
      fromDate: this.fromDate,
      toDate: this.toDate
    });
  }
}