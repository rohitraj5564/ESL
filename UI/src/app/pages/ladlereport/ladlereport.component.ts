import { Component, OnInit, ViewChild, AfterViewInit, OnDestroy } from '@angular/core';
import { MatPaginator } from '@angular/material/paginator';
import { MatTableDataSource } from '@angular/material/table';
import { MatSort } from '@angular/material/sort';
import { animate, state, style, transition, trigger } from '@angular/animations';
import { Subscription } from 'rxjs';
import * as XLSX from 'xlsx';
import { MainserviceService } from '../service/mainservice.service';
import { ThemeService } from '../service/theme.service';

@Component({
  selector: 'app-ladlereport',
  templateUrl: './ladlereport.component.html',
  styleUrls: ['./ladlereport.component.css'],
  standalone: false,
  animations: [
    trigger('detailExpand', [
      state('collapsed', style({ height: '0px', minHeight: '0', display: 'none' })),
      state('expanded', style({ height: '*' })),
      transition('expanded <=> collapsed', animate('225ms cubic-bezier(0.4, 0.0, 0.2, 1)')),
    ]),
  ],
})
export class LadlereportComponent implements OnInit, AfterViewInit, OnDestroy {
  public isLoading = false;
  public isDarkTheme = false;
  public fromInputDate: Date = new Date();
  public toInputDate: Date = new Date();
  public today: Date = new Date();
  public ladleName: string = '';
  public laderList: any[] = [];
  public productionInputData: any;
  public ladleTransaction: any[] = [];
  public expandedElement: any | null = null;
  public hasSearched = false;

  private themeSubscription!: Subscription;

  dataSource = new MatTableDataSource<any>([]);

  columnsToDisplay: string[] = [
    'expand', 'SerialNumber', 'TAT', 'LadleNo', 'SourceLocation',
    'SourceINDateSTR', 'SourceINTimeSTR', 'SourceOUTDateSTR', 'SourceOUTTimeSTR',
    'SourceWeight', 'SourceWeightDateTime', 'CastNo', 'TareWeight',
    'TareWeightDateTime', 'NetWeight', 'DestinationLocation',
    'DestinationINDateSTR', 'DestinationINTimeSTR', 'DestinationOUTDateSTR', 'DestinationOUTTimeSTR'
  ];

  innerDisplayedColumns: string[] = [
    'TAT', 'DestinationLocation', 'DestinationINDateSTR', 'DestinationINTimeSTR',
    'DestinationOUTDateSTR', 'DestinationOUTTimeSTR', 'GrossWeight',
    'TareWeight'
  ];

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  constructor(
    public mainService: MainserviceService,
    private themeService: ThemeService
  ) { }

  ngOnInit(): void {
    this.themeSubscription = this.themeService.isDarkMode$.subscribe(
      (isDark) => { this.isDarkTheme = isDark; }
    );
    this.getLadleDDL();
  }

  ngAfterViewInit() {
    this.dataSource.paginator = this.paginator;
    this.dataSource.sort = this.sort;
  }

  ngOnDestroy(): void {
    this.themeSubscription?.unsubscribe();
  }

  getLadleDDL() {
    this.isLoading = true;
    this.mainService.getLadleDDL().subscribe({
      next: (res: any) => {
        this.isLoading = false;
        this.laderList = res?.data ? res.data : (Array.isArray(res) ? res : []);
        if (this.laderList.length > 0 && !this.ladleName) {
          this.ladleName = this.laderList[0]?.Name || '';
          this.getFilteredData();
        }
      },
      error: () => {
        this.isLoading = false;
      }
    });
  }

  usersData: LadleTransactionDetails[] = [];

  USERS: any = [];
  analytisData: any = [];

