import { CommonModule } from '@angular/common';
import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { interval, Subscription } from 'rxjs';

import { MatTableModule } from '@angular/material/table';
import {
  MatPaginatorModule,
  PageEvent
} from '@angular/material/paginator';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';

import { ReportComponent } from '../report/report.component';

import { FooterComponent } from '../footer/footer.component';
import { ThemeService } from '../service/theme.service';
import { MainserviceService } from '../service/mainservice.service';
import { SessionTimeoutService } from '../service/session-timeout.service';
import { DateAdapter, MAT_DATE_FORMATS } from '@angular/material/core';
import { CustomDateAdapter, CUSTOM_DATE_FORMATS } from '../service/custom-date-adapter';

import Swal from 'sweetalert2';
import { Router } from '@angular/router';

@Component({
  selector: 'app-cast-assignment',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatTooltipModule,
    MatDialogModule,
    FooterComponent
  ],
  providers: [
    { provide: DateAdapter, useClass: CustomDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: CUSTOM_DATE_FORMATS }
  ],
  templateUrl: './cast-assignment.component.html',
  styleUrl: './cast-assignment.component.css'
})
export class CastAssignmentComponent implements OnInit, OnDestroy {

  isDarkTheme = false;
  private themeSubscription!: Subscription;
  isLoading = false;

  displayedColumns: string[] = [
    'Tran_ID',
    'GrossDate',
    'GrossTime',
    'LaddleNumber',
    'NetWT',
    'GrossWT',
    'TareWT',
    'SenderPlant',
    'CastNumber',
    'action'
  ];

  allData: any[] = [];
  filteredData: any[] = [];
  pagedData: any[] = [];

  pageSize = 5;
  pageIndex = 0;

  fromDate: Date | null = null;
  toDate: Date | null = null;

  maxFromDate = new Date();
  minToDate: Date | null = null;

  selectedLocation = 'Select All';

  locations: string[] = [
    'Select All',
    'BF1',
    'BF2',
    'BF3'
  ];

  constructor(
    private themeService: ThemeService,
    private service: MainserviceService,
    private sessionTimeoutService: SessionTimeoutService,
    private router: Router,
    private dialog: MatDialog
  ) {
    const today = new Date();

    this.fromDate = today;
    this.toDate = today;
    this.minToDate = today;
  }

  ngOnInit(): void {
    this.sessionTimeoutService.initSessionTimeout();

    this.themeSubscription = this.themeService.isDarkMode$.subscribe(
      (isDark) => {
        this.isDarkTheme = isDark;
      }
    );

    // Load data on init
    this.loadReportData();

    // Auto-refresh internally every 5 seconds without showing on UI
    this.autoRefreshSubscription = interval(5000).subscribe(() => {
      this.loadReportData(true);
    });
  }

  private autoRefreshSubscription?: Subscription;

  ngOnDestroy(): void {
    this.autoRefreshSubscription?.unsubscribe();
    this.themeSubscription?.unsubscribe();
  }

  // ============================
  // Load Data from API
  // ============================

  loadReportData(isSilent: boolean = false): void {
    if (!isSilent) {
      this.isLoading = true;
    }

    this.service.getHotmetalBooking().subscribe({
      next: (response: any) => {
        if (response?.status && response?.data) {
          this.allData = response.data;
          this.filteredData = [...this.allData];
          this.updatePagedData();
        } else if (!isSilent) {
          this.allData = [];
          this.filteredData = [];
          this.pagedData = [];
        }

        if (!isSilent) {
          this.isLoading = false;
        } else {
          console.log(`[Cast Assignment] Internal auto-refresh completed at ${new Date().toLocaleTimeString()}`);
        }
      },

      error: (err: any) => {
        if (!isSilent) {
          console.error('GetReportData Error:', err);
          this.isLoading = false;

          Swal.fire({
            icon: 'error',
            title: 'API Error',
            text: 'Unable to fetch report data',
            confirmButtonColor: '#2563eb',
            background: this.isDarkTheme ? '#0f172a' : '#ffffff',
            color: this.isDarkTheme ? '#ffffff' : '#0f172a'
          });
        }
      }
    });
  }

  // ============================
  // Theme
  // ============================

  changeTheme(theme: boolean): void {
    this.isDarkTheme = theme;
  }

