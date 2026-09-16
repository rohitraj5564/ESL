import { Component, ElementRef, QueryList, ViewChildren } from '@angular/core';
import { MainserviceService } from '../service/mainservice.service';
import Swal from 'sweetalert2';
import { MatDialog } from '@angular/material/dialog';
import { BfselectrdladleDialogComponent } from '../bfselectrdladle-dialog/bfselectrdladle-dialog.component';
import { Router } from '@angular/router';
import { ReversalladlewbDialogComponent } from '../reversalladlewb-dialog/reversalladlewb-dialog.component';
import { ToastrService } from 'ngx-toastr';
import { SessionTimeoutService } from '../service/session-timeout.service';
import { DateRangeDialogComponent } from '../date-range-dialog/date-range-dialog.component';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import * as XLSX from 'xlsx';
import { Overlay } from '@angular/cdk/overlay';

@Component({
  selector: 'app-home',
  standalone: false,
  templateUrl: './home.component.html',
  styleUrl: './home.component.css'
})
export class HomeComponent {

  // ==================== Properties ====================

  private refreshInterval: any;

  // BF ladle lists split by furnace
  bf1Ladles: any[] = [];
  bf2Ladles: any[] = [];
  bf3Ladles: any[] = [];

  // Select-all checkbox state for each table
  bf1SelectAll: boolean = false;
  bf2SelectAll: boolean = false;
  bf3SelectAll: boolean = false;
  reversalSelectAll: boolean = false;
  weighbridgeSelectAll: boolean = false;

  // Select-all checkbox state for SMS/DIP/PCM/LRS tables
  smsSelectAll: boolean = false;
  dipSelectAll: boolean = false;
  pcmSelectAll: boolean = false;
  lrsSelectAll: boolean = false;

  // Selected loco (per trip) for the Transfer Loco feature
  transferLocoSelection: { [tripID: string]: string } = {};

  // In-transit and completed loco movements
  intransitMovement: any[] = [];
  completedMovement: any[] = [];
  expandedTripIds: Set<string> = new Set();

  reversalLadle: any;
  isTransferring = false;

  // Checkbox disabled states
  areBFLadleCheckboxesDisabled = false;
  areReversalCheckboxesDisabled = false;
  crossLocationConfirmed: boolean = false;

  // Add button disabled states
  isAdd1ButtonDisabled = false;
  isAdd2ButtonDisabled = false;

  locoOccupied: any[] = [];
  ladleCountLocations: any[] = [];

  // ✅ NEW: Reader Transaction Ladles per location (from ReadersTransactionLog)
  readerBf1Ladles: any[] = [];
  readerBf2Ladles: any[] = [];
  readerBf3Ladles: any[] = [];
  readerSmsLadles: any[] = [];
  readerDipLadles: any[] = [];
  readerPcmLadles: any[] = [];
  readerLrsLadles: any[] = [];

  // Logged-in user details from session storage
  userName: string | null = null;
  userLocationName: string | null = null;
  userLocationId: number | null = null;

  // BF Chemical Composition popup state
  showBFPopup = false;
  bfChemicalData: any = {};

  // Footer year
  currentYear: number = new Date().getFullYear();

  // Transaction Report loading state
  isReportLoading: boolean = false;

  // PDF Preview modal state
  showPdfPreview: boolean = false;
  pdfPreviewUrl: SafeResourceUrl | null = null;
  showDownloadDropdown: boolean = false;
  private currentPdfBlob: Blob | null = null;
  private currentPdfBlobUrl: string | null = null;
  private currentReportBaseName: string = '';
  private currentReportHeaders: string[] = [];
  private currentReportRows: any[][] = [];

  @ViewChildren('scrollableTable') scrollableTables!: QueryList<ElementRef>;

  // ==================== End Properties ====================

  constructor(
    private service: MainserviceService,
    public dialog: MatDialog,
    private router: Router,
    private toastr: ToastrService,
    private sessionTimeoutService: SessionTimeoutService,
    private sanitizer: DomSanitizer,
    private overlay: Overlay
  ) { }

  // ==================== Lifecycle Hooks ====================

