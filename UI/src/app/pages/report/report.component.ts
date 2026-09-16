import { CommonModule } from '@angular/common';
import { Component, OnInit, AfterViewInit, ViewChild, ChangeDetectorRef, Optional } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, MatPaginator } from '@angular/material/paginator';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatOptionModule } from '@angular/material/core';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

import { MainserviceService } from '../service/mainservice.service';
import { ThemeService } from '../service/theme.service';
import { SessionTimeoutService } from '../service/session-timeout.service';
import { FooterComponent } from '../footer/footer.component';
import { Subscription } from 'rxjs';

import { DateAdapter, MAT_DATE_FORMATS } from '@angular/material/core';
import { CustomDateAdapter, CUSTOM_DATE_FORMATS } from '../service/custom-date-adapter';

import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';

@Component({
  selector: 'app-report',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTableModule,
    MatIconModule,
    MatPaginatorModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatOptionModule,
    MatDialogModule,
    MatButtonModule,
    FooterComponent
  ],
  providers: [
    { provide: DateAdapter, useClass: CustomDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: CUSTOM_DATE_FORMATS }
  ],
  templateUrl: './report.component.html',
  styleUrl: './report.component.css'
})
export class ReportComponent implements OnInit, AfterViewInit {

  isDarkTheme = false;
  isLoading = false;
  hasSearched = false;
  private themeSubscription!: Subscription;

  // Filter values
  fromDate: Date = new Date();
  toDate: Date = new Date();
  selectedLocation: string = '';

  locations: string[] = ['BF1', 'BF2', 'BF3'];

  // Table
  displayedColumns: string[] = [
    'Tran_ID',
    'GrossDate',
    'GrossTime',
    'LaddleNumber',
    'SenderPlant',
    'NetWT',
    'GrossWT',
    'TareWT',
    'receiverPlant',
    'CastNumber'
  ];


  dataSource = new MatTableDataSource<any>([]);

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  constructor(
    private service: MainserviceService,
    private themeService: ThemeService,
    private sessionTimeoutService: SessionTimeoutService,
    private cdr: ChangeDetectorRef,
    @Optional() private dialogRef: MatDialogRef<ReportComponent>
  ) {}

  ngOnInit(): void {
    this.sessionTimeoutService.initSessionTimeout();
    this.themeSubscription = this.themeService.isDarkMode$.subscribe(
      (isDark) => { this.isDarkTheme = isDark; }
    );
  }

  ngAfterViewInit(): void {
    this.dataSource.paginator = this.paginator;
  }

  ngOnDestroy(): void {
    this.themeSubscription?.unsubscribe();
  }

  // ============================
  // Search
  // ============================

  searchReport(): void {
    if (!this.fromDate || !this.toDate) {
      return;
    }

    this.isLoading = true;
    this.hasSearched = true;

    this.service.getReportData().subscribe({
      next: (response: any) => {
        console.log('GetReportData Response:', response);

        let data: any[] = [];

        if (Array.isArray(response)) {
          data = response;
        } else if (response?.status && Array.isArray(response.data)) {
          data = response.data;
        } else if (response?.data) {
          data = Array.isArray(response.data) ? response.data : [];
        }

        // Filter by date range
        const startTime = new Date(this.fromDate).setHours(0, 0, 0, 0);
        const endTime = new Date(this.toDate).setHours(23, 59, 59, 999);

        data = data.filter((row: any) => {
          const rowTime = new Date(row.GrossDateTime).getTime();
          return rowTime >= startTime && rowTime <= endTime;
        });

        // Filter by location if selected
        if (this.selectedLocation) {
          data = data.filter((row: any) =>
            row.SenderPlant?.toUpperCase() === this.selectedLocation.toUpperCase()
          );
        }

        this.dataSource.data = data;
        this.isLoading = false;
        this.cdr.detectChanges();

        if (this.paginator) {
          this.paginator.firstPage();
        }
      },
      error: (err: any) => {
        console.error('TransactionReport Error:', err);
        this.dataSource.data = [];
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  // ============================
  // Export PDF
  // ============================

  exportPDF(): void {
    const data = this.dataSource.data;
    if (!data || data.length === 0) {
      return;
    }

    const pdf = new jsPDF('landscape', 'mm', 'a4');

    // Title
    pdf.setFontSize(18);
    pdf.setFont('helvetica', 'bold');
    pdf.text('Blast Furnace Production Report', 148, 15, { align: 'center' });

    // Subtitle with filter info
    pdf.setFontSize(9);
    pdf.setFont('helvetica', 'normal');
    let filterText = `Date: ${this.formatDateForDisplay(this.fromDate)} to ${this.formatDateForDisplay(this.toDate)}`;
    if (this.selectedLocation) {
      filterText += ` | Location: ${this.selectedLocation}`;
    }
    pdf.text(filterText, 148, 22, { align: 'center' });

    // Table
    autoTable(pdf, {
      startY: 28,
      head: [['Transaction No', 'Date', 'Time', 'Laddle No', 'Sender Plant', 'Net WT', 'Gross WT', 'Tare WT', 'Receiver Plant', 'Cast No']],
      body: data.map((row: any) => [
        row.Tran_ID,
        new Date(row.GrossDateTime).toLocaleDateString('en-GB'),
        new Date(row.GrossDateTime).toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit' }),
        row.LaddleNumber,
        row.SenderPlant,
        row.NetWT,
        row.GrossWT,
        row.TareWT,
        row.ReceiverPlant,
        row.CastNumber
      ]),
      theme: 'grid',
      styles: { fontSize: 9, cellPadding: 3, valign: 'middle', halign: 'center' },
      headStyles: { fontSize: 9, fontStyle: 'bold', fillColor: [30, 58, 138] }
    });

    // Page numbers
    const pageCount = (pdf as any).internal.getNumberOfPages();
    for (let i = 1; i <= pageCount; i++) {
      pdf.setPage(i);
      pdf.setFontSize(8);
      pdf.text(`Page ${i} of ${pageCount}`, 148, 200, { align: 'center' });
    }

    // File name
    let fileName = 'BlastFurnace_Report';
    if (this.selectedLocation) {
      fileName += `_${this.selectedLocation}`;
    }
    fileName += `_${this.formatDateForFile(this.fromDate)}_to_${this.formatDateForFile(this.toDate)}`;
    fileName += '.pdf';

    pdf.save(fileName);
  }

  // ============================
  // Clear Filters
  // ============================

  clearFilters(): void {
    this.fromDate = new Date();
    this.toDate = new Date();
    this.selectedLocation = '';
    this.dataSource.data = [];
    this.hasSearched = false;

    if (this.paginator) {
      this.paginator.firstPage();
    }
  }

  // ============================
  // Helpers
  // ============================

  private formatDateForApi(date: Date): string {
    const d = new Date(date);
    const year = d.getFullYear();
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private formatDateForDisplay(date: Date): string {
    const d = new Date(date);
    const day = String(d.getDate()).padStart(2, '0');
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const year = d.getFullYear();
    return `${day}-${month}-${year}`;
  }

  private formatDateForFile(date: Date): string {
    const d = new Date(date);
    const day = String(d.getDate()).padStart(2, '0');
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const year = d.getFullYear();
    return `${day}${month}${year}`;
  }

  // ============================
  // Dialog helpers
  // ============================

  get isDialog(): boolean {
    return !!this.dialogRef;
  }

  closeDialog(): void {
    if (this.dialogRef) {
      this.dialogRef.close();
    }
  }
}