  // ============================
  // Pagination
  // ============================

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.updatePagedData();
  }

  updatePagedData(): void {
    const start = this.pageIndex * this.pageSize;

    this.pagedData = this.filteredData.slice(
      start,
      start + this.pageSize
    );
  }

  // ============================
  // Search / Filter
  // ============================

  searchData(): void {
    if (!this.fromDate || !this.toDate) {
      Swal.fire({
        icon: 'warning',
        title: 'Validation',
        text: 'Please select both From Date and To Date',
        confirmButtonColor: '#2563eb',
        background: this.isDarkTheme ? '#0f172a' : '#ffffff',
        color: this.isDarkTheme ? '#ffffff' : '#0f172a'
      });
      return;
    }

    if (this.fromDate > this.toDate) {
      Swal.fire({
        icon: 'warning',
        title: 'Validation',
        text: 'From Date cannot be greater than To Date',
        confirmButtonColor: '#2563eb',
        background: this.isDarkTheme ? '#0f172a' : '#ffffff',
        color: this.isDarkTheme ? '#ffffff' : '#0f172a'
      });
      return;
    }

    const from = new Date(this.fromDate);
    from.setHours(0, 0, 0, 0);

    const to = new Date(this.toDate);
    to.setHours(23, 59, 59, 999);

    this.filteredData = this.allData.filter((item: any) => {
      const grossDate = new Date(item.GrossDateTime);

      const locationMatch =
        this.selectedLocation === 'Select All' ||
        item.SenderPlant?.trim()?.toUpperCase() ===
          this.selectedLocation.toUpperCase();

      return (
        grossDate >= from &&
        grossDate <= to &&
        locationMatch
      );
    });

    this.pageIndex = 0;
    this.updatePagedData();
  }

  // ============================
  // Clear Filter
  // ============================

  clearFilter(): void {
    const today = new Date();

    this.fromDate = today;
    this.toDate = today;
    this.selectedLocation = 'Select All';

    this.filteredData = [...this.allData];

    this.pageIndex = 0;
    this.updatePagedData();
  }

  // ============================
  // Update Cast Number
  // ============================

  editCastNo(row: any): void {
    if (!row.CastNumber || row.CastNumber.trim() === '') {
      Swal.fire({
        icon: 'warning',
        title: 'Validation',
        text: 'Please enter a Cast Number',
        confirmButtonColor: '#2563eb',
        background: this.isDarkTheme ? '#0f172a' : '#ffffff',
        color: this.isDarkTheme ? '#ffffff' : '#0f172a'
      });
      return;
    }

    Swal.fire({
      title: 'Are you sure?',
      text: `Update Cast Number to "${row.CastNumber}" for Transaction ${row.Tran_ID}?`,
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Yes, Update',
      cancelButtonText: 'Cancel',
      confirmButtonColor: '#2563eb',
      cancelButtonColor: '#dc2626',
      background: this.isDarkTheme ? '#0f172a' : '#ffffff',
      color: this.isDarkTheme ? '#ffffff' : '#0f172a'
    }).then((result) => {
      if (!result.isConfirmed) {
        return;
      }

      const payload = {
        Tran_ID: row.Tran_ID,
        CastNumber: row.CastNumber.trim()
      };

      this.service.updateCastNumber(payload).subscribe({
        next: (response: any) => {
          console.log('UpdateCastNumber Response:', response);

          if (response?.status) {
            Swal.fire({
              icon: 'success',
              title: 'Success',
              text: response.message || 'Cast Number Updated Successfully',
              timer: 2000,
              showConfirmButton: false,
              background: this.isDarkTheme ? '#0f172a' : '#ffffff',
              color: this.isDarkTheme ? '#ffffff' : '#0f172a'
            });

            // Remove updated row from local data so next row becomes editable
            this.allData = this.allData.filter(
              (item: any) => item.Tran_ID !== row.Tran_ID
            );
            this.filteredData = this.filteredData.filter(
              (item: any) => item.Tran_ID !== row.Tran_ID
            );

            // Adjust page index if current page is now empty
            const totalPages = Math.ceil(this.filteredData.length / this.pageSize);
            if (this.pageIndex >= totalPages && this.pageIndex > 0) {
              this.pageIndex = totalPages - 1;
            }

            this.updatePagedData();
          } else {
            Swal.fire({
              icon: 'error',
              title: 'Failed',
              text: response?.message || 'Unable to update Cast Number',
              confirmButtonColor: '#dc2626',
              background: this.isDarkTheme ? '#0f172a' : '#ffffff',
              color: this.isDarkTheme ? '#ffffff' : '#0f172a'
            });
          }
        },

        error: (err: any) => {
          console.error('UpdateCastNumber Error:', err);

          Swal.fire({
            icon: 'error',
            title: 'API Error',
            text: 'Unable to connect to server',
            confirmButtonColor: '#dc2626',
            background: this.isDarkTheme ? '#0f172a' : '#ffffff',
            color: this.isDarkTheme ? '#ffffff' : '#0f172a'
          });
        }
      });
    });
  }

  // ============================
  // Open Report
  // ============================

  openReport(): void {
    this.dialog.open(
      ReportComponent,
      {
        width: '92vw',
        maxWidth: '92vw',
        height: '90vh',
        disableClose: false,
        autoFocus: false,
        panelClass: 'custom-report-dialog'
      }
    );
  }
}