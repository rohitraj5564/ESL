import { CommonModule } from '@angular/common';
import { Component, ElementRef, QueryList, ViewChildren, OnDestroy, OnInit } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MainserviceService } from '../service/mainservice.service';
import { SessionTimeoutService } from '../service/session-timeout.service';
import { Router } from '@angular/router';
import { CustomCheckboxComponent } from '../custom-checkbox/custom-checkbox.component';
import { DateRangeDialogComponent } from '../date-range-dialog/date-range-dialog.component';
import Swal from 'sweetalert2';
import { v4 as uuidv4 } from 'uuid';
import { FormsModule } from '@angular/forms';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import * as XLSX from 'xlsx';
import { FooterComponent } from '../footer/footer.component';

@Component({
  selector: 'app-esl-production',
  standalone: true,
  imports: [MatCheckboxModule, MatCardModule, MatDialogModule, CommonModule, CustomCheckboxComponent, FormsModule, FooterComponent],
  templateUrl: './esl-production.component.html',
  styleUrls: ['./esl-production.component.css']
})
export class EslProductionComponent implements OnInit, OnDestroy {

  @ViewChildren('scrollableTable') scrollableTables!: QueryList<ElementRef>;

  // Auto-refresh interval handle.
  private refreshInterval: any;

  // Session / user info.
  userName: string | null = null;
  userLocationName: string | null = null;
  userLocationId: number | null = null;


  ladleCount: number | null = null;
  isCompositionLocked: boolean = false;

  // Table data.
  reversalLadles: any[] = [];
  availableLadles: any[] = [];
  bookedSummary: any[] = [];
  pickupSummary: any[] = [];
  requestedLadles: any[] = [];
  pendingRequests: any[] = [];
  completedRequests: any[] = [];
  transaction: any[] = [];

  // ==================== NEW: Available Ladles sources ====================

  /** Rows from GetProductionDetails (the original source). */
  private prodAvailableLadles: any[] = [];

  /** ✅ NEW: BF rows from GetAllBFLadle - the same API Home's BF1/BF2/BF3 tables use. */
  private bfAvailableLadles: any[] = [];

  /**
   * ✅ Fallback map for BF LocationID when GetAllBFLadle does not return one.
   * The 'Casted' branch of ESL_PDA_SP_RequestReversalLadles_V1 filters on
   * LocationID = @sourceLocationID, so this must be right or the booking
   * silently updates zero rows.
   * BF1 = 9, BF2 = 1, BF3 = 2  (matches the Locations table)
   */
  private readonly BF_LOCATION_IDS: { [name: string]: number } = {
    'BF1': 9,
    'BF2': 1,
    'BF3': 2
  };

  // ==================== END: Available Ladles sources ====================

  isChecked = false;
  indeterminate = false;

  showPopup = false;
  chemicalData: any = {};

  showEditPopup: boolean = false;
  selectedLadle: any = null;
  editLadleCount: string = '';

  // Chemical fields for the edit popup.
  editChemical: any = {
    C: '', Si: '', Mn: '', S: '',
    P: '', Ti: '', Cr: '', SP: ''
  };

  // Request Report loading state
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

  constructor(
    private service: MainserviceService,
    private sessionTimeoutService: SessionTimeoutService,
    private router: Router,
    public dialog: MatDialog,
    private sanitizer: DomSanitizer
  ) { }

  private get isWBAdmin(): boolean {
    return Number(sessionStorage.getItem('userLocationId') || 0) === 3;
  }

  private get effectiveLocationId(): number {
    const loggedInLocationId = Number(sessionStorage.getItem('userLocationId') || 0);
    const selectedLocationId = Number(sessionStorage.getItem('selectedLocationId') || 0);

    if (loggedInLocationId === 3 && selectedLocationId > 0) {
      return selectedLocationId;
    }

    return loggedInLocationId;
  }

  private get effectiveLocationName(): string {
    const loggedInLocationId = Number(sessionStorage.getItem('userLocationId') || 0);
    const selectedLocationName = sessionStorage.getItem('selectedLocationName');
    const loggedInLocationName = sessionStorage.getItem('userLocationName');

    if (loggedInLocationId === 3 && selectedLocationName) {
      return selectedLocationName;
    }

    return loggedInLocationName || '';
  }

