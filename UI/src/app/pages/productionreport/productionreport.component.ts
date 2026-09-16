import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, OnInit, OnDestroy, ViewChild } from '@angular/core';
import { Subscription } from 'rxjs';

import { FormsModule } from '@angular/forms';

import { MatButtonModule } from '@angular/material/button';

import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';

import { MatFormFieldModule } from '@angular/material/form-field';

import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';

import { MatSelectModule } from '@angular/material/select';

import { MatOption, MatOptionModule } from '@angular/material/core';

import { MatTableDataSource, MatTableModule } from '@angular/material/table';

import { MainserviceService } from '../service/mainservice.service';

import jsPDF from 'jspdf';

import autoTable from 'jspdf-autotable';
import { MatSelect } from '@angular/material/select';

@Component({
  selector: 'app-productionreport',

  standalone: true,

  imports: [
    CommonModule,
    FormsModule,

    MatDialogModule,
    MatButtonModule,

    MatSelectModule,
    MatOptionModule,
    MatOption,

    MatTableModule,
    MatPaginatorModule,

    MatFormFieldModule,
  ],

  templateUrl: './productionreport.component.html',

  styleUrl: './productionreport.component.css',
})
export class ProductionreportComponent implements OnInit, AfterViewInit {
  // ============================================================
  // PAGINATOR
  // ============================================================

  @ViewChild(MatPaginator)
  paginator!: MatPaginator;

  // ============================================================
  // FILTER DATA
  // ============================================================

  productionUnits: string[] = ['BF-1', 'BF-2', 'BF-3'];

  months = [
    { value: '01', name: 'January' },
    { value: '02', name: 'February' },
    { value: '03', name: 'March' },
    { value: '04', name: 'April' },
    { value: '05', name: 'May' },
    { value: '06', name: 'June' },
    { value: '07', name: 'July' },
    { value: '08', name: 'August' },
    { value: '09', name: 'September' },
    { value: '10', name: 'October' },
    { value: '11', name: 'November' },
    { value: '12', name: 'December' },
  ];

  years: string[] = [];

  // ============================================================
  // SELECTED FILTER VALUES
  // ============================================================

  selectedUnit: string = '';

  selectedMonth: string = '';

  selectedYear: string = '';

  // ============================================================
  // TABLE COLUMNS
  // ============================================================

  displayedColumns: string[] = [
    'productionUnit',

    'productionOrder',

    'fromDate',

    'toDate',

    'orderMonth',

    'orderYear',
  ];

  // ============================================================
  // TABLE DATA
  // ============================================================

  originalData: any[] = [];

  dataSource = new MatTableDataSource<any>([]);

  // ============================================================
  // CONSTRUCTOR
  // ============================================================

  constructor(
    private dialogRef: MatDialogRef<ProductionreportComponent>,

    private service: MainserviceService,
  ) {}

  private refreshSub?: Subscription;

  // ============================================================
  // ON INIT
  // ============================================================

  ngOnInit(): void {
    this.loadReportData();
    this.refreshSub = this.service.refreshReport$.subscribe(() => {
      this.loadReportData();
    });
  }

  ngOnDestroy(): void {
    this.refreshSub?.unsubscribe();
  }

  // ============================================================
  // AFTER VIEW INIT
  // ============================================================

  ngAfterViewInit(): void {
    this.dataSource.paginator = this.paginator;
  }
  
// Add this method anywhere in the class
openSelect(select: MatSelect): void {
  select.open();
}
  // ============================================================
  // LOAD PRODUCTION REPORT
  // ============================================================

  loadReportData(): void {
    this.service.getProductionReport().subscribe({
      next: (response: any) => {
        console.log('Production Report Response:', response);

        /*
         * Your API may return:
         *
         * 1. Direct array
         *    [ {...}, {...} ]
         *
         * OR
         *
         * 2. ResponseData
         *    {
         *      status: true,
         *      data: [...]
         *    }
         *
         * Handle both.
         */

        let data: any[] = [];

        if (Array.isArray(response)) {
          data = response;
        } else if (response && Array.isArray(response.data)) {
          data = response.data;
        }

        this.originalData = [...data];

        this.dataSource.data = [...data];

        // ==================================================
        // CREATE YEAR LIST
        // ==================================================

        this.years = Array.from(
          new Set<string>(
            data

              .filter(
                (x: any) => x.Order_Year !== null && x.Order_Year !== undefined,
              )

              .map((x: any) => x.Order_Year.toString()),
          ),
        ).sort((a, b) => Number(b) - Number(a));

        // ==================================================
        // SET PAGINATOR
        // ==================================================

        if (this.paginator) {
          this.dataSource.paginator = this.paginator;
        }
      },

      error: (err: any) => {
        console.error('Production Report API Error:', err);
      },
    });
  }

  // ============================================================
  // SHOW / FILTER REPORT
  // ============================================================

  showReport(): void {
    const filteredData = this.originalData.filter((item: any) => {
      // ================================================
      // PRODUCTION UNIT
      // ================================================

      const matchUnit =
        !this.selectedUnit || item.Production_Unit === this.selectedUnit;

      // ================================================
      // MONTH
      // ================================================

      const matchMonth =
        !this.selectedMonth ||
        item.Order_Month?.toString().padStart(2, '0') === this.selectedMonth;

      // ================================================
      // YEAR
      // ================================================

      const matchYear =
        !this.selectedYear || item.Order_Year?.toString() === this.selectedYear;

      return matchUnit && matchMonth && matchYear;
    });

    // ========================================================
    // UPDATE TABLE
    // ========================================================

    this.dataSource.data = filteredData;

    // ========================================================
    // RESET PAGINATION
    // ========================================================

    if (this.paginator) {
      this.paginator.firstPage();
    }
  }

