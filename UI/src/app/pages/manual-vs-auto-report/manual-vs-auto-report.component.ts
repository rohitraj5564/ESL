import { Component, OnInit, ViewChild, AfterViewInit, OnDestroy } from '@angular/core';
import { MatPaginator } from '@angular/material/paginator';
import { MatTableDataSource } from '@angular/material/table';
import { MatSort } from '@angular/material/sort';
import { Subscription, interval } from 'rxjs';
import * as XLSX from 'xlsx';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import { MainserviceService } from '../service/mainservice.service';
import { ThemeService } from '../service/theme.service';

export interface ManualVsAutoItem {
  MappingID: string;
  TouchPointID?: number;
  TouchPointType?: string;
  LocoName?: string;
  LocoSerialNo?: string;
  LocoTagId?: string;
  LadleName?: string;
  LadleNumber?: number | string;
  LadleRfidTag?: string;
  LocoEventID?: string;
  LadleRfidEventID?: string;
  CameraEventID?: string;
  LocoEventTime?: string;
  LadleRfidEventTime?: string;
  CameraEventTime?: string;
  MappingStartTime?: string;
  MappingEndTime?: string;
  ConfidenceScore?: number;
  MappingStatus?: string;
  MappingReason?: string;
  CreatedOn?: string;
  IsManual?: boolean;
  AssignmentType?: string;
}

@Component({
  selector: 'app-manual-vs-auto-report',
  templateUrl: './manual-vs-auto-report.component.html',
  styleUrls: ['./manual-vs-auto-report.component.css'],
  standalone: false
})
export class ManualVsAutoReportComponent implements OnInit, AfterViewInit, OnDestroy {
  public isLoading = false;
  public isDarkTheme = false;

  // Date filters - default to rolling last 24 hours
  public fromInputDate: Date = new Date(Date.now() - 24 * 60 * 60 * 1000);
  public toInputDate: Date = new Date();
  public today: Date = new Date();

  // Assignment Type filter: ALL, MANUAL, AUTOMATIC
  public selectedTypeFilter: string = 'ALL';

  public rawData: ManualVsAutoItem[] = [];
  public filteredData: ManualVsAutoItem[] = [];
  public dataSource = new MatTableDataSource<ManualVsAutoItem>([]);
  public hasSearched = false;

  // KPI Metrics
  public totalCount = 0;
  public manualCount = 0;
  public manualPercentage = 0;
  public autoCount = 0;
  public autoPercentage = 0;
  public automationRate = 0;

  private themeSubscription!: Subscription;
  private autoClearSubscription!: Subscription;

  displayedColumns: string[] = [
    'SLNo',
    'AssignmentType',
    'LocoName',
    'LadleName',
    'TouchPointType',
    'ConfidenceScore',
    'MappingStatus',
    'MappingReason',
    'MappingStartTime',
    'CreatedOn'
  ];

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  constructor(
    public mainService: MainserviceService,
    private themeService: ThemeService
  ) {}

  ngOnInit(): void {
    this.themeSubscription = this.themeService.isDarkMode$.subscribe(
      (isDark) => { this.isDarkTheme = isDark; }
    );

    // Initial load for rolling 24 hours
    this.getFilteredData();

    // Auto-clear / refresh data every 24 hours (24 * 60 * 60 * 1000 ms)
    this.autoClearSubscription = interval(24 * 60 * 60 * 1000).subscribe(() => {
      this.clearFilters();
    });
  }

  ngAfterViewInit() {
    this.dataSource.paginator = this.paginator;
    this.dataSource.sort = this.sort;
    this.dataSource.filterPredicate = (data: ManualVsAutoItem, filter: string) => {
      const term = filter.trim().toLowerCase();
      const typeMatch = this.selectedTypeFilter === 'ALL'
        ? true
        : this.selectedTypeFilter === 'MANUAL'
          ? (data.IsManual === true || data.AssignmentType === 'Manual')
          : (data.IsManual === false || data.AssignmentType === 'Automatic');

      if (!typeMatch) return false;
      if (!term) return true;

      const searchable = [
        data.LocoName,
        data.LadleName,
        data.LadleNumber,
        data.TouchPointType,
        data.MappingStatus,
        data.MappingReason,
        data.AssignmentType
      ].filter(Boolean).join(' ').toLowerCase();

      return searchable.includes(term);
    };
  }

  ngOnDestroy(): void {
    this.themeSubscription?.unsubscribe();
    this.autoClearSubscription?.unsubscribe();
  }