  ngOnInit(): void {
    this.getAllBFLadle();
    this.getLadleMovement();
    this.getAllOccupiedLoco();
    this.getAllLadleCountLocations();
    this.getUserDetails();
    this.getReversalLadlesWB();
    this.fetchReaderTransactionLadles(); // ✅ NEW

    this.sessionTimeoutService.initSessionTimeout();

    this.refreshInterval = setInterval(() => {
      this.getAllBFLadle();
      this.getLadleMovement();
      this.getAllOccupiedLoco();
      this.getAllLadleCountLocations();
      this.getUserDetails();
      this.getReversalLadlesWB();
      this.fetchReaderTransactionLadles(); // ✅ NEW
    }, 3000);
  }

  ngOnDestroy(): void {
    this.sessionTimeoutService.cleanup();
    if (this.refreshInterval) clearInterval(this.refreshInterval);
    this.revokePdfBlobUrl();
  }

  ngAfterViewInit() {
    const tableElements = this.scrollableTables.map(ref => ref.nativeElement);
    this.sessionTimeoutService.setupActivityTracking(tableElements);
    this.sessionTimeoutService.initSessionTimeout();
  }

  // ==================== End Lifecycle Hooks ====================

  // ==================== Dialog Repaint Safety Net ====================

  private forceRepaint(): void {
    requestAnimationFrame(() => {
      void document.body.offsetHeight; // safe reflow without display none
    });
  }

  // ==================== End Dialog Repaint Safety Net ====================

  // ==================== Error Handling ====================

  private handleError(error: any, context: string): void {
    let errorMessage = 'An unexpected error occurred. Please try again later.';
    let errorTitle = 'Error';
    if (!navigator.onLine) {
      errorMessage = 'No internet connection. Please check your network and try again.';
      errorTitle = 'Network Error';
    } else if (error.status === 0) {
      errorMessage = 'Unable to connect to the server. Please check your network connection.';
      errorTitle = 'Server Error';
    } else if (error.status >= 500) {
      errorMessage = 'Internal server error. Please try again later.';
      errorTitle = 'Internal Server Error';
    } else if (error.status === 408 || error.name === 'TimeoutError') {
      errorMessage = 'Request timed out. Please check your network and try again.';
      errorTitle = 'Request Timed Out';
    }
    this.toastr.error(errorMessage, errorTitle, { timeOut: 5000, closeButton: true, progressBar: true });
  }

  // ==================== End Error Handling ====================

  // ==================== User Session ====================

  getUserDetails() {
    this.userName = sessionStorage.getItem('userFirstName');
    this.userLocationName = sessionStorage.getItem('userLocationName');
    const userLocationId = sessionStorage.getItem('userLocationId');
    this.userLocationId = userLocationId ? parseInt(userLocationId, 10) : null;
  }

  // ==================== End User Session ====================

  // ==================== BF Ladles ====================

  getAllBFLadle() {
    const preserveCheckedState = (oldList: any[], newList: any[]) => {
      return newList.map(item => {
        const existing = oldList.find(old => old.ladleNo === item.ladleNo);
        return { ...item, checked: existing ? existing.checked : false };
      });
    };

    this.service.getAllBFLadle().subscribe(
      (res: any) => {
        const allData = res.data || [];
        this.bf1Ladles = preserveCheckedState(this.bf1Ladles, allData.filter((item: any) => item.locationName === 'BF1'));
        this.bf2Ladles = preserveCheckedState(this.bf2Ladles, allData.filter((item: any) => item.locationName === 'BF2'));
        this.bf3Ladles = preserveCheckedState(this.bf3Ladles, allData.filter((item: any) => item.locationName === 'BF3'));
      },
      (error) => { this.handleError(error, 'fetching BF ladle data'); }
    );
  }

  // ==================== End BF Ladles ====================

  // ==================== NEW: Reader Transaction Ladles ====================

  fetchReaderTransactionLadles(): void {
    this.service.getReaderTransactionLadles().subscribe(
      (res: any) => {
        if (res?.status && res?.data) {
          const data = res.data;
          // ✅ LocationID mapping: BF1=9, BF2=1, BF3=2, SMS=4, DIP=5, PCM=6, LRS=7
          this.readerBf1Ladles = data.filter((l: any) => l.locationID === 9);
          this.readerBf2Ladles = data.filter((l: any) => l.locationID === 1);
          this.readerBf3Ladles = data.filter((l: any) => l.locationID === 2);
          this.readerSmsLadles = data.filter((l: any) => l.locationID === 4);
          this.readerDipLadles = data.filter((l: any) => l.locationID === 5);
          this.readerPcmLadles = data.filter((l: any) => l.locationID === 6);
          this.readerLrsLadles = data.filter((l: any) => l.locationID === 7);
        }
      },
      (error) => { this.handleError(error, 'fetching reader transaction ladles'); }
    );
  }

