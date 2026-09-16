import { CommonModule } from '@angular/common';
import { Component, ElementRef, QueryList, ViewChildren } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';

import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { MainserviceService } from '../service/mainservice.service';
import { SessionTimeoutService } from '../service/session-timeout.service';
import { Router } from '@angular/router';
import Swal from 'sweetalert2';
import { CustomCheckboxComponent } from '../custom-checkbox/custom-checkbox.component';
import { FooterComponent } from '../footer/footer.component';
import { v4 as uuidv4 } from 'uuid';
import { DateRangeDialogComponent } from '../date-range-dialog/date-range-dialog.component';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import * as XLSX from 'xlsx';

@Component({
  selector: 'app-esl-blast-furnace',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatCheckboxModule,
    MatDialogModule,
    CustomCheckboxComponent,
    FooterComponent
  ],
  templateUrl: './esl-blast-furnace.component.html',
  styleUrl: './esl-blast-furnace.component.css'
})
export class EslBlastFurnaceComponent {

  @ViewChildren('scrollableTable') scrollableTables!: QueryList<ElementRef>;

  private refreshInterval: any;

  showPopup = false;
  selectedLadleNo: string = '';
  isCastEditable: boolean = false;
  castNoPart1: string = '';
  castNoPart2: string = '';

  userName: string | null = null;
  userLocationName: string | null = null;
  userLocationId: number | null = null;
  showDownloadDropdown: boolean = false;
  availableLadles: string[] = [];
  emptyLadles: any[] = [];
  summary: any[] = [];
  availableSummary: any[] = [];
  emptySummary: any[] = [];
  completedSummary: any[] = [];
  transaction: any[] = [];

  prevCastNo: string = '';

  isChecked = false;
  indeterminate = false;

  // ==================== Reader Ladle Config ====================

  /**
   * ✅ LocationID mapping used by ESL_WEB_SP_GetReaderTransactionLadles.
   * BF1=9, BF2=1, BF3=2, SMS=4, DIP=5, PCM=6, LRS=7
   * The Empty Ladles Booking table shows the reversal-side locations only.
   */
  private readonly REVERSAL_LOCATION_IDS: number[] = [4, 5, 6, 7]; // SMS, DIP, PCM, LRS

  /** Display order: SMS → DIP → PCM → LRS (matches the Home dashboard) */
  private readonly LOCATION_ORDER: { [id: number]: number } = { 4: 1, 5: 2, 6: 3, 7: 4 };

  // ==================== End Reader Ladle Config ====================

  editChemical = {
    C: '', Si: '', Mn: '', S: '',
    P: '', Ti: '', Cr: '', SP: ''
  };

  castMasterChemical = {
    C: '', Si: '', Mn: '', S: '',
    P: '', Ti: '', Cr: '', SP: ''
  };

  isReportLoading: boolean = false;

  showPdfPreview: boolean = false;
  pdfPreviewUrl: SafeResourceUrl | null = null;
  private currentPdfBlob: Blob | null = null;
  private currentPdfBlobUrl: string | null = null;
  private currentReportBaseName: string = '';
  private currentReportHeaders: string[] = [];
  private currentReportRows: any[][] = [];

  constructor(
    private service: MainserviceService,
    private sessionTimeoutService: SessionTimeoutService,
    private router: Router,
    public dialog: MatDialog,
    private sanitizer: DomSanitizer
  ) { }

  ngOnInit(): void {
    this.fetchFurnaceDetails();
    this.getUserDetails();
    this.fetchTransactionLadles();
    this.fetchReaderEmptyLadles();          

    this.refreshInterval = setInterval(() => {
      this.fetchFurnaceDetails();
      this.getUserDetails();
      this.fetchTransactionLadles();
      this.fetchReaderEmptyLadles();        
    }, 3000);
  }

  ngOnDestroy(): void {
    if (this.refreshInterval) {
      clearInterval(this.refreshInterval);
    }
    this.revokePdfBlobUrl();
  }