  getFilteredData() {
    this.isLoading = true;
    this.hasSearched = true;

    const pad = (n: number) => n < 10 ? '0' + n : '' + n;
    const formatLocal = (d: any, timeStr?: string) => {
      if (!d) return '';
      const rawDate = d && typeof d.toDate === 'function' ? d.toDate() : (d instanceof Date ? d : new Date(d));
      const dt = isNaN(rawDate.getTime()) ? new Date() : rawDate;
      const h = pad(dt.getHours());
      const m = pad(dt.getMinutes());
      const s = pad(dt.getSeconds());
      const formattedTime = timeStr || `${h}:${m}:${s}`;
      return `${dt.getFullYear()}-${pad(dt.getMonth() + 1)}-${pad(dt.getDate())} ${formattedTime}`;
    };

    const input = {
      FromDate: formatLocal(this.fromInputDate, '00:00:00'),
      ToDate: formatLocal(this.toInputDate, '23:59:59')
    };

    this.mainService.getManualVsAutoAssignmentReport(input).subscribe({
      next: (res: any) => {
        this.isLoading = false;
        const rawList = res?.data ? res.data : (Array.isArray(res) ? res : []);
        this.rawData = Array.isArray(rawList) ? rawList : [];

        this.applyLocalFilters();
      },
      error: (err) => {
        console.error('Error fetching Manual vs Auto report:', err);
        this.isLoading = false;
        this.rawData = [];
        this.applyLocalFilters();
      }
    });
  }

  onTypeFilterChange(type: string) {
    this.selectedTypeFilter = type;
    this.applyLocalFilters();
  }

  applyLocalFilters() {
    // Filter raw data based on selectedTypeFilter
    let list = [...this.rawData];
    if (this.selectedTypeFilter === 'MANUAL') {
      list = list.filter(x => x.IsManual === true || x.AssignmentType === 'Manual');
    } else if (this.selectedTypeFilter === 'AUTOMATIC') {
      list = list.filter(x => x.IsManual === false || x.AssignmentType === 'Automatic');
    }

    this.filteredData = list;
    this.dataSource.data = this.filteredData;
    this.calculateMetrics();

    if (this.paginator) {
      this.paginator.firstPage();
    }
  }

  calculateMetrics() {
    // Total from raw 24-hour / selected date window
    this.totalCount = this.rawData.length;
    this.manualCount = this.rawData.filter(x => x.IsManual === true || x.AssignmentType === 'Manual').length;
    this.autoCount = this.rawData.filter(x => x.IsManual === false || x.AssignmentType === 'Automatic').length;

    if (this.totalCount > 0) {
      this.manualPercentage = Math.round((this.manualCount / this.totalCount) * 100);
      this.autoPercentage = Math.round((this.autoCount / this.totalCount) * 100);
      this.automationRate = parseFloat(((this.autoCount / this.totalCount) * 100).toFixed(1));
    } else {
      this.manualPercentage = 0;
      this.autoPercentage = 0;
      this.automationRate = 0;
    }
  }

  clearFilters(): void {
    // Resets to rolling 24-hour scope and re-queries data
    this.fromInputDate = new Date(Date.now() - 24 * 60 * 60 * 1000);
    this.toInputDate = new Date();
    this.selectedTypeFilter = 'ALL';
    this.dataSource.filter = '';
    this.getFilteredData();
  }

  applyFilter(event: Event) {
    const filterValue = (event.target as HTMLInputElement).value;
    this.dataSource.filter = filterValue.trim().toLowerCase();
    if (this.dataSource.paginator) {
      this.dataSource.paginator.firstPage();
    }
  }