  // ==================== END: Reader Transaction Ladles ====================

  // ==================== Loco Movement ====================

  toggleRowExpansion(tripID: string): void {
    if (this.expandedTripIds.has(tripID)) {
      this.expandedTripIds.delete(tripID);
    } else {
      this.expandedTripIds.add(tripID);
    }
  }

  trackByTripId(index: number, row: any): string {
    return row.tripID;
  }

  getLadleMovement() {
    this.service.getLadleMovement().subscribe(
      (data: any) => {
        this.intransitMovement = data.data.intransitMovement || [];
        this.completedMovement = data.data.completedmovement || [];
        this.validateTransferSelections();
      },
      (error) => { this.handleError(error, 'fetching ladle movement data'); }
    );
  }

  assignLoco(tripID: string, locoName: string, movementData: any[]): void {
    this.intransitMovement = this.intransitMovement.map(row =>
      row.tripID === tripID ? { ...row, isAssigning: true } : row
    );
    const ladleDetails = movementData
      .map(ladle => `${ladle.ladleNo} → ${ladle.sourceName} → ${ladle.destinationName}`)
      .join('<br>');

    Swal.fire({
      title: 'Confirm Loco Assignment',
      html: `Do you want to assign the loco <strong>${locoName}</strong> for Trip ID <strong>${tripID}</strong>?<br><br>${ladleDetails}`,
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Yes, Assign',
      cancelButtonText: 'No, Cancel',
      confirmButtonColor: '#3085d6',
      cancelButtonColor: '#d33'
    }).then((result) => {
      if (result.isConfirmed) {
        this.service.addAssignedLocoToLadle({ tripID, locoName }).subscribe(
          (response: any) => {
            Swal.fire({ icon: 'success', title: 'Success', text: 'Loco assigned successfully!', timer: 1500, showConfirmButton: false });
            this.getLadleMovement();
            this.getAllOccupiedLoco();
            this.intransitMovement = this.intransitMovement.map(row =>
              row.tripID === tripID ? { ...row, isAssigning: false } : row
            );
          },
          (error) => {
            Swal.fire({ icon: 'error', title: 'Error', text: 'Failed to assign loco. Please try again.', confirmButtonColor: '#3085d6' });
            this.intransitMovement = this.intransitMovement.map(row =>
              row.tripID === tripID ? { ...row, isAssigning: false } : row
            );
          }
        );
      } else {
        this.intransitMovement = this.intransitMovement.map(row =>
          row.tripID === tripID ? { ...row, isAssigning: false } : row
        );
      }
    });
  }

  getAvailableTransferLocos(currentLocoName: string): any[] {
    if (!this.locoOccupied || this.locoOccupied.length === 0) {
      return [];
    }
    const current = (currentLocoName || '').trim().toLowerCase();
    return this.locoOccupied.filter((loco: any) => {
      const name = (loco.LocoName || '').trim();
      const isCurrent = name.toLowerCase() === current;
      const isOccupied = Number(loco.IsOccupied) === 1;
      const isInTransit = (this.intransitMovement || []).some(
        (m: any) => (m.locoName || '').trim().toLowerCase() === name.toLowerCase()
      );
      return !isCurrent && !isOccupied && !isInTransit;
    });
  }

  validateTransferSelections(): void {
    if (!this.intransitMovement) return;
    for (const row of this.intransitMovement) {
      const selected = this.transferLocoSelection[row.tripID];
      if (selected) {
        const available = this.getAvailableTransferLocos(row.locoName);
        if (!available.some((l: any) => l.LocoName === selected)) {
          this.transferLocoSelection[row.tripID] = null as any;
        }
      }
    }
  }