  ngAfterViewInit() {
    const tableElements = this.scrollableTables.map(ref => ref.nativeElement);
    this.sessionTimeoutService.setupActivityTracking(tableElements);
  }

  get isWBAdmin(): boolean {
    return Number(sessionStorage.getItem('userLocationId') || 0) === 3;
  }

  get effectiveLocationId(): number {
    const loggedInLocationId = Number(
      sessionStorage.getItem('userLocationId') || 0
    );

    if (loggedInLocationId === 3) {
      return Number(
        sessionStorage.getItem('selectedLocationId') || loggedInLocationId
      );
    }

    return loggedInLocationId;
  }

  get effectiveLocationName(): string | null {
    const loggedInLocationId = Number(
      sessionStorage.getItem('userLocationId') || 0
    );

    if (loggedInLocationId === 3) {
      return (
        sessionStorage.getItem('selectedLocationName') ||
        sessionStorage.getItem('userLocationName')
      );
    }

    return sessionStorage.getItem('userLocationName');
  }

  getUserDetails() {
    this.userName = sessionStorage.getItem('userFirstName');
    this.userLocationId = Number(sessionStorage.getItem('userLocationId') || 0);

    this.userLocationName = this.effectiveLocationName;
  }

  // ==================== Empty Booking Table ====================
fetchReaderEmptyLadles(): void {
  this.service.getEmptyLadlesForBooking(this.effectiveLocationId).subscribe(
    (res: any) => {
      const rows: any[] = (res?.status && res?.data) ? res.data : [];

      const mapped = rows.map((r: any) => ({
        ladleNo:        r.ladleNo        ?? r.LadleNo,
        sourceId:       Number(r.locationID ?? r.LocationID),
        sourceName:     r.locationName   ?? r.LocationName,
        castNo:         r.castNo         ?? r.CastNo,
        serverDatetime: r.serverDatetime ?? r.ServerDatetime,
        isSelected:     false
      }));

      this.emptyLadles = this.preserveLadleSelection(this.emptyLadles, mapped);
      this.updateSelectAllCheckbox();
    },
    (error) => {
      console.error('[fetchReaderEmptyLadles] failed:', error);
    }
  );
}

  /** Carries `isSelected` forward when the polled list is replaced. */
  private preserveLadleSelection(oldList: any[], newList: any[]): any[] {
    return newList.map(item => {
      const existing = oldList.find(
        old => old.ladleNo === item.ladleNo && old.sourceId === item.sourceId
      );
      return { ...item, isSelected: existing ? existing.isSelected : false };
    });
  }

  // ==================== END: Reader Ladles for Empty Booking Table ====================

  toggleSelectAll(isChecked: boolean) {
    this.isChecked = isChecked;
    this.emptyLadles.forEach(ladle => (ladle.isSelected = this.isChecked));
  }

  updateSelectAllCheckbox() {
    if (this.emptyLadles.length === 0) {
      this.isChecked = false;
      this.indeterminate = false;
    } else {
      const allSelected = this.emptyLadles.every(ladle => ladle.isSelected);
      const someSelected = this.emptyLadles.some(ladle => ladle.isSelected);
      this.isChecked = allSelected;
      this.indeterminate = someSelected && !allSelected;
    }
  }

  onLadleCheckboxChange() {
    this.updateSelectAllCheckbox();
  }