  getFilteredData() {
    this.isLoading = true;
    this.hasSearched = true;

    this.USERS = [];
    this.usersData = [];


    const input = {
      fromDate: this.fromInputDate,
      toDate: this.toInputDate,
      ladleNo: this.laderList.filter(x => x.Name == this.ladleName)[0].LadleNo
    };

    this.mainService.getLadleReport(input).subscribe({
      next: (res: any) => {
        this.isLoading = false;
        const payload = res?.data ? res.data : res;
        this.productionInputData = payload?.ProductionSummary || payload?.productionSummary;
        this.ladleTransaction = payload?.LadleTransactionList || (Array.isArray(payload) ? payload : []);

        //console.log(res.data);
        this.analytisData = res.data['ExportString'];
        this.USERS = res.data['LadleTransactionDetails'];
        this.USERS.forEach((user: any) => {
          if (user['Destinations'] && Array.isArray(user['Destinations']) && user['Destinations'].length) {
            this.usersData = [...this.usersData, { ...user, destinations: new MatTableDataSource(user['Destinations']) }];
          } else {
            this.usersData = [...this.usersData, user];
          }
        });
        console.log(this.usersData);
        this.dataSource = new MatTableDataSource(this.usersData);
        //this.dataSource.data = this.ladleTransaction;
        if (this.paginator) {
          this.paginator.firstPage();
        }
      },
      error: (err) => {
        console.error('Error fetching ladle report:', err);
        this.isLoading = false;
      }
    });
  }

  clearFilters(): void {
    this.fromInputDate = new Date();
    this.toInputDate = new Date();
    if (this.laderList.length > 0) {
      this.ladleName = this.laderList[0]?.Name || '';
    }
    this.getFilteredData();
  }

  applyFilter(event: Event) {
    const filterValue = (event.target as HTMLInputElement).value;
    this.dataSource.filter = filterValue.trim().toLowerCase();
    if (this.dataSource.paginator) {
      this.dataSource.paginator.firstPage();
    }
  }

  toggleRow(element: any) {
    this.expandedElement = this.expandedElement === element ? null : element;
  }

  exportToExcel() {
    if (!this.ladleTransaction || this.ladleTransaction.length === 0) return;
    const worksheet: XLSX.WorkSheet = XLSX.utils.json_to_sheet(this.ladleTransaction);
    const workbook: XLSX.WorkBook = { Sheets: { 'LadleReport': worksheet }, SheetNames: ['LadleReport'] };
    XLSX.writeFile(workbook, `Ladle_Journey_${this.ladleName}_${new Date().getTime()}.xlsx`);
  }
}

export interface LadleTransactionDetails {

  SerialNumber: number;
  TXNo: number;
  LadleNo: string;
  SourceLocation: string;
  SourceInDateTimeSTR: string;
  SourceOutDateTimeSTR: string;


  SourceINDateSTR: string;
  SourceOUTDateSTR: string;
  SourceINTimeSTR: string;
  SourceOUTTimeSTR: string;

  SourceWeight: string;
  SourceWeightDateTime: string;
  CastNo: string;
  TareWeight: string;
  TareWeightDateTime: string;
  NetWeight: string;
  TAT: string;
  DestinationLocation: string;
  // DestinationInDateTimeSTR: string;
  // DestinationOutDateTimeSTR: string;


  DestinationINDateSTR: string;
  DestinationINTimeSTR: string;

  DestinationOUTDateSTR: string;
  DestinationOUTTimeSTR: string;


  HasMultipleDestination: boolean;
  HasLIMSData: boolean;
  LIMSData: string;
  LRSInDateTimeSTR: string;
  LRSOutDateTimeSTR: string;
  State: number;
  DisplayText: string;
  LRSInTime: string;
  LRSOutTime: string;

  destinations?: Destinations[] | MatTableDataSource<Destinations>;
}



export interface Destinations {
  TAT: string;
  DestinationLocation: string;
  DestinationINDateSTR: string;
  DestinationOUTDateSTR: string;

  DestinationINTimeSTR: string;
  DestinationOUTimeSTR: string;

  GrossWeight: string;
  GrossWeightDateTimeSTR: string;
  TareWeight: string;
  TareWeightDateTimeSTR: string;

}