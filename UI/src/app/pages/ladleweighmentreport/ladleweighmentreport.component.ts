import { Component, OnInit, ViewChild, AfterViewInit, OnDestroy } from '@angular/core';
import { MatPaginator } from '@angular/material/paginator';
import { MatTableDataSource } from '@angular/material/table';
import { MatSort } from '@angular/material/sort';
import { Subscription } from 'rxjs';
import * as XLSX from 'xlsx';
import { MainserviceService } from '../service/mainservice.service';
import { ThemeService } from '../service/theme.service';

export interface WeighmentTransactionInput {
  fromDate: any;
  toDate: any;
  ladleNo?: string;
  location?: string;
}

@Component({
  selector: 'app-ladleweighmentreport',
  templateUrl: './ladleweighmentreport.component.html',
  styleUrls: ['./ladleweighmentreport.component.css'],
  standalone: false
})
export class LadleWeighmentReportComponent implements OnInit, AfterViewInit, OnDestroy {
  public isLoading = false;
  public isDarkTheme = false;
  public fromInputDate: Date = new Date();
  public toInputDate: Date = new Date();
  public today: Date = new Date();
  public reportData: any[] = [];
  public dataSource = new MatTableDataSource<any>([]);
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

  ngAfterViewInit() {
    this.dataSource.paginator = this.paginator;
    this.dataSource.sort = this.sort;
  }

  ngOnDestroy(): void {
    this.themeSubscription?.unsubscribe();
  }

  getFilteredData() {
    this.isLoading = true;
    this.hasSearched = true;

    const pad = (n: number) => n < 10 ? '0' + n : '' + n;
    const formatLocal = (d: Date, timeStr: string) => {
      const dt = new Date(d);
      return `${dt.getFullYear()}-${pad(dt.getMonth() + 1)}-${pad(dt.getDate())}T${timeStr}`;
    };

    const input: WeighmentTransactionInput = {
      fromDate: formatLocal(this.fromInputDate, '00:00:00'),
      toDate: formatLocal(this.toInputDate, '23:59:59')
    };

    this.mainService.getLadleWeighmentReport(input).subscribe({
      next: (res: any) => {
        this.isLoading = false;
        const rawList = res?.data ? res.data : (Array.isArray(res) ? res : []);
        const payload = Array.isArray(rawList) ? rawList : [];

        // Assign same Cast No to all records sharing the same Consumption No
        const consumptionToCastMap = new Map<string, string>();
        for (const item of payload) {
          const consNo = String(item.ConsumptionNo ?? item.ConsumptionNumber ?? '').trim();
          const castNo = String(item.CastNo ?? item.CastNumber ?? '').trim();
          if (consNo && castNo && castNo !== '-' && !consumptionToCastMap.has(consNo)) {
            consumptionToCastMap.set(consNo, castNo);
          }
        }

        for (const item of payload) {
          const consNo = String(item.ConsumptionNo ?? item.ConsumptionNumber ?? '').trim();
          const currentCast = String(item.CastNo ?? item.CastNumber ?? '').trim();
          if (consNo && (!currentCast || currentCast === '-') && consumptionToCastMap.has(consNo)) {
            const assignedCast = consumptionToCastMap.get(consNo);
            item.CastNo = assignedCast;
            item.CastNumber = assignedCast;
          }
        }

        this.reportData = payload;
        this.dataSource.data = this.reportData;
        this.calculateMetrics();
        if (this.paginator) {
          this.paginator.firstPage();
        }
      },
      error: (err) => {
        console.error('Error fetching ladle weighment report:', err);
        this.isLoading = false;
      }
    });
  }

  calculateMetrics() {
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

  applyFilter(event: Event) {
    const filterValue = (event.target as HTMLInputElement).value;
    this.dataSource.filter = filterValue.trim().toLowerCase();
    if (this.dataSource.paginator) {
      this.dataSource.paginator.firstPage();
    }
  }

  exportToExcel() {
    if (!this.reportData || this.reportData.length === 0) return;

    const exportRows = this.reportData.map((item, index) => ({
      'SL.No': item.SLNo ?? (index + 1),
      'Ladle No': item.LadleNo ?? '-',
      'Transaction No': item.TransactionNo ?? item.TransactionNumber ?? '-',
      'Consumption No': item.ConsumptionNo ?? item.ConsumptionNumber ?? '-',
      'Cast No': item.CastNo ?? item.CastNumber ?? '-',
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

    // Auto fit columns width
    const colWidths = [
      { wch: 8 },  // SL.No
      { wch: 12 }, // Ladle No
      { wch: 16 }, // Transaction No
      { wch: 16 }, // Consumption No
      { wch: 14 }, // Cast No
      { wch: 22 }, // Trip Date Time
      { wch: 16 }, // Sender Location
      { wch: 16 }, // Receiver Location
      { wch: 14 }, // Tare Weight
      { wch: 22 }, // Tare Weight DateTime
      { wch: 14 }, // Gross Weight
      { wch: 22 }, // Gross Weight DateTime
      { wch: 14 }  // Net Weight
    ];
    worksheet['!cols'] = colWidths;

    const workbook: XLSX.WorkBook = { Sheets: { 'Ladle Weighment': worksheet }, SheetNames: ['Ladle Weighment'] };
    XLSX.writeFile(workbook, `Ladle_Weighment_Report_${new Date().getTime()}.xlsx`);
  }
}