  // Lifecycle Hooks. 

  ngOnInit(): void {
    this.getLadleRequest();
    this.getUserDetails();
    this.fetchProductionDetails();
    this.fetchBFLadles();                 // ✅ NEW
    this.getRequestedLadles();
    this.fetchTransactionLadles();
    this.sessionTimeoutService.initSessionTimeout();

    // Auto-refresh all data every 3 seconds.
    this.refreshInterval = setInterval(() => {
      this.getLadleRequest();
      this.getUserDetails();
      this.fetchTransactionLadles();
      this.fetchProductionDetails();
      this.fetchBFLadles();               // ✅ NEW
      this.getRequestedLadles();
    }, 3000);
  }

  ngOnDestroy(): void {
    this.sessionTimeoutService.cleanup();
    if (this.refreshInterval) {
      clearInterval(this.refreshInterval);
    }
    this.revokePdfBlobUrl();
  }

  ngAfterViewInit() {
    const tableElements = this.scrollableTables.map(ref => ref.nativeElement);
    this.sessionTimeoutService.setupActivityTracking(tableElements);
    this.sessionTimeoutService.initSessionTimeout();
  }

  // Session / User details.

  getUserDetails() {
    this.userName = sessionStorage.getItem('userFirstName');
    this.userLocationName = this.effectiveLocationName;
    this.userLocationId = this.effectiveLocationId;
  }

  // ==================== NEW: BF Ladles for the Available table ====================

  /**
   * ✅ Loads the BF1 / BF2 / BF3 ladles shown on the Home dashboard and feeds
   * them into the Available Ladles table so SMS / DIP / PCM can request them.
   *
   * Ladles already booked by another location (BookingLocation !== 'NA') are
   * excluded - they are not available to request.
   *
   * Each row is mapped onto the shape the existing template and Request
   * payload already use:
   *   sourceLocation -> "Location" column
   *   movementType   -> "Type" column, and drives the SP's 'Casted' branch
   *   locationID     -> sent as sourceLocationID
   *   chemicalData   -> the Chemical Composition expand row
   */
  fetchBFLadles(): void {
    this.service.getAllBFLadle().subscribe(
      (res: any) => {
        const rows: any[] = res?.data || [];

        this.bfAvailableLadles = rows
          .filter((r: any) => (r.BookingLocation ?? 'NA') === 'NA')
          .map((r: any) => ({
            ladleNo: r.ladleNo,
            castID: r.castID,
            castNo: r.castNo,
            createdDateTime: r.createdDateTime,
            sourceLocation: r.locationName,
            locationID: r.locationID ?? this.BF_LOCATION_IDS[r.locationName],
            movementType: 'Casted',
            isSelected: false,
            isExpanded: false,
            chemicalData: {
              'C%': r.C ?? 'NA',
              'Si': r.SI ?? 'NA',
              'Mn': r.Mn ?? 'NA',
              'S': r.S ?? 'NA',
              'P': r.P ?? 'NA',
              'Ti': r.Ti ?? 'NA',
              'Cr': r.Cr ?? 'NA',
              'S+P': r.SP ?? 'NA'
            }
          }))
          .filter((r: any) => !!r.ladleNo);

        this.rebuildAvailableLadles();
      },
      (error) => {
        console.error('[fetchBFLadles] failed:', error);
      }
    );
  }

  /**
   * ✅ Merges the original GetProductionDetails rows with the BF rows into a
   * single Available Ladles list, de-duplicated by ladleNo and preserving the
   * user's tick marks and expanded rows across the 3-second refresh.
   *
   * To show ONLY BF ladles instead, comment out the prodAvailableLadles line.
   */
   private rebuildAvailableLadles(): void {
    const previous = this.availableLadles || [];
    const merged: any[] = [];
    const seen = new Set<string>();
    const booked = new Set(
      (this.bookedSummary || []).map((b: any) => String(b.ladleNo))
    );

    const push = (item: any) => {
      const key = String(item.ladleNo);
      if (!item.ladleNo || seen.has(key) || booked.has(key)) { return; }
      seen.add(key);

      const existing = previous.find(p => String(p.ladleNo) === key);
      merged.push({
        ...item,
        isSelected: existing ? existing.isSelected : false,
        isExpanded: existing ? existing.isExpanded : false
      });
    };

    this.prodAvailableLadles.forEach(push);
    this.bfAvailableLadles.forEach(push);

    this.availableLadles = merged;
    this.updateSelectAllCheckbox();
  }