  // ============================================================
  // RESET REPORT
  // ============================================================

  resetReport(): void {
    this.selectedUnit = '';

    this.selectedMonth = '';

    this.selectedYear = '';

    this.dataSource.data = [...this.originalData];

    if (this.paginator) {
      this.paginator.firstPage();
    }
  }

  // ============================================================
  // EXPORT PDF
  // ============================================================

  exportPdf(): void {
    /*
     * Export currently displayed/filtered data.
     */

    const data = this.dataSource.data;

    // ========================================================
    // CHECK DATA
    // ========================================================

    if (!data || data.length === 0) {
      alert('No data available to export.');

      return;
    }

    // ========================================================
    // CREATE PDF
    // ========================================================

    const doc = new jsPDF('landscape', 'mm', 'a4');

    // ========================================================
    // TITLE
    // ========================================================

    doc.setFontSize(18);

    doc.setFont('helvetica', 'bold');

    doc.text('Production Report', 148, 15, {
      align: 'center',
    });

    // ========================================================
    // SUBTITLE
    // ========================================================

    doc.setFontSize(9);

    doc.setFont('helvetica', 'normal');

    let filterText = 'Production Order Mapping Report';

    if (this.selectedUnit) {
      filterText += ` | Unit: ${this.selectedUnit}`;
    }

    if (this.selectedMonth) {
      const month = this.months.find((x) => x.value === this.selectedMonth);

      if (month) {
        filterText += ` | Month: ${month.name}`;
      }
    }

    if (this.selectedYear) {
      filterText += ` | Year: ${this.selectedYear}`;
    }

    doc.text(filterText, 148, 22, {
      align: 'center',
    });

    // ========================================================
    // TABLE DATA
    // ========================================================

    const tableData = data.map((row: any) => [
      row.Production_Unit ?? '',

      row.Production_Order ?? '',

      this.formatDate(row.FromDate),

      this.formatDate(row.Todate),

      this.formatMonth(row.Order_Month),

      row.Order_Year ?? '',
    ]);

    // ========================================================
    // PDF TABLE
    // ========================================================

    autoTable(doc, {
      startY: 28,

      head: [
        [
          'Production Unit',

          'Production Order',

          'From Date',

          'To Date',

          'Month',

          'Year',
        ],
      ],

      body: tableData,

      theme: 'grid',

      styles: {
        fontSize: 9,

        cellPadding: 3,

        valign: 'middle',

        halign: 'center',
      },

      headStyles: {
        fontSize: 9,

        fontStyle: 'bold',

        halign: 'center',
      },

      columnStyles: {
        0: {
          cellWidth: 35,
        },

        1: {
          cellWidth: 55,
        },

        2: {
          cellWidth: 35,
        },

        3: {
          cellWidth: 35,
        },

        4: {
          cellWidth: 30,
        },

        5: {
          cellWidth: 30,
        },
      },

      margin: {
        left: 15,

        right: 15,
      },
    });

    // ========================================================
    // FOOTER
    // ========================================================

    const pageCount = (doc as any).internal.getNumberOfPages();

    for (let i = 1; i <= pageCount; i++) {
      doc.setPage(i);

      doc.setFontSize(8);

      doc.text(`Page ${i} of ${pageCount}`, 148, 200, {
        align: 'center',
      });
    }

    // ========================================================
    // FILE NAME
    // ========================================================

    let fileName = 'Production_Report';

    if (this.selectedUnit) {
      fileName += `_${this.selectedUnit}`;
    }

    if (this.selectedMonth) {
      const month = this.months.find((x) => x.value === this.selectedMonth);

      if (month) {
        fileName += `_${month.name}`;
      }
    }

    if (this.selectedYear) {
      fileName += `_${this.selectedYear}`;
    }

    fileName += '.pdf';

    // ========================================================
    // SAVE
    // ========================================================

    doc.save(fileName);
  }

  // ============================================================
  // FORMAT DATE
  // ============================================================

  private formatDate(value: any): string {
    if (!value) {
      return '';
    }

    const date = new Date(value);

    if (isNaN(date.getTime())) {
      return value.toString();
    }

    const day = String(date.getDate()).padStart(2, '0');

    const month = String(date.getMonth() + 1).padStart(2, '0');

    const year = date.getFullYear();

    return `${day}-${month}-${year}`;
  }

  // ============================================================
  // FORMAT MONTH
  // ============================================================

  private formatMonth(value: any): string {
    if (value === null || value === undefined || value === '') {
      return '';
    }

    const month = this.months.find(
      (x) => x.value === value.toString().padStart(2, '0'),
    );

    return month ? month.name : value.toString();
  }

  // ============================================================
  // GET BADGE CLASS
  // ============================================================

  getBadgeClass(unit: string): string {
    switch (unit) {
      case 'BF-1':
        return 'badge-sapphire';

      case 'BF-2':
        return 'badge-emerald';

      case 'BF-3':
        return 'badge-amber';

      default:
        return 'badge-default';
    }
  }

  // ============================================================
  // CLOSE DIALOG
  // ============================================================

  closeDialog(): void {
    this.dialogRef.close();
  }
}