  onTransferLoco(tripID: string, currentLoco: string, newLoco: string): void {
    if (!newLoco || newLoco === currentLoco) {
      Swal.fire({ icon: 'warning', title: 'Invalid Selection', text: 'Please select a different Loco to transfer.', confirmButtonColor: '#005ca8' });
      return;
    }

    const availableLocos = this.getAvailableTransferLocos(currentLoco);
    if (!availableLocos.some((l: any) => l.LocoName === newLoco)) {
      Swal.fire({
        icon: 'warning',
        title: 'Loco Unavailable',
        text: `Loco ${newLoco} is currently assigned or unavailable for transfer.`,
        confirmButtonColor: '#005ca8'
      });
      return;
    }

    Swal.fire({
      title: 'Confirm Loco Transfer',
      html: `Transfer Trip <strong>${tripID}</strong><br>From: <strong>${currentLoco}</strong><br>To: <strong>${newLoco}</strong>`,
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Yes, Transfer',
      cancelButtonText: 'Cancel',
      confirmButtonColor: '#023c6f',
      cancelButtonColor: '#d33'
    }).then((result) => {
      if (result.isConfirmed) {
        this.service.transferLoco({ tripID, currentLoco, newLoco }).subscribe({
          next: (res: any) => {
            if (res?.status) {
              Swal.fire({
                icon: 'success', title: 'Transferred!',
                text: `Trip ${tripID} transferred from ${currentLoco} to ${newLoco}`,
                confirmButtonColor: '#023c6f', timer: 2000, timerProgressBar: true
              });
              this.transferLocoSelection[tripID] = null as any;
              this.refreshAllData();
            } else {
              Swal.fire({ icon: 'error', title: 'Transfer Failed', text: res?.message || 'Something went wrong.', confirmButtonColor: '#005ca8' });
            }
          },
          error: () => {
            Swal.fire({ icon: 'error', title: 'Error', text: 'Failed to transfer Loco. Please try again.', confirmButtonColor: '#005ca8' });
          }
        });
      }
    });
  }

  private refreshAllData(): void {
    this.getAllBFLadle();
    this.getLadleMovement();
    this.getAllOccupiedLoco();
    this.getAllLadleCountLocations();
    this.getReversalLadlesWB();
    this.fetchReaderTransactionLadles();
  }

  deleteLadle(tripID: string, ladleNo: string, source: 'BF' | 'REVERSAL' = 'BF'): void {
    Swal.fire({
      title: 'Confirm Delete', text: `Do you want to delete Ladle ${ladleNo}?`,
      icon: 'warning', showCancelButton: true, confirmButtonText: 'Yes, Delete', cancelButtonText: 'Cancel'
    }).then((result) => {
      if (result.isConfirmed) {
        this.service.deleteLadleFromMovement({ TripID: tripID, LadleNo: ladleNo }).subscribe(
          (res: any) => {
            if (res.status) {
              Swal.fire({ icon: 'success', title: 'Deleted', text: res.message, timer: 1200, showConfirmButton: false });
              this.getAllBFLadle();
              this.getLadleMovement();
              this.getReversalLadlesWB();
              this.getAllOccupiedLoco();
            } else {
              Swal.fire('Error', res.message, 'error');
            }
          },
          (error) => { console.error(error); Swal.fire('Error', 'Delete failed!', 'error'); }
        );
      }
    });
  }

  getAllOccupiedLoco() {
    this.service.getAllOccupiedLoco().subscribe(
      (res: any) => {
        this.locoOccupied = res.data || [];
        this.validateTransferSelections();
      },
      (error) => { this.handleError(error, 'fetching occupied loco data'); }
    );
  }

  // ==================== End Loco Movement ====================

  // ==================== Reversal Ladles ====================

  getReversalLadlesWB() {
    const preserveCheckedState = (oldData: any[], newData: any[]) => {
      return newData.map(item => {
        const existing = oldData.find(old => old.ladleNo === item.ladleNo);
        return { ...item, checked: existing ? existing.checked : false };
      });
    };

    this.service.getReversalLadlesWB().subscribe((data: any) => {
      let newData = data?.data || [];
      const order: any = { 'Full Rejection': 1, 'Partial Consumption': 2, 'Full Consumption': 3, 'Available': 4 };
      newData = newData.sort((a: any, b: any) => (order[a.movementType] || 99) - (order[b.movementType] || 99));
      const oldData = this.reversalLadle?.data || [];
      newData = preserveCheckedState(oldData, newData);
      this.reversalLadle = { ...data, data: newData };
    });
  }

  // ==================== End Reversal Ladles ====================

  // ==================== SMS / DIP / PCM / LRS Table Filters ====================

  get smsLadles(): any[] { return this.reversalLadle?.data?.filter((row: any) => row.sourceName === 'SMS') || []; }
  get dipLadles(): any[] { return this.reversalLadle?.data?.filter((row: any) => row.sourceName === 'DIP') || []; }
  get pcmLadles(): any[] { return this.reversalLadle?.data?.filter((row: any) => row.sourceName === 'PCM') || []; }
  get lrsLadles(): any[] { return this.reversalLadle?.data?.filter((row: any) => row.sourceName === 'LRS') || []; }