  // ==================== END: BF Ladles for the Available table ====================

  // Fetches pending and completed ladle requests.

  getLadleRequest(): void {
    this.service.getLadleRequest().subscribe(
      (data: any) => {
        const now = new Date();
        const last24Hours = new Date(now.getTime() - 24 * 60 * 60 * 1000);
        const allPending = data?.data?.pendingRequest || [];
        const allCompleted = data?.data?.completedRequest || [];
        const userLocationName = this.effectiveLocationName;

        this.pendingRequests = allPending.filter((item: any) => {
          if (item.requestNo) return true;
          if (!item.requestNo && item.locationName === userLocationName) return true;
          return false;
        });

        this.completedRequests = allCompleted.filter((item: any) => {
          if (!item.completedDateTime) return false;
          const completedDate = new Date(item.completedDateTime);
          if (completedDate < last24Hours) return false;
          if (item.requestNo) return true;
          if (!item.requestNo && item.locationName === userLocationName) return true;
          return false;
        });
      },
      (error) => {
        console.error('Error fetching ladle request data:', error);
      }
    );
  }

  // Fetches pending task (reversalLadles), available ladles, and summary data.
  fetchProductionDetails(): void {
    const clientDeviceID = 'device123';
    const userID = sessionStorage.getItem('userID');
    const locationID = this.effectiveLocationId;

    const preserveCheckedState = (oldList: any[], newList: any[]) => {
      return newList.map(item => {
        const existing = oldList.find(old => old.ladleNo === item.ladleNo);
        return {
          ...item,
          isSelected: existing ? existing.isSelected : false,
          isExpanded: existing ? existing.isExpanded : false,
          selectedState: existing ? existing.selectedState : '',
          chemicalData: this.parseChemicalComp(item.chemicalComp || '')
        };
      });
    };

    this.service.getProductionDetails(clientDeviceID, userID, locationID).subscribe((res: any) => {
      if (res?.status && res?.data) {

        // ✅ CHANGED: park these rows and let rebuildAvailableLadles() merge
        //    them with the BF rows, instead of overwriting availableLadles.
        this.prodAvailableLadles = (res.data.availableLadles || []).map((ladle: any) => ({
          ...ladle,
          isSelected: false,
          isExpanded: false,
          chemicalData: this.parseChemicalComp(ladle.chemicalComp || '')
        }));
        this.rebuildAvailableLadles();

        const newReversalLadles = res.data.reversalLadles as any[];
        const incomingKeys = newReversalLadles.map((l: any) => l.ladleNo).join(',');
        const existingKeys = this.reversalLadles.map((l: any) => l.ladleNo).join(',');

        if (incomingKeys !== existingKeys) {
          this.reversalLadles = newReversalLadles.map((ladle: any) => {
            const existing = this.reversalLadles.find(old => old.ladleNo === ladle.ladleNo);
            return {
              ...ladle,
              isExpanded: existing ? existing.isExpanded : false,
              selectedState: existing ? existing.selectedState : '',
              chemicalData: this.parseChemicalComp(ladle.chemicalComp || '')
            };
          });
        } else {

          this.reversalLadles = this.reversalLadles.map((existing: any) => {
            const updated = newReversalLadles.find((l: any) => l.ladleNo === existing.ladleNo);
            if (!updated) return existing;
            return {
              ...existing,
              ...updated,
              selectedState: existing.selectedState,
              isExpanded: existing.isExpanded,
              chemicalData: this.parseChemicalComp(updated.chemicalComp || '')
            };
          });
        }

        const summary = res.data.summary || [];

        const newBookedSummary = summary
          .filter((item: any) => item.type === 'Booked')
          .map((item: any) => ({
            ...item,
            isExpanded: false,
            chemicalData: this.parseChemicalComp(item.chemicalComp || '')
          }));
        this.bookedSummary = preserveCheckedState(this.bookedSummary, newBookedSummary);

        const newPickupSummary = summary
          .filter((item: any) => item.type === 'Pickup')
          .map((item: any) => ({
            ...item,
            isExpanded: false,
            chemicalData: this.parseChemicalComp(item.chemicalComp || '')
          }));
        this.pickupSummary = preserveCheckedState(this.pickupSummary, newPickupSummary);
      }
    });
  }