  exportToExcel() {
    if (!this.filteredData || this.filteredData.length === 0) return;

    const exportRows = this.filteredData.map((item, index) => ({
      'SL.No': index + 1,
      'Assignment Type': item.AssignmentType || (item.IsManual ? 'Manual' : 'Automatic'),
      'Loco': item.LocoName || '-',
      'Loco Tag ID': item.LocoTagId || '-',
      'Ladle': item.LadleName || (item.LadleNumber ? `Ladle ${item.LadleNumber}` : '-'),
      'Ladle RFID Tag': item.LadleRfidTag || '-',
      'Touch Point / Track': item.TouchPointType || '-',
      'Confidence Score (%)': item.ConfidenceScore ?? '-',
      'Mapping Status': item.MappingStatus || '-',
      'Mapping Reason': item.MappingReason || '-',
      'Mapping Start Time': item.MappingStartTime ? new Date(item.MappingStartTime).toLocaleString('en-GB') : '-',
      'Mapping End Time': item.MappingEndTime ? new Date(item.MappingEndTime).toLocaleString('en-GB') : '-',
      'Loco Event Time': item.LocoEventTime ? new Date(item.LocoEventTime).toLocaleString('en-GB') : '-',
      'Ladle Event Time': item.LadleRfidEventTime ? new Date(item.LadleRfidEventTime).toLocaleString('en-GB') : '-',
      'Created On': item.CreatedOn ? new Date(item.CreatedOn).toLocaleString('en-GB') : '-'
    }));

    const worksheet: XLSX.WorkSheet = XLSX.utils.json_to_sheet(exportRows);

    // Auto fit column widths
    const colWidths = [
      { wch: 8 },  // SL.No
      { wch: 18 }, // Assignment Type
      { wch: 14 }, // Loco
      { wch: 28 }, // Loco Tag ID
      { wch: 14 }, // Ladle
      { wch: 28 }, // Ladle RFID Tag
      { wch: 20 }, // Touch Point
      { wch: 20 }, // Confidence Score
      { wch: 16 }, // Mapping Status
      { wch: 45 }, // Mapping Reason
      { wch: 22 }, // Mapping Start Time
      { wch: 22 }, // Mapping End Time
      { wch: 22 }, // Loco Event Time
      { wch: 22 }, // Ladle Event Time
      { wch: 22 }  // Created On
    ];
    worksheet['!cols'] = colWidths;

    const workbook: XLSX.WorkBook = {
      Sheets: { 'Manual vs Auto Assignment': worksheet },
      SheetNames: ['Manual vs Auto Assignment']
    };
    XLSX.writeFile(workbook, `ESL_Manual_vs_Auto_Assignment_Report_${new Date().getTime()}.xlsx`);
  }

  exportToPDF() {
    if (!this.filteredData || this.filteredData.length === 0) return;

    const doc = new jsPDF('landscape');

    // Title & Header
    doc.setFontSize(16);
    doc.setTextColor(33, 37, 41);
    doc.text('Electrosteel Steels Limited - ESL Ladle Tracking System', 14, 15);

    doc.setFontSize(12);
    doc.setTextColor(108, 117, 125);
    doc.text('Manual Assignment vs Automatic Assignment Report', 14, 22);

    // Date range & KPI info
    doc.setFontSize(9);
    doc.setTextColor(60, 60, 60);
    const dateRangeStr = `Period: ${this.fromInputDate.toLocaleDateString()} to ${this.toInputDate.toLocaleDateString()} | Generated: ${new Date().toLocaleString('en-GB')}`;
    doc.text(dateRangeStr, 14, 28);

    const kpiSummary = `Total: ${this.totalCount}  |  Manual: ${this.manualCount} (${this.manualPercentage}%)  |  Automatic: ${this.autoCount} (${this.autoPercentage}%)  |  Automation Rate: ${this.automationRate}%`;
    doc.setFont('helvetica', 'bold');
    doc.text(kpiSummary, 14, 34);
    doc.setFont('helvetica', 'normal');

    // Table
    const tableData = this.filteredData.map((item, index) => [
      index + 1,
      item.AssignmentType || (item.IsManual ? 'Manual' : 'Automatic'),
      item.LocoName || '-',
      item.LadleName || `Ladle ${item.LadleNumber ?? '-'}`,
      item.TouchPointType || '-',
      `${item.ConfidenceScore ?? 0}%`,
      item.MappingStatus || '-',
      item.MappingReason || '-',
      item.CreatedOn ? new Date(item.CreatedOn).toLocaleString('en-GB') : '-'
    ]);

    autoTable(doc, {
      startY: 38,
      head: [['#', 'Type', 'Loco', 'Ladle', 'Track/Location', 'Score', 'Status', 'Details / Reason', 'Timestamp']],
      body: tableData,
      theme: 'grid',
      headStyles: {
        fillColor: [30, 41, 59],
        textColor: [255, 255, 255],
        fontSize: 8,
        halign: 'center'
      },
      styles: {
        fontSize: 7.5,
        cellPadding: 2,
        overflow: 'linebreak'
      },
      columnStyles: {
        0: { cellWidth: 10, halign: 'center' },
        1: { cellWidth: 22, halign: 'center' },
        2: { cellWidth: 20 },
        3: { cellWidth: 20 },
        4: { cellWidth: 28 },
        5: { cellWidth: 16, halign: 'center' },
        6: { cellWidth: 24, halign: 'center' },
        7: { cellWidth: 'auto' },
        8: { cellWidth: 32, halign: 'center' }
      }
    });

    doc.save(`ESL_Manual_vs_Auto_Assignment_Report_${new Date().getTime()}.pdf`);
  }
}
