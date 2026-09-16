import { Component, OnInit, ViewChild, AfterViewInit, OnDestroy } from '@angular/core';
import { MatPaginator } from '@angular/material/paginator';
import { MatTableDataSource } from '@angular/material/table';
import { MatSort } from '@angular/material/sort';
import { Subscription } from 'rxjs';
import * as XLSX from 'xlsx';
import { MainserviceService } from '../service/mainservice.service';
import { ThemeService } from '../service/theme.service';

@Component({
  selector: 'app-departmentreport',
  templateUrl: './departmentreport.component.html',
  styleUrls: ['./departmentreport.component.css'],
  standalone: false
})
export class DepartmentreportComponent implements OnInit, AfterViewInit, OnDestroy {
  public isLoading = false;
  public isDarkTheme = false;
  public fromInputDate: Date = new Date();
  public toInputDate: Date = new Date();
  public today: Date = new Date();
  public locName: string = '';
  public laderList: any[] = [];
  public productionInputData: any[] = [];
  public ladleTransaction: any[] = [];
  public hasSearched = false;

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
    private themeService: ThemeService
  ) { }

  ngOnInit(): void {
    this.themeSubscription = this.themeService.isDarkMode$.subscribe(
      (isDark) => { this.isDarkTheme = isDark; }
    );
    this.getLocations();
  }

  ngAfterViewInit() {
    this.dataSource.paginator = this.paginator;
    this.dataSource.sort = this.sort;
  }

  ngOnDestroy(): void {
    this.themeSubscription?.unsubscribe();
  }

  getLocations() {
    this.isLoading = true;
    this.mainService.getAllLocations().subscribe({
      next: (res: any) => {
        this.isLoading = false;
        this.laderList = res?.data ? res.data : (Array.isArray(res) ? res : []);
        var ladleNew = {
          LocationName: "All",
          LocationID: 0

        };
        this.laderList.push(ladleNew);

        if (this.laderList.length > 0 && !this.locName) {
          this.locName = this.laderList[0]?.LocationName || '';
          this.getFilteredData();
        }
      },
      error: () => {
        this.isLoading = false;
      }
    });
  }

  getFilteredData() {
    this.isLoading = true;
    this.hasSearched = true;
    const input = {
      fromDate: this.fromInputDate,
      toDate: this.toInputDate

    };

    this.mainService.getTransactionSumReport(input).subscribe({
      next: (res: any) => {
        this.isLoading = false;
        const payload = res?.data ? res.data : res;

        console.log(res.data);
        this.productionInputData = payload?.ProductionSummary || [];
        this.ladleTransaction = payload?.LadleTransactionList || (Array.isArray(payload) ? payload : []);

        if (res?.data.LadleTransactionDetails && Array.isArray(res.data.LadleTransactionDetails)) {
          this.ladleTransaction = [];
          res.data.LadleTransactionDetails.forEach((element: any) => {
            const lTrans = {
              TXNo: element.TXNo,
              SerialNumber: element.SerialNumber,
              LadleNo: element.LadleNo,
              SourceLocation: element.SourceLocation,
              SourceINDateSTR: element.SourceINDateSTR,
              SourceINTimeSTR: element.SourceINTimeSTR,
              SourceOUTDateSTR: element.SourceOUTDateSTR,
              SourceOUTTimeSTR: element.SourceOUTTimeSTR,
              SourceWeight: element.SourceWeight,
              SourceWeightDateTime: element.SourceWeightDateTime,
              CastNo: element.CastNo,
              TareWeight: element.TareWeight,
              TareWeightDateTime: element.TareWeightDateTime,
              NetWeight: element.NetWeight,
              TAT: element.TAT,
              DestinationLocation: element.DestinationLocation,
              DestinationINDateSTR: element.DestinationINDateSTR,
              DestinationINTimeSTR: element.DestinationINTimeSTR,
              DestinationOUTDateSTR: element.DestinationOUTDateSTR,
              DestinationOUTTimeSTR: element.DestinationOUTTimeSTR
            };

            if (lTrans.DestinationLocation === this.locName || this.locName === 'All') {
              this.ladleTransaction.push(lTrans);
            }
          });
        }

        this.dataSource = new MatTableDataSource<any>(this.ladleTransaction);
        //this.dataSource.paginator = this.paginator;
        this.dataSource.sort = this.sort;
        if (this.paginator) {
          this.paginator.firstPage();
        }

        this.dataSource.data = this.ladleTransaction;
        if (this.paginator) {
          this.paginator.firstPage();
        }
      },
      error: (err) => {
        console.error('Error fetching department report:', err);
        this.isLoading = false;
      }
    });
  }

  clearFilters(): void {
    this.fromInputDate = new Date();
    this.toInputDate = new Date();
    if (this.laderList.length > 0) {
      this.locName = this.laderList[0]?.LocationName || '';
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

  exportToExcel() {
    if (!this.ladleTransaction || this.ladleTransaction.length === 0) return;
    const worksheet: XLSX.WorkSheet = XLSX.utils.json_to_sheet(this.ladleTransaction);
    const workbook: XLSX.WorkBook = { Sheets: { 'DeptReport': worksheet }, SheetNames: ['DeptReport'] };
    XLSX.writeFile(workbook, `Department_${this.locName}_Report_${new Date().getTime()}.xlsx`);
  }
}