  trackByLadleNo(index: number, ladle: any): string {
    return ladle.ladleNo;
  }

  // Fetches requested ladles for the current user and location.
  getRequestedLadles(): void {
    const userID = sessionStorage.getItem('userID');
    const locationID = this.effectiveLocationId;

    const preserveCheckedState = (oldList: any[], newList: any[]) => {
      return newList.map(item => {
        const existing = oldList.find(old => old.requestNo === item.requestNo);
        return {
          ...item,
          isExpanded: existing ? existing.isExpanded : false,
          chemicalData: {
            'C%': item.C || 'NA',
            'Si': item.Si || 'NA',
            'Mn': item.Mn || 'NA',
            'S': item.S || 'NA',
            'P': item.P || 'NA',
            'Ti': item.Ti || 'NA',
            'Cr': item.Cr || 'NA',
            'S+P': item.SP || 'NA'
          }
        };
      });
    };

    this.service.getRequestedLadles(userID, locationID).subscribe((res: any) => {
      if (res?.status && res?.data) {

        const newRequestedLadles = res.data.map((ladle: any) => ({
          ...ladle,
          isExpanded: false,
          chemicalData: {
            'C%': ladle.C || 'NA',
            'Si': ladle.Si || 'NA',
            'Mn': ladle.Mn || 'NA',
            'S': ladle.S || 'NA',
            'P': ladle.P || 'NA',
            'Ti': ladle.Ti || 'NA',
            'Cr': ladle.Cr || 'NA',
            'S+P': ladle.SP || 'NA'
          }
        }));
        this.requestedLadles = preserveCheckedState(this.requestedLadles, newRequestedLadles);
      }
    });
  }

  // Checkbox Helpers. 

  toggleSelectAll(isChecked: boolean) {
    this.isChecked = isChecked;
    this.availableLadles.forEach(ladle => (ladle.isSelected = this.isChecked));
  }

  // Recalculates the header checkbox state (checked / indeterminate / unchecked) 
  updateSelectAllCheckbox() {
    if (this.availableLadles.length === 0) {
      this.isChecked = false;
      this.indeterminate = false;
    } else {
      const allSelected = this.availableLadles.every(ladle => ladle.isSelected);
      const someSelected = this.availableLadles.some(ladle => ladle.isSelected);
      this.isChecked = allSelected;
      this.indeterminate = someSelected && !allSelected;
    }
  }

  // Called when any individual row checkbox changes.
  onLadleCheckboxChange() {
    this.updateSelectAllCheckbox();
  }

  // Chemical Composition Parser. 

  parseChemicalComp(chemicalComp: string): any {
    const chemData: any = {
      'C%': 'NA', 'Si': 'NA', 'Mn': 'NA', 'S': 'NA',
      'P': 'NA', 'Ti': 'NA', 'Cr': 'NA', 'S+P': 'NA'
    };
    if (chemicalComp) {
      const pairs = chemicalComp.split(',');
      pairs.forEach(pair => {
        const [key, value] = pair.split(':');
        if (!key || value === undefined) { return; }
        const trimmedKey = key.trim();
        const trimmedValue = value.trim();
        if (trimmedKey === 'C') {
          chemData['C%'] = trimmedValue;
        } else {
          chemData[trimmedKey] = trimmedValue;
        }
      });
    }
    return chemData;
  }

  // UI Helpers.
  toggleExpand(ladle: any) {
    ladle.isExpanded = !ladle.isExpanded;
  }

  // Chemical Composition Popup. 

  openPopup(row: any): void {
    this.chemicalData = {
      'C%': row.C,
      'Si': row.Si,
      'Mn': row.Mn,
      'S': row.S,
      'P': row.P,
      'Ti': row.Ti,
      'Cr': row.Cr,
      'S+P': row.SP
    };
    this.showPopup = true;
  }

