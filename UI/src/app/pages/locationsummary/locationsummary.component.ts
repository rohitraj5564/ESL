import { Component, OnInit, ViewChild, AfterViewInit, OnDestroy } from '@angular/core';
import { MatPaginator } from '@angular/material/paginator';
import { MatTableDataSource } from '@angular/material/table';
import { MatSort } from '@angular/material/sort';
import { Subscription } from 'rxjs';
import * as XLSX from 'xlsx';
import { MainserviceService } from '../service/mainservice.service';
import { DataService } from '../service/data.service';
import { ThemeService } from '../service/theme.service';

@Component({
  selector: 'app-locationsummary',
  templateUrl: './locationsummary.component.html',
  styleUrls: ['./locationsummary.component.css'],
  standalone: false
})
export class LocationsummaryComponent implements OnInit, AfterViewInit, OnDestroy {
  public isLoading = false;
  public isDarkTheme = false;
  public data: string = '';
  public fromInputDate: Date = new Date();
  public toInputDate: Date = new Date();
  public today: Date = new Date();
  public AvgTAT: string = '0';
  public AvgHoldtime: string = '0';
  public listlength: number = 0;
  public ladleTransaction: any[] = [];
  public locationList: any[] = [];

  private themeSubscription!: Subscription;

  dataSource = new MatTableDataSource<any>([]);

  displayedColumns: string[] = [
    'SerialNumber', 'LadleNo', 'SourceLocation', 'SourceINDateSTR',
    'SourceINTimeSTR', 'SourceOUTDateSTR', 'SourceOUTTimeSTR', 'SourceWeight',
    'SourceWeightDateTime', 'CastNo', 'TareWeight', 'TareWeightDateTime',
    'NetWeight', 'TAT', 'DestinationLocation', 'DestinationINDateSTR',
    'DestinationINTimeSTR', 'DestinationOUTDateSTR', 'DestinationOUTTimeSTR'
  ];

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  constructor(
    public mainService: MainserviceService,
    private dataService: DataService,
    private themeService: ThemeService
  ) { }

  ngOnInit(): void {
    this.themeSubscription = this.themeService.isDarkMode$.subscribe(
      (isDark) => { this.isDarkTheme = isDark; }
    );
    this.data = this.dataService.getData() || 'BF 1';
    this.getLocations();
    this.getLocationData();
  }

  ngAfterViewInit() {
    this.dataSource.paginator = this.paginator;
    this.dataSource.sort = this.sort;
  }

  ngOnDestroy(): void {
    this.themeSubscription?.unsubscribe();
  }

  getLocations() {
    this.mainService.getAllLocations().subscribe({
      next: (res: any) => {
        this.locationList = res?.data ? res.data : (Array.isArray(res) ? res : []);
      }
    });
  }

  onLocationChange(newLoc: string) {
    this.data = newLoc;
    this.dataService.setData(newLoc);
    this.getLocationData();
  }

  getLocationData() {
    this.isLoading = true;
    let loc = this.data || 'SMS';
    if (loc === 'BF 1') loc = 'BF1';
    if (loc === 'BF 2') loc = 'BF2';
    if (loc === 'BF 3') loc = 'BF3';

    const input = {
      fromDate: this.fromInputDate,
      toDate: this.toInputDate,
      ladleNo: loc,
      LadleNo: loc,
      locationName: loc
    };

    console.log(input);

    this.mainService.getLocationSummary(input).subscribe({
      next: (res: any) => {
        this.isLoading = false;
        const payload = res?.data ? res.data : res;
        this.AvgTAT = (payload?.AverageTATSTR && payload.AverageTATSTR !== '0' && payload.AverageTATSTR !== '00:00:00' && payload.AverageTATSTR !== '00:00') ? payload.AverageTATSTR : '00:00';
        this.AvgHoldtime = (payload?.AverageHoldTimeSTR && payload.AverageHoldTimeSTR !== '0' && payload.AverageHoldTimeSTR !== '00:00:00' && payload.AverageHoldTimeSTR !== '00:00') ? payload.AverageHoldTimeSTR : '00:00';
        this.ladleTransaction = payload?.LadleTransactionList || payload?.LadleList || (Array.isArray(payload) ? payload : []);
        this.listlength = this.ladleTransaction.length;
        this.dataSource.data = this.ladleTransaction;
        if (this.paginator) {
          this.paginator.firstPage();
        }
      },
      error: (err) => {
        console.error('Error fetching location summary:', err);
        this.isLoading = false;
        this.AvgTAT = '00:00';
        this.AvgHoldtime = '00:00';
      }
    });
  }

  clearFilters(): void {
    this.fromInputDate = new Date();
    this.toInputDate = new Date();
    this.getLocationData();
  }

  applyFilter(event: Event) {
    const filterValue = (event.target as HTMLInputElement).value;
    this.dataSource.filter = filterValue.trim().toLowerCase();
    if (this.dataSource.paginator) {
      this.dataSource.paginator.firstPage();
    }
  }

  exportToExcel() {
    if (!this.ladleTransaction || this.ladleTransaction.length === 0) return;
    const worksheet: XLSX.WorkSheet = XLSX.utils.json_to_sheet(this.ladleTransaction);
    const workbook: XLSX.WorkBook = { Sheets: { 'LocationSummary': worksheet }, SheetNames: ['LocationSummary'] };
    XLSX.writeFile(workbook, `Location_${this.data}_Summary_${new Date().getTime()}.xlsx`);
  }
}
