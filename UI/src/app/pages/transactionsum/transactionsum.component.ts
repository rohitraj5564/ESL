import { Component, OnInit, ViewChild, AfterViewInit, OnDestroy, TemplateRef } from '@angular/core';
import { MatPaginator } from '@angular/material/paginator';
import { MatTableDataSource } from '@angular/material/table';
import { MatSort } from '@angular/material/sort';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { Subscription } from 'rxjs';
import * as XLSX from 'xlsx';
import { MainserviceService } from '../service/mainservice.service';
import { ThemeService } from '../service/theme.service';

export interface TransactionInput {
  fromDate: any;
  toDate: any;
  ladleNo?: string;
  location?: string;
}

@Component({
  selector: 'app-transactionsum',
  templateUrl: './transactionsum.component.html',
  styleUrls: ['./transactionsum.component.css'],
  standalone: false
})
export class TransactionsumComponent implements OnInit, AfterViewInit, OnDestroy {
  public isLoading = false;
  public isDarkTheme = false;
  public fromInputDate: Date = new Date();
  public toInputDate: Date = new Date();
  public today: Date = new Date();
  public ladleTransaction: any[] = [];
  public dataSource = new MatTableDataSource<any>([]);
  public limsDatas: any = {};
  public locName: string = '';
  public laderList: any[] = [];
  public hasSearched = false;

  private themeSubscription!: Subscription;

  displayedColumns: string[] = [
    'SerialNumber', 'TxNo', 'LadleNo', 'SourceLocation', 'SourceInDateTime',
    'SourceInTime', 'SourceOutDateTime', 'SourceOutTime', 'SourceWeight',
    'SourceWeightDateTime', 'CastNo', 'TareWeight', 'TareWeightDateTime',
    'NetWeight', 'TAT', 'DestinationLocation', 'DestinationInDateTime',
    'DestinationInTime', 'DestinationOutDateTime', 'DestinationOutTime', 'labDetails'
  ];

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  private currentDialogRef?: MatDialogRef<any>;

  constructor(
    public mainService: MainserviceService,
    private themeService: ThemeService,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.themeSubscription = this.themeService.isDarkMode$.subscribe(
      (isDark) => { this.isDarkTheme = isDark; }
    );
    this.getLocations();
    this.getFilteredData();
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
      },
      error: () => {
        this.isLoading = false;
      }
    });
  }

  getFilteredData() {
    this.isLoading = true;
    this.hasSearched = true;
    const input: TransactionInput = {
      fromDate: this.fromInputDate,
      toDate: this.toInputDate,
      location: this.locName
    };

    this.mainService.getTransactionSumReport(input).subscribe({
      next: (res: any) => {
        this.isLoading = false;
        const payload = res?.data ? res.data : res;
        this.ladleTransaction = payload?.LadleTransactionList || (Array.isArray(payload) ? payload : []);
        this.dataSource.data = this.ladleTransaction;
        if (this.paginator) {
          this.paginator.firstPage();
        }
      },
      error: (err) => {
        console.error('Error fetching transactions:', err);
        this.isLoading = false;
      }
    });
  }

  clearFilters(): void {
    this.fromInputDate = new Date();
    this.toInputDate = new Date();
    this.locName = '';
    this.getFilteredData();
  }

  applyFilter(event: Event) {
    const filterValue = (event.target as HTMLInputElement).value;
    this.dataSource.filter = filterValue.trim().toLowerCase();
    if (this.dataSource.paginator) {
      this.dataSource.paginator.firstPage();
    }
  }

  openLims(template: TemplateRef<any>, data: any) {
    this.limsDatas = data.LimsData || data;
    this.currentDialogRef = this.dialog.open(template, {
      width: '680px',
      maxWidth: '95vw',
      panelClass: 'custom-lims-dialog'
    });
  }

  closeDialog() {
    if (this.currentDialogRef) {
      this.currentDialogRef.close();
    }
  }

  exportToExcel() {
    if (!this.ladleTransaction || this.ladleTransaction.length === 0) return;
    const worksheet: XLSX.WorkSheet = XLSX.utils.json_to_sheet(this.ladleTransaction);
    const workbook: XLSX.WorkBook = { Sheets: { 'Transactions': worksheet }, SheetNames: ['Transactions'] };
    XLSX.writeFile(workbook, `Ladle_Transactions_${new Date().getTime()}.xlsx`);
  }
}
