import { Component, OnInit, ViewChild, AfterViewInit, OnDestroy } from '@angular/core';
import { MatPaginator } from '@angular/material/paginator';
import { MatTableDataSource } from '@angular/material/table';
import { MatSort } from '@angular/material/sort';
import { Subscription } from 'rxjs';
import * as XLSX from 'xlsx';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import { MainserviceService } from '../service/mainservice.service';
import { ThemeService } from '../service/theme.service';
import { ToastrService } from 'ngx-toastr';

export interface UserLoginSummaryItem {
  Username: string;
  Name: string;
  TotalLogins: number;
  TotalDurationSeconds: number;
  TotalDurationFormatted: string;
  LastLoginDateTime?: string;
  IsCurrentlyOnline: boolean;
}

export interface UserLoginDetailItem {
  Id: number;
  UserID?: string;
  Username: string;
  Name: string;
  LoginDateTime: string;
  LogoutDateTime?: string;
  TotalDuration: string;
  TotalDurationSeconds?: number;
  LogoutReason: string;
  IsLoggedIn: boolean;
  ServerDateTime?: string;
}

export interface UserOptionItem {
  Username: string;
  Name: string;
}

@Component({
  selector: 'app-user-login-history-report',
  templateUrl: './user-login-history-report.component.html',
  styleUrls: ['./user-login-history-report.component.css'],
  standalone: false
})
export class UserLoginHistoryReportComponent implements OnInit, AfterViewInit, OnDestroy {
  public isLoading = false;
  public isDarkTheme = false;

  // Date filters - default to rolling last 7 days
  public fromInputDate: Date = new Date(Date.now() - 7 * 24 * 60 * 60 * 1000);
  public toInputDate: Date = new Date();
  public today: Date = new Date();

  // User filter
  public selectedUser: string = 'ALL';
  public userList: UserOptionItem[] = [];

  // Data
  public summaryData: UserLoginSummaryItem[] = [];
  public detailsData: UserLoginDetailItem[] = [];
  public dataSource = new MatTableDataSource<UserLoginDetailItem>([]);
  public hasSearched = false;

  // Overall KPIs
  public totalSessions = 0;
  public totalDistinctUsers = 0;
  public totalDurationSeconds = 0;
  public totalDurationFormatted = '00h 00m 00s';
  public activeOnlineCount = 0;

  private themeSubscription!: Subscription;

  displayedColumns: string[] = [
    'SLNo',
    'Username',
    'Name',
    'LoginDateTime',
    'LogoutDateTime',
    'TotalDuration',
    'LogoutReason',
    'Status'
  ];

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  constructor(
    public mainService: MainserviceService,
    private themeService: ThemeService,
    private toastr: ToastrService
  ) {}

  ngOnInit(): void {
    this.themeSubscription = this.themeService.isDarkMode$.subscribe(
      (isDark) => { this.isDarkTheme = isDark; }
    );

    this.loadUserList();
    this.getReportData();
  }

  ngAfterViewInit(): void {
    this.dataSource.paginator = this.paginator;
    this.dataSource.sort = this.sort;
  }

  ngOnDestroy(): void {
    if (this.themeSubscription) {
      this.themeSubscription.unsubscribe();
    }
  }

  loadUserList(): void {
    this.mainService.getLoginReportUsers().subscribe({
      next: (res) => {
        if (res?.status && Array.isArray(res.data)) {
          this.userList = res.data;
        }
      },
      error: () => {}
    });
  }

  formatDateForApi(d: Date, isEnd: boolean = false): string {
    const year = d.getFullYear();
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    const time = isEnd ? '23:59:59' : '00:00:00';
    return `${year}-${month}-${day} ${time}`;
  }