  onSmsSelectAllChange() {
    this.smsLadles.forEach(row => { if (!this.areReversalCheckboxesDisabled) row.checked = this.smsSelectAll; });
    this.onReversalCheckboxChange();
  }

  onDipSelectAllChange() {
    this.dipLadles.forEach(row => { if (!this.areReversalCheckboxesDisabled) row.checked = this.dipSelectAll; });
    this.onReversalCheckboxChange();
  }

  onPcmSelectAllChange() {
    this.pcmLadles.forEach(row => { if (!this.areReversalCheckboxesDisabled) row.checked = this.pcmSelectAll; });
    this.onReversalCheckboxChange();
  }

  onLrsSelectAllChange() {
    this.lrsLadles.forEach(row => { if (!this.areReversalCheckboxesDisabled) row.checked = this.lrsSelectAll; });
    this.onReversalCheckboxChange();
  }

  // ==================== End SMS / DIP / PCM / LRS Table Filters ====================

  // ==================== Checkbox Logic ====================

  onLadleCheckboxChange() {
    const bf1Checked = this.bf1Ladles.some(ladle => ladle.checked);
    const bf2Checked = this.bf2Ladles.some(ladle => ladle.checked);
    const bf3Checked = this.bf3Ladles.some(ladle => ladle.checked);
    const selectedGroups = [bf1Checked, bf2Checked, bf3Checked].filter(val => val).length;

    this.bf1SelectAll = this.bf1Ladles.length > 0 && this.bf1Ladles.every(ladle => ladle.checked);
    this.bf2SelectAll = this.bf2Ladles.length > 0 && this.bf2Ladles.every(ladle => ladle.checked);
    this.bf3SelectAll = this.bf3Ladles.length > 0 && this.bf3Ladles.every(ladle => ladle.checked);

    if (selectedGroups <= 1) this.crossLocationConfirmed = false;

    if (selectedGroups > 1 && !this.crossLocationConfirmed) {
      Swal.fire({
        title: 'Cross Location Selection',
        text: 'You are selecting ladles across multiple BF locations. Do you want to continue?',
        icon: 'warning', showCancelButton: true, confirmButtonText: 'Yes, proceed', cancelButtonText: 'Cancel'
      }).then((result) => {
        if (result.isConfirmed) {
          this.crossLocationConfirmed = true;
        } else {
          if (bf1Checked && bf2Checked) { this.bf2Ladles.forEach(ladle => ladle.checked = false); }
          else if (bf1Checked && bf3Checked) { this.bf3Ladles.forEach(ladle => ladle.checked = false); }
          else if (bf2Checked && bf3Checked) { this.bf3Ladles.forEach(ladle => ladle.checked = false); }
          this.bf1SelectAll = this.bf1Ladles.every(ladle => ladle.checked);
          this.bf2SelectAll = this.bf2Ladles.every(ladle => ladle.checked);
          this.bf3SelectAll = this.bf3Ladles.every(ladle => ladle.checked);
        }
        this.updateSectionStates();
      });
    } else {
      this.updateSectionStates();
    }
  }

  onReversalCheckboxChange() {
    this.reversalSelectAll = this.reversalLadle?.data?.length > 0 && this.reversalLadle.data.every((row: any) => row.checked);
    this.smsSelectAll = this.smsLadles.length > 0 && this.smsLadles.every((row: any) => row.checked);
    this.dipSelectAll = this.dipLadles.length > 0 && this.dipLadles.every((row: any) => row.checked);
    this.pcmSelectAll = this.pcmLadles.length > 0 && this.pcmLadles.every((row: any) => row.checked);
    this.lrsSelectAll = this.lrsLadles.length > 0 && this.lrsLadles.every((row: any) => row.checked);
    this.updateSectionStates();
  }

  updateSectionStates() {
    const anyBFChecked = this.bf1Ladles.some(l => l.checked) || this.bf2Ladles.some(l => l.checked) || this.bf3Ladles.some(l => l.checked);
    const anyReversalChecked = this.reversalLadle?.data.some((row: { checked: any }) => row.checked);
    this.areReversalCheckboxesDisabled = anyBFChecked;
    this.areBFLadleCheckboxesDisabled = anyReversalChecked;
    this.isAdd1ButtonDisabled = anyReversalChecked;
    this.isAdd2ButtonDisabled = anyBFChecked;
  }