  closePopup(): void {
    this.showPopup = false;
  }

  // Submits selected pending ladles with their chosen movement states.
  proceedLadleMovement() {
    const selectedLadles = this.reversalLadles.filter(ladle => ladle.selectedState !== '');
    if (selectedLadles.length === 0) {
      Swal.fire('No ladles selected', 'Please select a state for at least one ladle to proceed.', 'warning');
      return;
    }

    Swal.fire({
      title: 'Are you sure?',
      html: `You are about to proceed with the following ladle(s): <strong>${selectedLadles.map(l => `${l.ladleNo} → ${l.selectedState}`).join('<br>')}</strong>`,
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Yes, proceed',
      cancelButtonText: 'Cancel'
    }).then((result) => {
      if (result.isConfirmed) {
        const payload = {
          clientDeviceID: 'device123',
          userID: sessionStorage.getItem('userID'),
          source: this.effectiveLocationName,
          transactionDateTime: this.getFormattedLocalDateTime(),
          reversalDetails: selectedLadles.map(ladle => ({
            ID: uuidv4(),
            ladleNo: ladle.ladleNo,
            movementType: ladle.selectedState,
            destination: ladle.selectedState === 'Full Consumption' ? 'lrs' : '0',
            castID: ladle.castID
          }))
        };

        this.service.createRevarsalMovement(payload).subscribe(
          (res: any) => {
            if (res.status) {
              Swal.fire({
                icon: 'success',
                title: 'Success',
                text: res.message || 'Submitted successfully.',
                timer: 3000,
                showConfirmButton: false
              });
            } else {
              Swal.fire({ icon: 'error', title: 'Error', text: res.message || 'Something went wrong. Please try again.' });
            }
            this.fetchProductionDetails();
          },
          (err) => {
            Swal.fire({ icon: 'error', title: 'Server Error', text: 'There was an error connecting to the server.' });
            console.error('Error submitting data:', err);
          }
        );
      }
    });
  }

  // Sends a booking request for all selected available ladles.
    // Sends a booking request for all selected available ladles.
  bookedAvailableLadles() {
    const selectedLadles = this.availableLadles.filter(ladle => ladle.isSelected);

    if (selectedLadles.length === 0) {
      Swal.fire('No ladles selected', 'Please select at least one ladle to proceed.', 'warning');
      return;
    }

    const unresolved = selectedLadles.filter(l => !l.locationID);
    if (unresolved.length > 0) {
      Swal.fire({
        icon: 'error',
        title: 'Missing Location',
        text: `Could not resolve the source location for: ${unresolved.map(l => l.ladleNo).join(', ')}`
      });
      return;
    }

    const ladleCount = selectedLadles.length;
    const ladleWord = ladleCount === 1 ? 'ladle' : 'ladles';

    Swal.fire({
      title: 'Confirm Ladle Request',
      html: `
      <div style="text-align:left;">
        <p style="font-size:15px; margin-bottom:8px;">
          You are requesting a total of <strong>${ladleCount} ${ladleWord}</strong>.
        </p>
        <p style="margin-bottom:4px;"><strong>Ladle No(s):</strong></p>
        <p style="color:#005ca8; font-weight:600;">
          ${selectedLadles.map(l => `${l.ladleNo} (${l.sourceLocation})`).join(', ')}
        </p>
      </div>
    `,
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Yes, Request',
      cancelButtonText: 'Cancel'
    }).then((result) => {
      if (result.isConfirmed) {

        /* ✅ CHANGED: was requestReversalLadles with movementType 'Casted'.
           These BF ladles have NO CastTransaction row, so that SP's Casted
           branch updated zero rows and failed. ESL_WEB_SP_RequestEmptyLadles
           INSERTs the booking row when one doesn't exist - the same endpoint
           esl-blast-furnace already uses. No movementType is sent. */
        const payload = {
          clientDeviceID: 'device123',
          userID: sessionStorage.getItem('userID'),
          requestLocationID: this.effectiveLocationId,   // number, not string
          transactionDateTime: this.getFormattedLocalDateTime(),
          ladles: selectedLadles.map(ladle => ({
            sourceLocationID: ladle.locationID,
            ladleNo: ladle.ladleNo
          }))
        };

        this.service.requestEmptyLadles(payload).subscribe(
          (res: any) => {
            if (res.status) {
              const keys = selectedLadles.map(l => String(l.ladleNo));
              this.availableLadles = this.availableLadles.filter(
                l => keys.indexOf(String(l.ladleNo)) === -1
              );
              this.updateSelectAllCheckbox();

              Swal.fire({
                icon: 'success',
                title: 'Request Submitted',
                text: res.message || `${ladleCount} ${ladleWord} requested successfully.`,
                timer: 3000,
                showConfirmButton: false
              });
            } else {
              Swal.fire({ icon: 'error', title: 'Error', text: res.message || 'Something went wrong. Please try again.' });
            }
            this.fetchProductionDetails();
            this.fetchBFLadles();
          },
          (err) => {
            Swal.fire({ icon: 'error', title: 'Server Error', text: 'There was an error connecting to the server.' });
            console.error('Error submitting data:', err);
          }
        );
      }
    });
  }