  getReportData(): void {
    if (!this.fromInputDate || !this.toInputDate) {
      this.toastr.warning('Please select both From Date and To Date', 'Date Filter');
      return;
    }

    if (this.fromInputDate > this.toInputDate) {
      this.toastr.warning('From Date cannot be greater than To Date', 'Invalid Date Range');
      return;
    }

    this.isLoading = true;
    this.hasSearched = true;

    const fromStr = this.formatDateForApi(this.fromInputDate, false);
    const toStr = this.formatDateForApi(this.toInputDate, true);

    this.mainService.getUserLoginHistoryReport(fromStr, toStr, this.selectedUser).subscribe({
      next: (res) => {
        this.isLoading = false;
        if (res?.status && res.data) {
          this.summaryData = res.data.summary || [];
          this.detailsData = res.data.details || [];
          this.dataSource.data = this.detailsData;

          this.calculateKPIs();

          if (this.paginator) {
            this.dataSource.paginator = this.paginator;
            this.paginator.firstPage();
          }
          if (this.sort) {
            this.dataSource.sort = this.sort;
          }
        } else {
          this.summaryData = [];
          this.detailsData = [];
          this.dataSource.data = [];
          this.resetKPIs();
        }
      },
      error: (err) => {
        this.isLoading = false;
        this.summaryData = [];
        this.detailsData = [];
        this.dataSource.data = [];
        this.resetKPIs();
        this.toastr.error('Failed to load user login history report.', 'Error');
      }
    });
  }

  calculateKPIs(): void {
    this.totalSessions = this.detailsData.length;
    this.totalDistinctUsers = this.summaryData.length;
    this.totalDurationSeconds = this.summaryData.reduce((acc, curr) => acc + (curr.TotalDurationSeconds || 0), 0);
    this.activeOnlineCount = this.summaryData.filter(s => s.IsCurrentlyOnline).length;

    const hrs = String(Math.floor(this.totalDurationSeconds / 3600)).padStart(2, '0');
    const mins = String(Math.floor((this.totalDurationSeconds % 3600) / 60)).padStart(2, '0');
    const secs = String(this.totalDurationSeconds % 60).padStart(2, '0');
    this.totalDurationFormatted = `${hrs}h ${mins}m ${secs}s`;
  }

  resetKPIs(): void {
    this.totalSessions = 0;
    this.totalDistinctUsers = 0;
    this.totalDurationSeconds = 0;
    this.totalDurationFormatted = '00h 00m 00s';
    this.activeOnlineCount = 0;
  }

  applyFilter(event: Event): void {
    const filterValue = (event.target as HTMLInputElement).value;
    this.dataSource.filter = filterValue.trim().toLowerCase();

    if (this.dataSource.paginator) {
      this.dataSource.paginator.firstPage();
    }
  }

  resetFilters(): void {
    this.fromInputDate = new Date(Date.now() - 7 * 24 * 60 * 60 * 1000);
    this.toInputDate = new Date();
    this.selectedUser = 'ALL';
    this.getReportData();
  }

