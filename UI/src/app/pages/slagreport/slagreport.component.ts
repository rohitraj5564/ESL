import { Component, OnInit, ViewChild, AfterViewInit, OnDestroy } from '@angular/core';
import { MatPaginator } from '@angular/material/paginator';
import { MatTableDataSource } from '@angular/material/table';
import { MatSort } from '@angular/material/sort';
import { Subscription } from 'rxjs';
import * as XLSX from 'xlsx';
import { MainserviceService } from '../service/mainservice.service';
import { ThemeService } from '../service/theme.service';

export interface SlagWeighmentInput {
  fromDate: any;
  toDate: any;
  ladleNo?: string;
  location?: string;
}

export interface SlagRecord {
  SLNo: number;
  LadleNo: string;
  TransactionNo: string;
  ConsumptionNo: string;
  CastNo: string;
  TripDateTime: string;
  SenderLocation: string;
  ReceiverLocation: string;
  TareWeight: number;
  TareWeightDateTime: string;
  GrossWeight: number;
  GrossWeightDateTime: string;
  NetWeight: number;
}

@Component({
  selector: 'app-slagreport',
  templateUrl: './slagreport.component.html',
  styleUrls: ['./slagreport.component.css'],
  standalone: false
})
export class SlagReportComponent implements OnInit, AfterViewInit, OnDestroy {
  public isLoading = false;
  public isDarkTheme = false;
  public fromInputDate: Date = new Date();
  public toInputDate: Date = new Date();
  public today: Date = new Date();
  public reportData: SlagRecord[] = [];
  public dataSource = new MatTableDataSource<SlagRecord>([]);
  public hasSearched = false;

  // KPI Metrics
  public totalGrossWeight = 0;
  public totalTareWeight = 0;
  public totalNetWeight = 0;

  private themeSubscription!: Subscription;

