import { CommonModule } from '@angular/common';
import { Component, OnInit, OnDestroy } from '@angular/core';
import {
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';

import { interval, Subscription } from 'rxjs';

import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { MainserviceService } from '../service/mainservice.service';
import { SessionTimeoutService } from '../service/session-timeout.service';
import { ThemeService } from '../service/theme.service';
import { FooterComponent } from '../footer/footer.component';

import { DateAdapter, MAT_DATE_FORMATS } from '@angular/material/core';
import { MatNativeDateModule } from '@angular/material/core';
import { CustomDateAdapter, CUSTOM_DATE_FORMATS } from '../service/custom-date-adapter';
import { ProductionreportComponent } from '../productionreport/productionreport.component';
// adjust the path to match your actual folder structure

import Swal from 'sweetalert2';

@Component({
  selector: 'app-production-order-mapping',
  standalone: true,

  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatDatepickerModule,
    MatDialogModule,
    MatNativeDateModule,
    FooterComponent,
  ],

  providers: [
    { provide: DateAdapter, useClass: CustomDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: CUSTOM_DATE_FORMATS }
  ],

  templateUrl: './production-order-mapping.component.html',
  styleUrl: './production-order-mapping.component.css'
})
export class ProductionOrderMappingComponent implements OnInit, OnDestroy {

  mappingForm: FormGroup;

  isDarkTheme = true;

  private themeSubscription!: Subscription;
  private autoRefreshSubscription?: Subscription;

  productionUnits: string[] = [
    'BF-1',
    'BF-2',
    'BF-3'
  ];

  constructor(
    private fb: FormBuilder,
    private dialog: MatDialog,
    private sessionTimeoutService: SessionTimeoutService,
    private service: MainserviceService,
    private themeService: ThemeService
  ) {

    this.mappingForm = this.fb.group({

      fromDate: [
        new Date(),
        Validators.required
      ],

      toDate: [
        new Date(),
        Validators.required
      ],

      productionUnit: [
        '',
        Validators.required
      ],

      productionOrderNumber: [
        '',
        Validators.required
      ],

      batchPredecessor: [
        '',
        [
          Validators.required,
          Validators.maxLength(3)
        ]
      ]

    });
  }

  ngOnInit(): void {
    this.sessionTimeoutService.initSessionTimeout();
    this.themeSubscription = this.themeService.isDarkMode$.subscribe(
      (isDark: boolean) => {
        this.isDarkTheme = isDark;
      }
    );

    // Auto-refresh internally every 5 seconds without showing on UI
    this.autoRefreshSubscription = interval(5000).subscribe(() => {
      this.performInternalAutoRefresh();
    });
  }

  private performInternalAutoRefresh(): void {
    // 1. Silently notify connected report services/dialogs to refresh data
    this.service.triggerReportRefresh();

    // 2. If a production unit is selected and field has not been manually touched, sync prefix silently
    const unit = this.mappingForm.get('productionUnit')?.value;
    const predecessorControl = this.mappingForm.get('batchPredecessor');
    if (unit && predecessorControl && !predecessorControl.dirty) {
      this.service.getPrefixByLocation(unit).subscribe({
        next: (response: any) => {
          if (response?.data?.Prefix) {
            this.mappingForm.patchValue({
              batchPredecessor: response.data.Prefix
            }, { emitEvent: false });
          }
        },
        error: () => {} // Silent on background refresh
      });
    }
  }

  ngOnDestroy(): void {
    this.autoRefreshSubscription?.unsubscribe();
    if (this.themeSubscription) {
      this.themeSubscription.unsubscribe();
    }
  }

  // ============================
  // Theme
  // ============================

  changeTheme(isDark: boolean): void {
    this.isDarkTheme = isDark;
  }

  // ============================
  // Production Unit Change
  // ============================