  // ==========================================
  // EXPORT TO PDF
  // Grouped by Individual User with Name & Total Duration
  // ==========================================
  exportToPDF(): void {
    if (!this.detailsData || this.detailsData.length === 0) {
      this.toastr.info('No login history records available to export.', 'Export PDF');
      return;
    }

    const doc = new jsPDF('landscape');

    // Header Banner
    doc.setFillColor(15, 23, 42); // Slate dark
    doc.rect(0, 0, 297, 26, 'F');

    doc.setFontSize(15);
    doc.setTextColor(255, 255, 255);
    doc.setFont('helvetica', 'bold');
    doc.text('Electrosteel Steels Limited - ESL Ladle Tracking System', 14, 11);

    doc.setFontSize(11);
    doc.setTextColor(148, 163, 184);
    doc.setFont('helvetica', 'normal');
    doc.text('User Login History & Duration Analysis Report', 14, 19);

    // Meta & Period Info
    doc.setFontSize(9);
    doc.setTextColor(51, 65, 85);
    const periodStr = `Period: ${this.fromInputDate.toLocaleDateString('en-GB')} to ${this.toInputDate.toLocaleDateString('en-GB')}   |   Generated: ${new Date().toLocaleString('en-GB')}   |   Filter: ${this.selectedUser === 'ALL' ? 'All Users' : this.selectedUser}`;
    doc.text(periodStr, 14, 32);

    // Overall KPI Strip
    const kpiSummary = `Total Sessions: ${this.totalSessions}   |   Total Time Spent: ${this.totalDurationFormatted}   |   Users: ${this.totalDistinctUsers}   |   Active Now: ${this.activeOnlineCount}`;
    doc.setFont('helvetica', 'bold');
    doc.setTextColor(30, 41, 59);
    doc.text(kpiSummary, 14, 38);
    doc.setFont('helvetica', 'normal');

    let currentY = 44;

    // SECTION 1: Summary Table by User
    doc.setFontSize(11);
    doc.setFont('helvetica', 'bold');
    doc.setTextColor(15, 23, 42);
    doc.text('1. User-Wise Total Duration & Session Summary', 14, currentY);
    currentY += 4;

    const summaryTableData = this.summaryData.map((s, idx) => [
      idx + 1,
      s.Username,
      s.Name || '-',
      s.TotalLogins,
      s.TotalDurationFormatted || '00h 00m 00s',
      s.LastLoginDateTime ? new Date(s.LastLoginDateTime).toLocaleString('en-GB') : '-',
      s.IsCurrentlyOnline ? 'Online' : 'Logged Out'
    ]);

    autoTable(doc, {
      startY: currentY,
      head: [['#', 'Username', 'Full Name', 'Total Logins', 'Total Duration Spent', 'Last Login Time', 'Status']],
      body: summaryTableData,
      theme: 'grid',
      headStyles: {
        fillColor: [30, 41, 59],
        textColor: [255, 255, 255],
        fontSize: 8.5,
        fontStyle: 'bold',
        halign: 'center'
      },
      styles: {
        fontSize: 8,
        cellPadding: 2.5
      },
      columnStyles: {
        0: { cellWidth: 10, halign: 'center' },
        1: { cellWidth: 35 },
        2: { cellWidth: 50 },
        3: { cellWidth: 28, halign: 'center' },
        4: { cellWidth: 40, halign: 'center', fontStyle: 'bold' },
        5: { cellWidth: 45, halign: 'center' },
        6: { cellWidth: 28, halign: 'center' }
      }
    });

    currentY = (doc as any).lastAutoTable.finalY + 12;

    // SECTION 2: Per-User Detailed Breakdown
    doc.setFontSize(11);
    doc.setFont('helvetica', 'bold');
    doc.setTextColor(15, 23, 42);
    doc.text('2. Individual User Detailed Session Logs', 14, currentY);
    currentY += 5;

    // Group session details by Username
    const userGroups = new Map<string, UserLoginDetailItem[]>();
    for (const item of this.detailsData) {
      const uname = item.Username;
      if (!userGroups.has(uname)) {
        userGroups.set(uname, []);
      }
      userGroups.get(uname)!.push(item);
    }

    // Render an individual section for each user
    userGroups.forEach((sessions, username) => {
      const userSummary = this.summaryData.find(s => s.Username.toUpperCase() === username.toUpperCase());
      const fullName = userSummary?.Name || sessions[0]?.Name || username;
      const totalDur = userSummary?.TotalDurationFormatted || '00h 00m 00s';
      const sessionCount = sessions.length;
      const isOnline = userSummary?.IsCurrentlyOnline || false;

      // Check remaining page space, add page if needed
      if (currentY > 165) {
        doc.addPage();
        currentY = 16;
      }

      // User Header Box
      doc.setFillColor(241, 245, 249);
      doc.rect(14, currentY, 269, 9, 'F');
      doc.setDrawColor(203, 213, 225);
      doc.rect(14, currentY, 269, 9, 'S');

      doc.setFontSize(9);
      doc.setFont('helvetica', 'bold');
      doc.setTextColor(30, 41, 59);
      doc.text(`User: ${fullName} (${username})   |   Total Sessions: ${sessionCount}   |   Total Time: ${totalDur}   |   Status: ${isOnline ? 'Online' : 'Offline'}`, 18, currentY + 6);

      currentY += 11;

      const userTableBody = sessions.map((s, sIdx) => [
        sIdx + 1,
        s.LoginDateTime ? new Date(s.LoginDateTime).toLocaleString('en-GB') : '-',
        s.LogoutDateTime ? new Date(s.LogoutDateTime).toLocaleString('en-GB') : (s.IsLoggedIn ? 'Session Ongoing' : '-'),
        s.TotalDuration || '-',
        s.LogoutReason || (s.IsLoggedIn ? 'Active Session' : '-'),
        s.IsLoggedIn ? 'Active' : 'Logged Out'
      ]);

      autoTable(doc, {
        startY: currentY,
        head: [['#', 'Login Date & Time', 'Logout Date & Time', 'Duration', 'Logout Reason', 'Status']],
        body: userTableBody,
        theme: 'striped',
        headStyles: {
          fillColor: [51, 65, 85],
          textColor: [255, 255, 255],
          fontSize: 7.5,
          halign: 'center'
        },
        styles: {
          fontSize: 7.5,
          cellPadding: 2
        },
        columnStyles: {
          0: { cellWidth: 10, halign: 'center' },
          1: { cellWidth: 50, halign: 'center' },
          2: { cellWidth: 50, halign: 'center' },
          3: { cellWidth: 35, halign: 'center', fontStyle: 'bold' },
          4: { cellWidth: 85 },
          5: { cellWidth: 35, halign: 'center' }
        }
      });

      currentY = (doc as any).lastAutoTable.finalY + 8;
    });

    // Save PDF
    const filename = `ESL_User_Login_History_Report_${new Date().getTime()}.pdf`;
    doc.save(filename);
    this.toastr.success('PDF report downloaded successfully.', 'Export Complete');
  }