  parseChemicalComp(item: any): any {
    const chemData: any = {
      'C%': 'NA', 'Si': 'NA', 'Mn': 'NA', 'S': 'NA',
      'P': 'NA', 'Ti': 'NA', 'Cr': 'NA', 'S+P': 'NA'
    };

    if (!item) return chemData;

    const chemicalComp = typeof item === 'string' ? item : item?.chemicalComp;

    if (chemicalComp && chemicalComp !== 'NA') {
      const pairs = chemicalComp.split(',').filter((p: string) => p.trim() !== '');
      pairs.forEach((pair: string) => {
        const [key, value] = pair.split(':');
        if (key && value && value.trim() !== 'NA') {
          const k = key.trim();
          const v = value.trim();
          if (k === 'C') chemData['C%'] = v;
          else chemData[k] = v;
        }
      });
    }

    const allNA = Object.values(chemData).every(v => v === 'NA');
    if (allNA && this.castMasterChemical.C) {
      chemData['C%'] = this.castMasterChemical.C || 'NA';
      chemData['Si'] = this.castMasterChemical.Si || 'NA';
      chemData['Mn'] = this.castMasterChemical.Mn || 'NA';
      chemData['S'] = this.castMasterChemical.S || 'NA';
      chemData['P'] = this.castMasterChemical.P || 'NA';
      chemData['Ti'] = this.castMasterChemical.Ti || 'NA';
      chemData['Cr'] = this.castMasterChemical.Cr || 'NA';
      chemData['S+P'] = this.castMasterChemical.SP || 'NA';
    }

    return chemData;
  }

  toggleExpand(ladle: any) {
    ladle.isExpanded = !ladle.isExpanded;
  }

  toggleCastEdit(): void {
    this.isCastEditable = !this.isCastEditable;
  }

  allowDecimalInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    let value = input.value;
    value = value.replace(/[^0-9.]/g, '');

    const parts = value.split('.');
    if (parts.length > 2) {
      value = parts[0] + '.' + parts.slice(1).join('');
    }

    if (value.length > 9) {
      value = value.substring(0, 9);
    }

    input.value = value;