  // Routes delete (Blast Furnace vs Reversal).
  deleteBookedLadle(ladle: any) {
    if (ladle.revType === 'Casted') {
      this.deleteBookedBFLadle(ladle);
    } else {
      this.deleteBookedReversalLadle(ladle);
    }
  }

  // Deletes a Blast Furnace (casted) booked ladle using castNo.
  deleteBookedBFLadle(ladle: any) {
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
          castNo: ladle.castNo
        };

        this.service.deleteBookedBFLadle(payload).subscribe({
          next: (res: any) => {
            if (res.status) {
              Swal.fire({
                icon: 'success',
                title: 'Success',
                text: res.message || 'Ladle request deleted successfully.',
                timer: 3000,
                showConfirmButton: false
              });
              this.fetchProductionDetails();
              this.fetchBFLadles();
            } else {
              Swal.fire({ icon: 'error', title: 'Error', text: res.message || 'Something went wrong. Please try again.' });
            }
          },
          error: () => {
            Swal.fire('Error', 'Server error while deleting ladle request.', 'error');
          }
        });
      }
    });
  }

  // Deletes a reversal.
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
          movementType: ladle.revType
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
              this.fetchProductionDetails();
              this.fetchBFLadles();
            } else {
              Swal.fire({ icon: 'error', title: 'Error', text: res.message || 'Something went wrong. Please try again.' });
            }
          },
          error: () => {
            Swal.fire('Error', 'Server error while deleting ladle request.', 'error');
          }
        });
      }
    });
  }

  // Submits a new ladle request from the Ladle Request Form.
  submitLadleRequest() {
    const ladleCountInput = (document.getElementById('ladle-count') as HTMLInputElement)?.value;
    const nosOfLadle = parseInt(ladleCountInput, 10);
    if (isNaN(nosOfLadle) || nosOfLadle <= 0) {
      Swal.fire('Invalid Input', 'Please enter a valid number of ladles.', 'warning');
      return;
    }

    const getValue = (id: string) => (document.getElementById(id) as HTMLInputElement)?.value || 'NA';
    const payload = {
      requestID: uuidv4(),
      clientDeviceID: 'device123',
      userID: sessionStorage.getItem('userID'),
      locationID: this.effectiveLocationId,
      nosOfLadle,
      C: getValue('c-percent'),
      Si: getValue('si'),
      Mn: getValue('mn'),
      S: getValue('s'),
      P: getValue('p'),
      Ti: getValue('ti'),
      Cr: getValue('cr'),
      SP: getValue('s-plus-p'),
      transactionDateTime: this.getFormattedLocalDateTime()
    };

    const htmlMessage = `
      <strong>Confirm Ladle Request with the following values:</strong><br/><br/>
      Nos. of Ladles: <b>${payload.nosOfLadle}</b><br/>
      C%: <b>${payload.C}</b><br/>
      Si: <b>${payload.Si}</b><br/>
      Mn: <b>${payload.Mn}</b><br/>
      S: <b>${payload.S}</b><br/>
      P: <b>${payload.P}</b><br/>
      Ti: <b>${payload.Ti}</b><br/>
      Cr: <b>${payload.Cr}</b><br/>
      S+P: <b>${payload.SP}</b>
    `;

    Swal.fire({
      title: 'Confirm Request',
      html: htmlMessage,
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Yes, Create',
      cancelButtonText: 'No'
    }).then((result) => {
      if (result.isConfirmed) {
        this.service.createRequestladles(payload).subscribe({
          next: (res: any) => {
            if (res.status) {
              Swal.fire({
                icon: 'success',
                title: 'Success',
                text: res.message || 'Submitted successfully.',
                timer: 3000,
                showConfirmButton: false
              });
              this.getRequestedLadles();
              if (!this.isCompositionLocked) {
                this.clearForm();
              } else {
                const ladleCountEl = document.getElementById('ladle-count') as HTMLInputElement;
                if (ladleCountEl) ladleCountEl.value = '';
              }
            } else {
              Swal.fire({ icon: 'error', title: 'Error', text: res.message || 'Something went wrong. Please try again.' });
            }
          },
          error: () => {
            Swal.fire('Error', 'Server error while submitting ladle request.', 'error');
          }
        });
      }
    });
  }

  // Clears the Ladle Request Form fields
  clearForm() {
    const ids = [
      'ladle-count',
      ...(this.isCompositionLocked ? [] : ['c-percent', 'si', 'mn', 's', 'p', 'ti', 'cr', 's-plus-p'])
    ];
    ids.forEach(id => {
      const el = document.getElementById(id) as HTMLInputElement;
      if (el) el.value = '';
    });
  }

  // Toggles chemical composition fields lock. 
  toggleCompositionLock() {
    const compositionIds = ['c-percent', 'si', 'mn', 's', 'p', 'ti', 'cr', 's-plus-p'];
    const allEmpty = compositionIds.every(id => {
      const input = document.getElementById(id) as HTMLInputElement;
      return !input?.value.trim();
    });
    if (allEmpty && !this.isCompositionLocked) {
      Swal.fire({
        toast: true,
        position: 'top-end',
        icon: 'warning',
        title: 'Enter Chemical Composition',
        showConfirmButton: false,
        timer: 3000
      });
      return;
    }
    this.isCompositionLocked = !this.isCompositionLocked;
  }

  // getTransactionLadlesProduction
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

  // Input Validation.

  validateLadleCount(event: any): void {
    const input = event.target;
    let value = input.value;
    value = value.replace(/\D/g, '').slice(0, 2);
    input.value = value;
  }

  // Formats composition.  
  formatCompositionInput(event: any): void {
    let input = event.target.value;
    if (input.includes('.')) {
      const parts = input.split('.');
      const before = parts[0].slice(0, 2);
      const after = parts[1].slice(0, 3);
      event.target.value = before + '.' + after;
    } else {
      input = input.replace(/\D/g, '');
      if (input.length > 2) {
        input = input.slice(0, 6);
        input = input.slice(0, 2) + '.' + input.slice(2);
      }
      event.target.value = input;
    }
  }

  // Edit Popup.
  openEditPopup(ladle: any) {
    this.selectedLadle = ladle;
    this.editLadleCount = ladle.nosOfLadle?.toString() || '';
    this.editChemical = {
      C: ladle.C ?? '',
      Si: ladle.Si ?? '',
      Mn: ladle.Mn ?? '',
      S: ladle.S ?? '',
      P: ladle.P ?? '',
      Ti: ladle.Ti ?? '',
      Cr: ladle.Cr ?? '',
      SP: ladle.SP ?? ''
    };
    this.showEditPopup = true;
  }

  closeEditPopup() {
    this.showEditPopup = false;
    this.selectedLadle = null;
    this.editLadleCount = '';
  }

  // Validates and submits the updated ladle request.  
  confirmUpdateLadle() {
    const nosOfLadle = parseInt(this.editLadleCount, 10);
    if (isNaN(nosOfLadle) || nosOfLadle <= 0) {
      Swal.fire('Invalid Input', 'Please enter a valid number of ladles.', 'warning');
      return;
    }

    Swal.fire({
      title: 'Are you sure?',
      html: `You are about to update the ladle request <strong>${this.selectedLadle.requestNo}</strong>`,
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Yes, proceed'
    }).then((result) => {
      if (result.isConfirmed) {
        const payload = {
          clientDeviceID: 'device123',
          userID: sessionStorage.getItem('userID'),
          nosOfLadle: nosOfLadle,
          requestNo: this.selectedLadle.requestNo,
          C: this.editChemical.C || null,
          Si: this.editChemical.Si || null,
          Mn: this.editChemical.Mn || null,
          S: this.editChemical.S || null,
          P: this.editChemical.P || null,
          Ti: this.editChemical.Ti || null,
          Cr: this.editChemical.Cr || null,
          SP: this.editChemical.SP || null,
          transactionDateTime: this.getFormattedLocalDateTime(),
          action: 'E'
        };

        this.service.updateORdeleteRequestladles(payload).subscribe({
          next: (res: any) => {
            if (res.status) {
              Swal.fire({
                icon: 'success',
                title: 'Updated!',
                text: res.message || 'Ladle request updated successfully.',
                timer: 2000,
                showConfirmButton: false
              });
              this.getLadleRequest();
              this.getRequestedLadles();
              this.closeEditPopup();
            } else {
              Swal.fire('Error', res.message, 'error');
            }
          },
          error: () => {
            Swal.fire('Error', 'Server error while updating ladle request.', 'error');
          }
        });
      }
    });
  }

  // Confirms and submits a delete action for a ladle request. 
  confirmDeleteLadle(ladle: any) {
    Swal.fire({
      title: 'Are you sure?',
      html: `You are about to delete the ladle request <strong>${ladle.requestNo}</strong>.`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonText: 'Yes, proceed',
      cancelButtonText: 'Cancel'
    }).then((result) => {
      if (result.isConfirmed) {
        const payload = {
          clientDeviceID: 'device123',
          userID: sessionStorage.getItem('userID'),
          nosOfLadle: ladle.nosOfLadle,
          requestNo: ladle.requestNo,
          transactionDateTime: this.getFormattedLocalDateTime(),
          action: 'D'
        };

        this.service.updateORdeleteRequestladles(payload).subscribe({
          next: (res: any) => {
            if (res.status) {
              Swal.fire({
                icon: 'success',
                title: 'Success',
                text: res.message || 'Ladle request deleted successfully.',
                timer: 3000,
                showConfirmButton: false
              });
              this.getRequestedLadles();
            } else {
              Swal.fire({ icon: 'error', title: 'Error', text: res.message || 'Something went wrong. Please try again.' });
            }
          },
          error: () => {
            Swal.fire('Error', 'Server error while deleting ladle request.', 'error');
          }
        });
      }
    });
  }

  // Marks a ladle request as complete.
  completeLadle(id: string) {
    Swal.fire({
      title: 'Are you sure?',
      text: 'Do you want to complete this ladle request?',
      icon: 'warning',
      showCancelButton: true,
      confirmButtonText: 'Yes, Complete it!'
    }).then((result) => {
      if (result.isConfirmed) {
        Swal.fire({
          title: 'Processing...',
          allowOutsideClick: false,
          didOpen: () => Swal.showLoading()
        });

        this.service.completeLadleRequest(id).subscribe({
          next: (res: any) => {
            if (res.status) {
              Swal.fire({
                icon: 'success',
                title: 'Completed!',
                timer: 1500,
                showConfirmButton: false
              });
              this.getRequestedLadles();
              this.getLadleRequest();
            } else {
              Swal.fire('Error', res.message, 'error');
            }
          },
          error: () => {
            Swal.fire('Error', 'Server error', 'error');
          }
        });
      }
    });
  }

  // ==================== Request Report ====================

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

  // ==================== End Request Report ====================

  // Get the current local date-time. 
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

  // Logout
  logout() {
    Swal.fire({
      title: 'Are you sure?',
      text: 'Do you want to logout?',
      icon: 'warning',
      showCancelButton: true,
      confirmButtonText: 'Yes, logout',
      cancelButtonText: 'No, stay logged in'
    }).then((result) => {
      if (result.isConfirmed) {
        if (this.refreshInterval) {
          clearInterval(this.refreshInterval);
        }
        this.service.logout();
        sessionStorage.clear();
        this.router.navigate(['/login']);
      }
    });
  }
}