  onProductionUnitChange(): void {

    const unit =
      this.mappingForm.get('productionUnit')?.value;

    if (!unit) {

      this.mappingForm.patchValue({
        batchPredecessor: ''
      });

      return;
    }

    this.service.getPrefixByLocation(unit).subscribe({

      next: (response: any) => {

        console.log('Prefix Response:', response);

        this.mappingForm.patchValue({
          batchPredecessor: response?.data?.Prefix || ''
        });

      },

      error: (err: any) => {

        console.error(
          'Error getting prefix:',
          err
        );

        this.mappingForm.patchValue({
          batchPredecessor: ''
        });

      }

    });
  }

  // ============================
  // Update / Save Data
  // ============================

  updateData(): void {

    if (this.mappingForm.invalid) {

      this.mappingForm.markAllAsTouched();

      Swal.fire({
        icon: 'warning',
        title: 'Validation Error',
        text: 'Please fill all required fields',
        confirmButtonColor: '#2563eb',
        background: '#0f172a',
        color: '#ffffff'
      });

      return;
    }

    Swal.fire({

      title: 'Are you sure?',

      text: 'Do you want to save this Production Order?',

      icon: 'question',

      showCancelButton: true,

      confirmButtonText: 'Yes, Save',

      cancelButtonText: 'Cancel',

      confirmButtonColor: '#2563eb',

      cancelButtonColor: '#dc2626',

      background: '#0f172a',

      color: '#ffffff'

    }).then((result) => {

      if (!result.isConfirmed) {
        return;
      }

      const formValue =
        this.mappingForm.value;

      const payload = {

        Production_Unit:
          formValue.productionUnit,

        Production_Order:
          formValue.productionOrderNumber,

        FromDate:
          this.formatDate(formValue.fromDate),

        Todate:
          this.formatDate(formValue.toDate),

        CastNo_Predecessor:
          formValue.batchPredecessor

      };

      console.log(
        'Production Order Payload:',
        payload
      );

      this.service.insertProductionOrdersMapping(payload).subscribe({
        next: (response: any) => {
          console.log('InsertProductionOrder Response:', response);

          if (response?.status) {
            Swal.fire({
              icon: 'success',
              title: 'Success',
              text: response.message || 'Production Order Saved Successfully',
              timer: 2000,
              showConfirmButton: false,
              background: '#0f172a',
              color: '#ffffff'
            });

            this.resetForm();
            this.service.triggerReportRefresh();
          } else {
            Swal.fire({
              icon: 'error',
              title: 'Failed',
              text: response?.message || 'Unable to save Production Order',
              confirmButtonColor: '#dc2626',
              background: '#0f172a',
              color: '#ffffff'
            });
          }
        },

        error: (err: any) => {
          console.error('InsertProductionOrder Error:', err);

          Swal.fire({
            icon: 'error',
            title: 'API Error',
            text: 'Unable to connect to server',
            confirmButtonColor: '#dc2626',
            background: '#0f172a',
            color: '#ffffff'
          });
        }
      });

    });
  }

  // ============================
  // Date Formatter
  // ============================

  private formatDate(date: Date): string {

    if (!date) {
      return '';
    }

    const selectedDate =
      new Date(date);

    const year =
      selectedDate.getFullYear();

    const month =
      String(
        selectedDate.getMonth() + 1
      ).padStart(2, '0');

    const day =
      String(
        selectedDate.getDate()
      ).padStart(2, '0');

    return `${year}-${month}-${day}`;
  }

  // ============================
  // Open Report
  // ============================

  openReport(): void {

    this.dialog.open(
      ProductionreportComponent,
      {

        width: '92vw',

        maxWidth: '92vw',

        height: '90vh',

        disableClose: false,

        autoFocus: false,

        panelClass:
          'custom-report-dialog'

      }
    );
  }

  // ============================
  // Reset Form
  // ============================

  resetForm(): void {

    this.mappingForm.reset({

      fromDate:
        new Date(),

      toDate:
        new Date(),

      productionUnit:
        '',

      productionOrderNumber:
        '',

      batchPredecessor:
        ''

    });

  }

  // ============================
  // Close Form
  // ============================

  closeForm(): void {

    console.log(
      'Close clicked'
    );

  }

}


// ==========================================
// Production Report Dialog Component
// ==========================================

@Component({

  selector:
    'app-production-report',

  standalone: true,

  template: ''

})
class Productionreport {}