    const fieldName = input.getAttribute('data-field');
    if (fieldName) {
      (this.editChemical as any)[fieldName] = value;
    }
  }

  fetchFurnaceDetails(): void {
    const clientDeviceID = 'device123';
    const userID = sessionStorage.getItem('userID');
    const locationID = this.effectiveLocationId;

    const preserveExpandedState = (oldList: any[], newList: any[]) => {
      return newList.map(item => {
        const existing = oldList.find(old => old.ladleNo === item.ladleNo);
        return {
          ...item,
          isExpanded: existing ? existing.isExpanded : false,
          chemicalData: this.parseChemicalComp(item.chemicalComp)
        };
      });
    };

    this.service.getFurnaceDashboardData(clientDeviceID, userID, locationID).subscribe((res: any) => {
      if (res?.status && res?.data) {

        // ✅ emptyLadles is no longer sourced from here — the Empty Ladles
        // Booking table is now driven by fetchReaderEmptyLadles()
        // (SMS / DIP / PCM / LRS from the reader transaction log).

        this.summary = res.data.summary || [];
        this.availableLadles = res.data.availableLadles?.ladles || [];
        this.prevCastNo = res.data.availableLadles?.prevCastNo || '';

        this.castMasterChemical = {
          C: res.data.availableLadles?.C || '',
          Si: res.data.availableLadles?.Si || '',
          Mn: res.data.availableLadles?.Mn || '',
          S: res.data.availableLadles?.S || '',
          P: res.data.availableLadles?.P || '',
          Ti: res.data.availableLadles?.Ti || '',
          Cr: res.data.availableLadles?.Cr || '',
          SP: res.data.availableLadles?.SP || '',
        };

        const newAvailableSummary = this.summary
          .filter((item: any) => item.type === 'Cast Assigned')
          .map((item: any) => ({ ...item }));
        this.availableSummary = preserveExpandedState(this.availableSummary, newAvailableSummary);

        const newEmptySummary = this.summary
          .filter((item: any) => item.type === 'Booked' || item.type === 'Assigned')
          .map((item: any) => ({ ...item }));
        this.emptySummary = preserveExpandedState(this.emptySummary, newEmptySummary);

        const now = new Date();
        const last24Hours = new Date(now.getTime() - 24 * 60 * 60 * 1000);
        this.completedSummary = this.summary
          .filter((item: any) => {
            if (item.type !== 'Completed') return false;
            if (!item.completedDateTime) return false;
            const completedDate = new Date(item.completedDateTime);
            return completedDate >= last24Hours;
          })
          .map((item: any) => ({ ...item }));
      }
    });
  }

  openPopup(ladleNo: string) {
    this.userLocationName = this.effectiveLocationName;
    this.selectedLadleNo = ladleNo;

    if (this.prevCastNo) {
      const [part1, part2] = this.prevCastNo.split('-');
      this.castNoPart1 = part1 || '';
      this.castNoPart2 = part2 || '';
    } else {
      this.castNoPart1 = '';
      this.castNoPart2 = '';
    }

    const ladleSummary = this.availableSummary.find(
      (item: any) => item.ladleNo === ladleNo
    );

    if (ladleSummary && ladleSummary.chemicalData) {
      this.editChemical = {
        C: ladleSummary.chemicalData['C%'] !== 'NA' ? ladleSummary.chemicalData['C%'] : '',
        Si: ladleSummary.chemicalData['Si'] !== 'NA' ? ladleSummary.chemicalData['Si'] : '',
        Mn: ladleSummary.chemicalData['Mn'] !== 'NA' ? ladleSummary.chemicalData['Mn'] : '',
        S: ladleSummary.chemicalData['S'] !== 'NA' ? ladleSummary.chemicalData['S'] : '',
        P: ladleSummary.chemicalData['P'] !== 'NA' ? ladleSummary.chemicalData['P'] : '',
        Ti: ladleSummary.chemicalData['Ti'] !== 'NA' ? ladleSummary.chemicalData['Ti'] : '',
        Cr: ladleSummary.chemicalData['Cr'] !== 'NA' ? ladleSummary.chemicalData['Cr'] : '',
        SP: ladleSummary.chemicalData['S+P'] !== 'NA' ? ladleSummary.chemicalData['S+P'] : '',
      };
    } else {
      this.editChemical = { ...this.castMasterChemical };
    }

    this.isCastEditable = false;
    this.showPopup = true;
  }

  closePopup() {
    this.showPopup = false;
    this.selectedLadleNo = '';
  }

  allowOnlyDigits(event: KeyboardEvent, allowZero: boolean = true): void {
    const charCode = event.which ? event.which : event.keyCode;
    if (charCode > 31 && (charCode < 48 || charCode > 57) || (!allowZero && charCode === 48)) {
      event.preventDefault();
    }
  }

  incrementCastNoPart1(): void {
    let num = parseInt(this.castNoPart1) || 0;
    num++;
    const newValue = num.toString();
    if (newValue.length > 8) {
      Swal.fire('Maximum digits reached', 'Cannot increment further.', 'warning');
    } else {
      this.castNoPart1 = newValue;
    }
  }

  incrementCastNoPart2(): void {
    let num = parseInt(this.castNoPart2);
    if (isNaN(num) || num < 1 || num > 9) {
      this.castNoPart2 = '1';
    } else if (num === 9) {
      this.castNoPart2 = '1';
    } else {
      this.castNoPart2 = (num + 1).toString();
    }
  }

  submitCastAssignment(): void {
    if (!this.castNoPart1) {
      Swal.fire('Warning', 'Please enter the cast number.', 'warning');
      return;
    }

    const part1 = this.castNoPart1.trim();
    const part2 = this.castNoPart2 ? this.castNoPart2.trim() : '';
    const castNo = part2 ? `${part1}-${part2}` : part1;

    Swal.fire({
      title: 'Confirm Cast Assignment',
      html: `Are you sure you want to assign the following?<br><br>
             <strong>Ladle No:</strong> ${this.selectedLadleNo}<br>
             <strong>Cast No:</strong> ${castNo}`,
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Okay',
      cancelButtonText: 'Cancel'
    }).then((result) => {
      if (result.isConfirmed) {
        const payload = {
          castID: uuidv4(),
          clientDeviceID: 'device123',
          userID: sessionStorage.getItem('userID'),
          sourceLocationID: this.effectiveLocationId,
          sourceLocationName: this.effectiveLocationName,
          castNo: castNo,
          ladleNo: this.selectedLadleNo,
          transactionDateTime: this.getFormattedLocalDateTime(),
          C: this.editChemical.C,
          Si: this.editChemical.Si,
          Mn: this.editChemical.Mn,
          S: this.editChemical.S,
          P: this.editChemical.P,
          Ti: this.editChemical.Ti,
          Cr: this.editChemical.Cr,
          SP: this.editChemical.SP
        };

        this.service.insertCastAssignment(payload).subscribe(
          (res: any) => {
            if (res.status) {
              Swal.fire({
                icon: 'success',
                title: 'Success',
                text: res.message || 'Cast number assigned successfully.',
                timer: 3000,
                showConfirmButton: false
              });
              this.closePopup();
              this.fetchFurnaceDetails();
            } else {
              Swal.fire('Error', res.message || 'Failed to assign cast number.', 'error');
            }
          },
          () => {
            Swal.fire('Error', 'Server error while assigning cast number.', 'error');
          }
        );
      }
    });
  }

  bookedAvailableLadles() {
  const selectedLadles = this.emptyLadles.filter(ladle => ladle.isSelected);
  if (selectedLadles.length === 0) {
    Swal.fire('No ladles selected', 'Please select at least one ladle to proceed.', 'warning');
    return;
  }

  Swal.fire({
    title: 'Are you sure?',
    html: `You are about to proceed with the following ladle(s): <strong>${selectedLadles.map(l => l.ladleNo).join(', ')}</strong>`,
    icon: 'question',
    showCancelButton: true,
    confirmButtonText: 'Yes, proceed',
    cancelButtonText: 'Cancel'
  }).then((result) => {
    if (result.isConfirmed) {
      const payload = {
        clientDeviceID: 'device123',
        userID: sessionStorage.getItem('userID'),
        requestLocationID: this.effectiveLocationId,   
        transactionDateTime: this.getFormattedLocalDateTime(),
        ladles: selectedLadles.map(ladle => ({
          sourceLocationID: ladle.sourceId,
          ladleNo: ladle.ladleNo
          
        }))
      };

      this.service.requestEmptyLadles(payload).subscribe(
        (res: any) => {
          if (res.status) {
            // remove the booked ladles immediately
            const keys = selectedLadles.map(l => `${l.sourceId}|${l.ladleNo}`);
            this.emptyLadles = this.emptyLadles.filter(
              l => keys.indexOf(`${l.sourceId}|${l.ladleNo}`) === -1
            );
            this.updateSelectAllCheckbox();

            Swal.fire({
              icon: 'success',
              title: 'Success',
              text: res.message || 'Submitted successfully.',
              timer: 3000,
              showConfirmButton: false
            });
          } else {
            Swal.fire({ icon: 'error', title: 'Error', text: res.message || 'Something went wrong.' });
          }
          this.fetchFurnaceDetails();
          this.fetchReaderEmptyLadles();
        },
        () => {
          Swal.fire({ icon: 'error', title: 'Server Error', text: 'Error connecting to server.' });
        }
      );
    }
  });
}

  deleteBookedReversalLadle(ladle: any) {
    Swal.fire({
      title: 'Are you sure?',
      html: `You are about to delete the booked ladle <strong>${ladle.ladleNo}</strong>.`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonText: 'Yes, proceed',
      cancelButtonText: 'Cancel'
    }).then((result) => {
      if (result.isConfirmed) {
        const payload = {
          clientDeviceID: 'device123',
          userID: sessionStorage.getItem('userID'),
          ladleNo: ladle.ladleNo,
          movementType: 'Available'
        };

        this.service.deleteBookedReversalLadle(payload).subscribe({
          next: (res: any) => {
            if (res.status) {
              Swal.fire({
                icon: 'success',
                title: 'Success',
                text: res.message || 'Ladle request deleted successfully.',
                timer: 3000,
                showConfirmButton: false
              });
              this.fetchFurnaceDetails();
              this.fetchReaderEmptyLadles();   // ✅ refresh the booking table too
            } else {
              Swal.fire({ icon: 'error', title: 'Error', text: res.message || 'Something went wrong.' });
            }
          },
          error: () => {
            Swal.fire('Error', 'Server error while deleting ladle request.', 'error');
          }
        });
      }
    });
  }

  fetchTransactionLadles(): void {
    const clientDeviceID = 'device123';
    const userID = sessionStorage.getItem('userID');
    const locationID = this.effectiveLocationId;

    this.service.getTransactionLadles(clientDeviceID, userID, locationID).subscribe((res: any) => {
      if (res?.status && res?.data) {
        this.transaction = res.data;
      } else {
        this.transaction = [];
      }
    });
  }

  openRequestReportDialog(): void {
    Swal.fire({
      title: 'Select Report Type',
      text: 'Which report would you like to download?',
      icon: 'question',
      showCancelButton: true,
      showDenyButton: true,
      confirmButtonText: 'Full Report',
      denyButtonText: 'Request Report',
      cancelButtonText: 'Cancel',
      confirmButtonColor: '#005ca8',
      denyButtonColor: '#3085d6',
      cancelButtonColor: '#d33'
    }).then((choice) => {
      if (choice.isConfirmed) {
        this.openDateRangeForReport('full');
      } else if (choice.isDenied) {
        this.openDateRangeForReport('request');
      }
    });
  }

  private openDateRangeForReport(reportType: 'full' | 'request'): void {
    const dialogRef = this.dialog.open(DateRangeDialogComponent, {
      width: '420px'
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result && result.fromDate && result.toDate) {
        const fromStr = this.formatDateForApi(result.fromDate);
        const toStr = this.formatDateForApi(result.toDate);

        if (reportType === 'full') {
          this.fetchAndGenerateFullReport(fromStr, toStr, result.fromDate, result.toDate);
        } else {
          this.fetchAndGenerateRequestReport(fromStr, toStr, result.fromDate, result.toDate);
        }
      }
    });
  }

  private fetchAndGenerateRequestReport(fromStr: string, toStr: string, fromDate: Date, toDate: Date): void {
    this.isReportLoading = true;
    const locationID = this.effectiveLocationId;

    this.service.getRequestReport(fromStr, toStr, locationID).subscribe(
      (res: any) => {
        this.isReportLoading = false;
        const data = res?.data || [];
        if (!data.length) {
          Swal.fire({
            icon: 'info',
            title: 'No Data',
            text: 'No requests found for the selected date range.',
            confirmButtonColor: '#005ca8'
          });
          return;
        }
        this.generateRequestReportPDF(data, fromDate, toDate);
      },
      (error) => {
        this.isReportLoading = false;
        Swal.fire({
          icon: 'error',
          title: 'Error',
          text: 'Failed to fetch request report. Please try again.',
          confirmButtonColor: '#005ca8'
        });
        console.error('Error fetching request report:', error);
      }
    );
  }

  private generateRequestReportPDF(data: any[], fromDate: Date, toDate: Date): void {
    const doc = new jsPDF({ orientation: 'landscape', unit: 'pt', format: 'a4' });

    doc.setFontSize(14);
    doc.text('Ladle Request - Report', 40, 40);
    doc.setFontSize(10);
    doc.text(`Date Range: ${this.formatDisplayDate(fromDate)} to ${this.formatDisplayDate(toDate)}`, 40, 58);
    doc.text(`Generated On: ${this.formatDisplayDate(new Date())}`, 40, 72);

    const headers = [
      'Trip No', 'Cast No', 'Source', 'Destination', 'Loco', 'Ladle No', 'Movement Type', 'Status',
      'C%', 'Si', 'Mn', 'S', 'P', 'Ti', 'Cr', 'S+P', 'Assigned', 'Requested', 'Completed'
    ];

    const tableBody = data.map((row: any) => [
      row.TripNo ?? 'NA',
      row.CastNo ?? 'NA',
      row.SourceLocation ?? 'NA',
      row.DestinationLocation ?? 'NA',
      row.LocoName ?? 'NA',
      row.LadleNo ?? 'NA',
      row.MovementType ?? 'NA',
      row.Status ?? 'NA',
      row.C ?? 'NA',
      row.Si ?? 'NA',
      row.Mn ?? 'NA',
      row.S ?? 'NA',
      row.P ?? 'NA',
      row.Ti ?? 'NA',
      row.Cr ?? 'NA',
      row.SP ?? 'NA',
      row.AssignedDateTime ? new Date(row.AssignedDateTime).toLocaleString() : 'NA',
      row.RequestedDateTime ? new Date(row.RequestedDateTime).toLocaleString() : 'NA',
      row.CompletedDateTime ? new Date(row.CompletedDateTime).toLocaleString() : 'NA'
    ]);

    autoTable(doc, {
      startY: 90,
      head: [headers],
      body: tableBody,
      styles: { fontSize: 7, cellPadding: 3 },
      headStyles: { fillColor: [0, 92, 168] },
      theme: 'grid'
    });

    const baseFileName = `Request_Report_${this.formatFileDate(fromDate)}_to_${this.formatFileDate(toDate)}`;
    this.previewReport(doc, baseFileName, headers, tableBody);
  }

  private fetchAndGenerateFullReport(fromStr: string, toStr: string, fromDate: Date, toDate: Date): void {
    this.isReportLoading = true;
    const locationID = this.effectiveLocationId;

    this.service.getPDAFullReport(fromStr, toStr, locationID).subscribe(
      (res: any) => {
        this.isReportLoading = false;
        const data = res?.data || [];
        if (!data.length) {
          Swal.fire({
            icon: 'info',
            title: 'No Data',
            text: 'No records found for the selected date range.',
            confirmButtonColor: '#005ca8'
          });
          return;
        }
        this.generateFullReportPDF(data, fromDate, toDate);
      },
      (error) => {
        this.isReportLoading = false;
        Swal.fire({
          icon: 'error',
          title: 'Error',
          text: 'Failed to fetch full report. Please try again.',
          confirmButtonColor: '#005ca8'
        });
        console.error('Error fetching full report:', error);
      }
    );
  }

  private generateFullReportPDF(data: any[], fromDate: Date, toDate: Date): void {
    const doc = new jsPDF({ orientation: 'landscape', unit: 'pt', format: 'a4' });

    doc.setFontSize(14);
    doc.text('Ladle Movement - Full Report', 40, 40);
    doc.setFontSize(10);
    doc.text(`Date Range: ${this.formatDisplayDate(fromDate)} to ${this.formatDisplayDate(toDate)}`, 40, 58);
    doc.text(`Generated On: ${this.formatDisplayDate(new Date())}`, 40, 72);

    const headers = [
      'Type', 'Trip No', 'Cast No', 'Source', 'Destination', 'Loco', 'Ladle No', 'Movement Type', 'Status',
      'C%', 'Si', 'Mn', 'S', 'P', 'Ti', 'Cr', 'S+P', 'Assigned', 'Requested', 'Completed'
    ];

    const tableBody = data.map((row: any) => [
      row.ReportType ?? 'NA',
      row.TripNo ?? 'NA',
      row.CastNo ?? 'NA',
      row.SourceLocation ?? 'NA',
      row.DestinationLocation ?? 'NA',
      row.LocoName ?? 'NA',
      row.LadleNo ?? 'NA',
      row.MovementType ?? 'NA',
      row.Status ?? 'NA',
      row.C ?? 'NA',
      row.Si ?? 'NA',
      row.Mn ?? 'NA',
      row.S ?? 'NA',
      row.P ?? 'NA',
      row.Ti ?? 'NA',
      row.Cr ?? 'NA',
      row.SP ?? 'NA',
      row.AssignedDateTime ? new Date(row.AssignedDateTime).toLocaleString() : 'NA',
      row.RequestedDateTime ? new Date(row.RequestedDateTime).toLocaleString() : 'NA',
      row.CompletedDateTime ? new Date(row.CompletedDateTime).toLocaleString() : 'NA'
    ]);

    autoTable(doc, {
      startY: 90,
      head: [headers],
      body: tableBody,
      styles: { fontSize: 6.5, cellPadding: 3 },
      headStyles: { fillColor: [0, 92, 168] },
      theme: 'grid'
    });

    const baseFileName = `Full_Report_${this.formatFileDate(fromDate)}_to_${this.formatFileDate(toDate)}`;
    this.previewReport(doc, baseFileName, headers, tableBody);
  }

  private formatDateForApi(date: Date): string {
    const y = date.getFullYear();
    const m = String(date.getMonth() + 1).padStart(2, '0');
    const d = String(date.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}`;
  }

  private formatDisplayDate(date: Date): string {
    return new Date(date).toLocaleString('en-GB', {
      day: '2-digit', month: '2-digit', year: 'numeric',
      hour: '2-digit', minute: '2-digit'
    });
  }

  private formatFileDate(date: Date): string {
    const d = new Date(date);
    return `${d.getFullYear()}${String(d.getMonth() + 1).padStart(2, '0')}${String(d.getDate()).padStart(2, '0')}`;
  }

  private previewReport(doc: jsPDF, baseFileName: string, headers: string[], rows: any[][]): void {
    this.revokePdfBlobUrl();

    this.currentPdfBlob = doc.output('blob');
    this.currentPdfBlobUrl = URL.createObjectURL(this.currentPdfBlob);
    this.pdfPreviewUrl = this.sanitizer.bypassSecurityTrustResourceUrl(this.currentPdfBlobUrl);
    this.currentReportBaseName = baseFileName;
    this.currentReportHeaders = headers;
    this.currentReportRows = rows;
    this.showPdfPreview = true;
  }

  toggleDownloadDropdown(): void {
    this.showDownloadDropdown = !this.showDownloadDropdown;
  }

  closeDownloadDropdown(): void {
    this.showDownloadDropdown = false;
  }

  downloadPreviewedPDF(): void {
    if (!this.currentPdfBlob) return;
    const link = document.createElement('a');
    link.href = this.currentPdfBlobUrl!;
    link.download = `${this.currentReportBaseName}.pdf`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }

  downloadPreviewedExcel(): void {
    if (!this.currentReportRows.length) return;

    const worksheetData = [this.currentReportHeaders, ...this.currentReportRows];
    const worksheet = XLSX.utils.aoa_to_sheet(worksheetData);
    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, worksheet, 'Report');
    XLSX.writeFile(workbook, `${this.currentReportBaseName}.xlsx`);
  }

  closePdfPreview(): void {
    this.showPdfPreview = false;
    this.showDownloadDropdown = false;
    this.revokePdfBlobUrl();
  }

  private revokePdfBlobUrl(): void {
    if (this.currentPdfBlobUrl) {
      URL.revokeObjectURL(this.currentPdfBlobUrl);
      this.currentPdfBlobUrl = null;
    }
    this.currentPdfBlob = null;
    this.pdfPreviewUrl = null;
    this.currentReportHeaders = [];
    this.currentReportRows = [];
  }

  getFormattedLocalDateTime(): string {
    const now = new Date();
    const pad = (n: number) => n.toString().padStart(2, '0');
    const yyyy = now.getFullYear();
    const MM = pad(now.getMonth() + 1);
    const dd = pad(now.getDate());
    const HH = pad(now.getHours());
    const mm = pad(now.getMinutes());
    const ss = pad(now.getSeconds());
    const ms = now.getMilliseconds().toString().padStart(3, '0');
    return `${yyyy}-${MM}-${dd} ${HH}:${mm}:${ss}.${ms}`;
  }
}