  displayedColumns: string[] = [
    'SLNo',
    'LadleNo',
    'TransactionNo',
    'ConsumptionNo',
    'CastNo',
    'TripDateTime',
    'SenderLocation',
    'ReceiverLocation',
    'TareWeight',
    'TareWeightDateTime',
    'GrossWeight',
    'GrossWeightDateTime',
    'NetWeight'
  ];

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  // Mock slag records for UI preview until backend API endpoint is ready
  private mockSlagRecords: SlagRecord[] = [
    {
      SLNo: 1,
      LadleNo: 'SP-101',
      TransactionNo: 'TX-SLAG-8801',
      ConsumptionNo: 'CS-4011',
      CastNo: 'CST-921',
      TripDateTime: new Date(Date.now() - 3600000 * 2).toISOString(),
      SenderLocation: 'Blast Furnace 1',
      ReceiverLocation: 'Slag Yard',
      TareWeight: 24.50,
      TareWeightDateTime: new Date(Date.now() - 3600000 * 2.5).toISOString(),
      GrossWeight: 58.80,
      GrossWeightDateTime: new Date(Date.now() - 3600000 * 2).toISOString(),
      NetWeight: 34.30
    },
    {
      SLNo: 2,
      LadleNo: 'SP-104',
      TransactionNo: 'TX-SLAG-8802',
      ConsumptionNo: 'CS-4012',
      CastNo: 'CST-922',
      TripDateTime: new Date(Date.now() - 3600000 * 4).toISOString(),
      SenderLocation: 'Blast Furnace 2',
      ReceiverLocation: 'Slag Granulation Plant',
      TareWeight: 25.10,
      TareWeightDateTime: new Date(Date.now() - 3600000 * 4.5).toISOString(),
      GrossWeight: 61.30,
      GrossWeightDateTime: new Date(Date.now() - 3600000 * 4).toISOString(),
      NetWeight: 36.20
    },
    {
      SLNo: 3,
      LadleNo: 'SP-108',
      TransactionNo: 'TX-SLAG-8803',
      ConsumptionNo: 'CS-4015',
      CastNo: 'CST-924',
      TripDateTime: new Date(Date.now() - 3600000 * 6).toISOString(),
      SenderLocation: 'SMS 1',
      ReceiverLocation: 'Slag Dumping Area',
      TareWeight: 23.80,
      TareWeightDateTime: new Date(Date.now() - 3600000 * 6.5).toISOString(),
      GrossWeight: 55.40,
      GrossWeightDateTime: new Date(Date.now() - 3600000 * 6).toISOString(),
      NetWeight: 31.60
    },
    {
      SLNo: 4,
      LadleNo: 'SP-112',
      TransactionNo: 'TX-SLAG-8804',
      ConsumptionNo: 'CS-4018',
      CastNo: 'CST-927',
      TripDateTime: new Date(Date.now() - 3600000 * 9).toISOString(),
      SenderLocation: 'Blast Furnace 3',
      ReceiverLocation: 'Slag Yard',
      TareWeight: 26.00,
      TareWeightDateTime: new Date(Date.now() - 3600000 * 9.5).toISOString(),
      GrossWeight: 63.25,
      GrossWeightDateTime: new Date(Date.now() - 3600000 * 9).toISOString(),
      NetWeight: 37.25
    },
    {
      SLNo: 5,
      LadleNo: 'SP-115',
      TransactionNo: 'TX-SLAG-8805',
      ConsumptionNo: 'CS-4021',
      CastNo: 'CST-930',
      TripDateTime: new Date(Date.now() - 3600000 * 12).toISOString(),
      SenderLocation: 'SMS 2',
      ReceiverLocation: 'Slag Granulation Plant',
      TareWeight: 24.20,
      TareWeightDateTime: new Date(Date.now() - 3600000 * 12.5).toISOString(),
      GrossWeight: 59.10,
      GrossWeightDateTime: new Date(Date.now() - 3600000 * 12).toISOString(),
      NetWeight: 34.90
    },
    {
      SLNo: 6,
      LadleNo: 'SP-120',
      TransactionNo: 'TX-SLAG-8806',
      ConsumptionNo: 'CS-4025',
      CastNo: 'CST-933',
      TripDateTime: new Date(Date.now() - 3600000 * 15).toISOString(),
      SenderLocation: 'Blast Furnace 1',
      ReceiverLocation: 'Slag Dumping Area',
      TareWeight: 25.40,
      TareWeightDateTime: new Date(Date.now() - 3600000 * 15.5).toISOString(),
      GrossWeight: 60.80,
      GrossWeightDateTime: new Date(Date.now() - 3600000 * 15).toISOString(),
      NetWeight: 35.40
    },
    {
      SLNo: 7,
      LadleNo: 'SP-122',
      TransactionNo: 'TX-SLAG-8807',
      ConsumptionNo: 'CS-4029',
      CastNo: 'CST-936',
      TripDateTime: new Date(Date.now() - 3600000 * 18).toISOString(),
      SenderLocation: 'SMS 1',
      ReceiverLocation: 'Slag Yard',
      TareWeight: 23.90,
      TareWeightDateTime: new Date(Date.now() - 3600000 * 18.5).toISOString(),
      GrossWeight: 56.70,
      GrossWeightDateTime: new Date(Date.now() - 3600000 * 18).toISOString(),
      NetWeight: 32.80
    }
  ];

  constructor(
    public mainService: MainserviceService,
    private themeService: ThemeService
  ) {}

  ngOnInit(): void {
    this.themeSubscription = this.themeService.isDarkMode$.subscribe(
      (isDark) => { this.isDarkTheme = isDark; }
    );
    this.getFilteredData();
  }

  ngAfterViewInit(): void {
    this.dataSource.paginator = this.paginator;
    this.dataSource.sort = this.sort;
  }

  ngOnDestroy(): void {
    this.themeSubscription?.unsubscribe();
  }