  // ==========================================
  // EXPORT TO EXCEL
  // ==========================================
  exportToExcel(): void {
    if (!this.detailsData || this.detailsData.length === 0) {
      this.toastr.info('No login history records available to export.', 'Export Excel');
      return;
    }

    // Summary Sheet
    const summaryRows = this.summaryData.map((s, idx) => ({
      'SL.No': idx + 1,
      'Username': s.Username,
      'Full Name': s.Name || '-',
      'Total Logins': s.TotalLogins,
      'Total Duration': s.TotalDurationFormatted,
      'Total Seconds': s.TotalDurationSeconds,
      'Last Login Time': s.LastLoginDateTime ? new Date(s.LastLoginDateTime).toLocaleString('en-GB') : '-',
      'Current Status': s.IsCurrentlyOnline ? 'Online' : 'Logged Out'
    }));

    // Details Sheet
    const detailRows = this.detailsData.map((d, idx) => ({
      'SL.No': idx + 1,
      'Username': d.Username,
      'Full Name': d.Name || '-',
      'Login Time': d.LoginDateTime ? new Date(d.LoginDateTime).toLocaleString('en-GB') : '-',
      'Logout Time': d.LogoutDateTime ? new Date(d.LogoutDateTime).toLocaleString('en-GB') : (d.IsLoggedIn ? 'Ongoing' : '-'),
      'Duration': d.TotalDuration,
      'Duration (Seconds)': d.TotalDurationSeconds ?? '-',
      'Logout Reason': d.LogoutReason || '-',
      'Session Status': d.IsLoggedIn ? 'Active' : 'Terminated'
    }));

    const wb: XLSX.WorkBook = XLSX.utils.book_new();

    const wsSummary = XLSX.utils.json_to_sheet(summaryRows);
    wsSummary['!cols'] = [{ wch: 8 }, { wch: 20 }, { wch: 25 }, { wch: 14 }, { wch: 20 }, { wch: 14 }, { wch: 24 }, { wch: 16 }];
    XLSX.utils.book_append_sheet(wb, wsSummary, 'User Summary');

    const wsDetails = XLSX.utils.json_to_sheet(detailRows);
    wsDetails['!cols'] = [{ wch: 8 }, { wch: 20 }, { wch: 25 }, { wch: 22 }, { wch: 22 }, { wch: 18 }, { wch: 18 }, { wch: 35 }, { wch: 16 }];
    XLSX.utils.book_append_sheet(wb, wsDetails, 'Session Details');

    XLSX.writeFile(wb, `ESL_User_Login_History_Report_${new Date().getTime()}.xlsx`);
    this.toastr.success('Excel report downloaded successfully.', 'Export Complete');
  }
}