  onBF1SelectAllChange() {
    this.bf1Ladles.forEach(ladle => { if (!this.areBFLadleCheckboxesDisabled) ladle.checked = this.bf1SelectAll; });
    this.onLadleCheckboxChange();
  }

  onBF2SelectAllChange() {
    this.bf2Ladles.forEach(ladle => { if (!this.areBFLadleCheckboxesDisabled) ladle.checked = this.bf2SelectAll; });
    this.onLadleCheckboxChange();
  }

  onBF3SelectAllChange() {
    this.bf3Ladles.forEach(ladle => { if (!this.areBFLadleCheckboxesDisabled) ladle.checked = this.bf3SelectAll; });
    this.onLadleCheckboxChange();
  }

  onReversalSelectAllChange() {
    this.reversalLadle?.data.forEach((row: any) => { if (!this.areReversalCheckboxesDisabled) row.checked = this.reversalSelectAll; });
    this.onReversalCheckboxChange();
  }

  getSelectedLadle() {
    return [...this.bf1Ladles, ...this.bf2Ladles, ...this.bf3Ladles].filter(ladle => ladle.checked);
  }

  onAddBFLadlesClick() {
    const selectedLadles = this.getSelectedLadle();
    if (selectedLadles.length > 0) { this.openDialogBFLadles(selectedLadles); }
    else { Swal.fire('Please select a ladle first!'); }
  }

  openDialogBFLadles(selectedLadles: any[]): void {
    // ✅ never allow a second dialog to stack on top of an open one
    if (this.dialog.openDialogs.length > 0) { return; }

    const dialogRef = this.dialog.open(BfselectrdladleDialogComponent, {
      data: selectedLadles,
      restoreFocus: false,
      scrollStrategy: this.overlay.scrollStrategies.noop()
    });

    dialogRef.afterClosed().subscribe((result) => {
      this.forceRepaint();   // ✅ clears any leftover ghost layer

      if (result === true) {
        this.resetAllSelections();
        this.getAllBFLadle();
        this.getLadleMovement();

        Swal.fire({
          icon: 'success',
          title: 'Success',
          text: 'Ladle movement created successfully.',
          timer: 2000,
          showConfirmButton: false
        });
      }
    });
  }

  getReversalSelectedLadle() {
    return this.reversalLadle?.data?.filter((ladle: any) => ladle.checked) || [];
  }

  onAddReversalLadlesClick() {
    const selectedReversalLadles = this.getReversalSelectedLadle();
    if (selectedReversalLadles.length > 0) { this.openDialogReversalLadles(selectedReversalLadles); }
    else { Swal.fire('Please select a ladle first!'); }
  }

  openDialogReversalLadles(selectedReversalLadles: any[]): void {
    // ✅ never allow a second dialog to stack on top of an open one
    if (this.dialog.openDialogs.length > 0) { return; }

    const dialogRef = this.dialog.open(ReversalladlewbDialogComponent, {
      data: selectedReversalLadles,
      restoreFocus: false,
      scrollStrategy: this.overlay.scrollStrategies.noop()
    });

    dialogRef.afterClosed().subscribe((result) => {
      this.forceRepaint();   // ✅ clears any leftover ghost layer

      if (result === true) {
        this.resetAllSelections();
        this.getReversalLadlesWB();
        this.getLadleMovement();
      }
    });
  }

  private resetAllSelections(): void {
    this.areBFLadleCheckboxesDisabled = false;
    this.areReversalCheckboxesDisabled = false;
    this.isAdd1ButtonDisabled = false;
    this.isAdd2ButtonDisabled = false;
    this.bf1SelectAll = false;
    this.bf2SelectAll = false;
    this.bf3SelectAll = false;
    this.reversalSelectAll = false;
    this.smsSelectAll = false;
    this.dipSelectAll = false;
    this.pcmSelectAll = false;
    this.lrsSelectAll = false;
    this.weighbridgeSelectAll = false;
  }

  getAllLadleCountLocations() {
    this.service.getAllLadleCountLocations().subscribe(
      (res: any) => { this.ladleCountLocations = res.data || []; },
      (error) => { this.handleError(error, 'fetching ladle count locations data'); }
    );
  }

  openBFPopup(row: any) {
    this.bfChemicalData = {
      'LadleNo': row.ladleNo, 'C%': row.C, 'SI': row.SI,
      'Mn': row.Mn, 'S': row.S, 'P': row.P, 'Ti': row.Ti, 'Cr': row.Cr, 'S+P': row.SP
    };
    this.showBFPopup = true;
  }