  /**
   * Search / Filter Slag Report records.
   * NOTE: As requested, no backend API endpoint is called for now.
   * UI simulates realistic response data with loading state.
   */
  getFilteredData(): void {
    this.isLoading = true;
    this.hasSearched = true;

    // Simulate network delay for realistic UI behavior
    setTimeout(() => {
      this.isLoading = false;
      const records = [...this.mockSlagRecords];

      // Assign same Cast No to all records sharing the same Consumption No
      const consumptionToCastMap = new Map<string, string>();
      for (const item of records) {
        const consNo = String(item.ConsumptionNo ?? '').trim();
        const castNo = String(item.CastNo ?? '').trim();
        if (consNo && castNo && castNo !== '-' && !consumptionToCastMap.has(consNo)) {
          consumptionToCastMap.set(consNo, castNo);
        }
      }

      for (const item of records) {
        const consNo = String(item.ConsumptionNo ?? '').trim();
        const currentCast = String(item.CastNo ?? '').trim();
        if (consNo && (!currentCast || currentCast === '-') && consumptionToCastMap.has(consNo)) {
          item.CastNo = consumptionToCastMap.get(consNo) || '';
        }
      }

      this.reportData = records;
      this.dataSource.data = this.reportData;
      this.calculateMetrics();

      if (this.paginator) {
        this.paginator.firstPage();
      }
    }, 350);
  }

  calculateMetrics(): void {
    let gross = 0;
    let tare = 0;
    let net = 0;

    for (const item of this.reportData) {
      if (item.GrossWeight && !isNaN(Number(item.GrossWeight))) {
        gross += Number(item.GrossWeight);
      }
      if (item.TareWeight && !isNaN(Number(item.TareWeight))) {
        tare += Number(item.TareWeight);
      }
      if (item.NetWeight && !isNaN(Number(item.NetWeight))) {
        net += Number(item.NetWeight);
      }
    }

    this.totalGrossWeight = Number(gross.toFixed(2));
    this.totalTareWeight = Number(tare.toFixed(2));
    this.totalNetWeight = Number(net.toFixed(2));
  }

  clearFilters(): void {
    this.fromInputDate = new Date();
    this.toInputDate = new Date();
    this.getFilteredData();
  }

  applyFilter(event: Event): void {
    const filterValue = (event.target as HTMLInputElement).value;
    this.dataSource.filter = filterValue.trim().toLowerCase();
    if (this.dataSource.paginator) {
      this.dataSource.paginator.firstPage();
    }
  }

  exportToExcel(): void {
    if (!this.reportData || this.reportData.length === 0) return;

    const exportRows = this.reportData.map((item, index) => ({
      'SL.No': item.SLNo ?? (index + 1),
      'Slag Pot / Ladle No': item.LadleNo ?? '-',
      'Transaction No': item.TransactionNo ?? '-',
      'Consumption No': item.ConsumptionNo ?? '-',
      'Cast No': item.CastNo ?? '-',
      'Trip Date Time': item.TripDateTime ? new Date(item.TripDateTime).toLocaleString('en-GB') : '-',
      'Sender Location': item.SenderLocation ?? '-',
      'Receiver Location': item.ReceiverLocation ?? '-',
      'Tare Weight': item.TareWeight != null ? Number(item.TareWeight).toFixed(2) : '-',
      'Tare Weight DateTime': item.TareWeightDateTime ? new Date(item.TareWeightDateTime).toLocaleString('en-GB') : '-',
      'Gross Weight': item.GrossWeight != null ? Number(item.GrossWeight).toFixed(2) : '-',
      'Gross Weight DateTime': item.GrossWeightDateTime ? new Date(item.GrossWeightDateTime).toLocaleString('en-GB') : '-',
      'Net Weight': item.NetWeight != null ? Number(item.NetWeight).toFixed(2) : '-'
    }));

    const worksheet: XLSX.WorkSheet = XLSX.utils.json_to_sheet(exportRows);

    // Auto fit column widths
    const colWidths = [
      { wch: 8 },  // SL.No
      { wch: 18 }, // Slag Pot / Ladle No
      { wch: 18 }, // Transaction No
      { wch: 16 }, // Consumption No
      { wch: 14 }, // Cast No
      { wch: 22 }, // Trip Date Time
      { wch: 20 }, // Sender Location
      { wch: 24 }, // Receiver Location
      { wch: 14 }, // Tare Weight
      { wch: 22 }, // Tare Weight DateTime
      { wch: 14 }, // Gross Weight
      { wch: 22 }, // Gross Weight DateTime
      { wch: 14 }  // Net Weight
    ];
    worksheet['!cols'] = colWidths;

    const workbook: XLSX.WorkBook = { Sheets: { 'Slag Report': worksheet }, SheetNames: ['Slag Report'] };
    XLSX.writeFile(workbook, `Slag_Report_${new Date().getTime()}.xlsx`);
  }
}