  closeBFPopup() { this.showBFPopup = false; }

  // ==================== Transaction Report ====================

  openTransactionReportDialog(): void {
    Swal.fire({
      title: 'Select Report Type',
      text: 'Which report would you like to download?',
      icon: 'question',
      showCancelButton: true,
      showDenyButton: true,
      confirmButtonText: 'Full Report',
      denyButtonText: 'Transaction Report',
      cancelButtonText: 'Cancel',
      confirmButtonColor: '#005ca8',
      denyButtonColor: '#3085d6',
      cancelButtonColor: '#d33'
    }).then((choice) => {
      if (choice.isConfirmed) { this.openDateRangeForReport('full'); }
      else if (choice.isDenied) { this.openDateRangeForReport('transaction'); }
    });
  }

  private openDateRangeForReport(reportType: 'full' | 'transaction'): void {
    const dialogRef = this.dialog.open(DateRangeDialogComponent, {
      width: '420px',
      scrollStrategy: this.overlay.scrollStrategies.noop()
    });
    dialogRef.afterClosed().subscribe((result) => {
      this.forceRepaint();   // ✅ same safety net for the date-range dialog

      if (result && result.fromDate && result.toDate) {
        const fromStr = this.formatDateForApi(result.fromDate);
        const toStr = this.formatDateForApi(result.toDate);
        if (reportType === 'full') { this.fetchAndGenerateFullReport(fromStr, toStr, result.fromDate, result.toDate); }
        else { this.fetchAndGenerateReport(fromStr, toStr, result.fromDate, result.toDate); }
      }
    });
  }

  private fetchAndGenerateReport(fromStr: string, toStr: string, fromDate: Date, toDate: Date): void {
    this.isReportLoading = true;
    this.service.getTransactionReport(fromStr, toStr).subscribe(
      (res: any) => {
        this.isReportLoading = false;
        const data = res?.data || [];
        if (!data.length) {
          Swal.fire({ icon: 'info', title: 'No Data', text: 'No transactions found for the selected date range.', confirmButtonColor: '#005ca8' });
          return;
        }
        this.generateTransactionReportPDF(data, fromDate, toDate);
      },
      (error) => { this.isReportLoading = false; this.handleError(error, 'fetching transaction report'); }
    );
  }

  private generateTransactionReportPDF(data: any[], fromDate: Date, toDate: Date): void {
    const doc = new jsPDF({ orientation: 'landscape', unit: 'pt', format: 'a4' });
    doc.setFontSize(14);
    doc.text('Ladle Movement - Transaction Report', 40, 40);
    doc.setFontSize(10);
    doc.text(`Date Range: ${this.formatDisplayDate(fromDate)} to ${this.formatDisplayDate(toDate)}`, 40, 58);
    doc.text(`Generated On: ${this.formatDisplayDate(new Date())}`, 40, 72);

    const headers = ['Trip No', 'Cast No', 'Loco', 'Ladle No', 'Source', 'Destination', 'Movement Type',
      'C%', 'Si', 'Mn', 'S', 'P', 'Ti', 'Cr', 'S+P', 'Assigned', 'Completed'];

    const tableBody = data.map((row: any) => [
      row.TripNo ?? 'NA', row.CastNo ?? 'NA', row.LocoName ?? 'NA', row.LadleNo ?? 'NA',
      row.SourceLocation ?? 'NA', row.DestinationLocation ?? 'NA', row.MovementType ?? 'NA',
      row.C ?? 'NA', row.Si ?? 'NA', row.Mn ?? 'NA', row.S ?? 'NA', row.P ?? 'NA',
      row.Ti ?? 'NA', row.Cr ?? 'NA', row.SP ?? 'NA',
      row.AssignedDateTime ? new Date(row.AssignedDateTime).toLocaleString() : 'NA',
      row.CompletedDateTime ? new Date(row.CompletedDateTime).toLocaleString() : 'NA'
    ]);

    autoTable(doc, { startY: 90, head: [headers], body: tableBody, styles: { fontSize: 7, cellPadding: 3 }, headStyles: { fillColor: [0, 92, 168] }, theme: 'grid' });
    this.previewReport(doc, `Transaction_Report_${this.formatFileDate(fromDate)}_to_${this.formatFileDate(toDate)}`, headers, tableBody);
  }

  private fetchAndGenerateFullReport(fromStr: string, toStr: string, fromDate: Date, toDate: Date): void {
    this.isReportLoading = true;
    this.service.getFullReport(fromStr, toStr).subscribe(
      (res: any) => {
        this.isReportLoading = false;
        const data = res?.data || [];
        if (!data.length) {
          Swal.fire({ icon: 'info', title: 'No Data', text: 'No records found for the selected date range.', confirmButtonColor: '#005ca8' });
          return;
        }
        this.generateFullReportPDF(data, fromDate, toDate);
      },
      (error) => { this.isReportLoading = false; this.handleError(error, 'fetching full report'); }
    );
  }

  private generateFullReportPDF(data: any[], fromDate: Date, toDate: Date): void {
    const doc = new jsPDF({ orientation: 'landscape', unit: 'pt', format: 'a4' });
    doc.setFontSize(14);
    doc.text('Ladle Movement - Full Report', 40, 40);
    doc.setFontSize(10);
    doc.text(`Date Range: ${this.formatDisplayDate(fromDate)} to ${this.formatDisplayDate(toDate)}`, 40, 58);
    doc.text(`Generated On: ${this.formatDisplayDate(new Date())}`, 40, 72);

    const headers = ['Type', 'Trip No', 'Cast No', 'Loco', 'Ladle No', 'Source', 'Destination',
      'Movement Type', 'Status', 'C%', 'Si', 'Mn', 'S', 'P', 'Ti', 'Cr', 'S+P', 'Assigned', 'Requested', 'Completed'];

    const tableBody = data.map((row: any) => [
      row.ReportType ?? 'NA', row.TripNo ?? 'NA', row.CastNo ?? 'NA', row.LocoName ?? 'NA',
      row.LadleNo ?? 'NA', row.SourceLocation ?? 'NA', row.DestinationLocation ?? 'NA',
      row.MovementType ?? 'NA', row.Status ?? 'NA',
      row.C ?? 'NA', row.Si ?? 'NA', row.Mn ?? 'NA', row.S ?? 'NA', row.P ?? 'NA',
      row.Ti ?? 'NA', row.Cr ?? 'NA', row.SP ?? 'NA',
      row.AssignedDateTime ? new Date(row.AssignedDateTime).toLocaleString() : 'NA',
      row.RequestedDateTime ? new Date(row.RequestedDateTime).toLocaleString() : 'NA',
      row.CompletedDateTime ? new Date(row.CompletedDateTime).toLocaleString() : 'NA'
    ]);

    autoTable(doc, { startY: 90, head: [headers], body: tableBody, styles: { fontSize: 6.5, cellPadding: 3 }, headStyles: { fillColor: [0, 92, 168] }, theme: 'grid' });
    this.previewReport(doc, `Full_Report_${this.formatFileDate(fromDate)}_to_${this.formatFileDate(toDate)}`, headers, tableBody);
  }

  private formatDateForApi(date: Date): string {
    const y = date.getFullYear();
    const m = String(date.getMonth() + 1).padStart(2, '0');
    const d = String(date.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}`;
  }

  private formatDisplayDate(date: Date): string {
    return new Date(date).toLocaleString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
  }

  private formatFileDate(date: Date): string {
    const d = new Date(date);
    return `${d.getFullYear()}${String(d.getMonth() + 1).padStart(2, '0')}${String(d.getDate()).padStart(2, '0')}`;
  }

  // ==================== PDF Preview Modal ====================

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

  toggleDownloadDropdown(): void { this.showDownloadDropdown = !this.showDownloadDropdown; }
  closeDownloadDropdown(): void { this.showDownloadDropdown = false; }

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
    if (this.currentPdfBlobUrl) { URL.revokeObjectURL(this.currentPdfBlobUrl); this.currentPdfBlobUrl = null; }
    this.currentPdfBlob = null;
    this.pdfPreviewUrl = null;
    this.currentReportHeaders = [];
    this.currentReportRows = [];
  }

  // ==================== End PDF Preview ====================

  logout() {
    Swal.fire({
      title: 'Are you sure?', text: 'Do you want to logout?', icon: 'warning',
      showCancelButton: true, confirmButtonText: 'Yes, logout', cancelButtonText: 'No, stay logged in'
    }).then((result) => {
      if (result.isConfirmed) {
        if (this.refreshInterval) clearInterval(this.refreshInterval);
        this.service.logout();
      }
    });
  }
}