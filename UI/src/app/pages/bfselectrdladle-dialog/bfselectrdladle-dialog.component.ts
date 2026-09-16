import { ChangeDetectorRef, Component, Inject, OnDestroy, OnInit, NgZone } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { v4 as uuidv4 } from 'uuid';
import Swal from 'sweetalert2';
import { MainserviceService } from '../service/mainservice.service';
import { interval, Subscription } from 'rxjs';
import { SessionTimeoutService } from '../service/session-timeout.service';

@Component({
  selector: 'app-bfselectrdladle-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './bfselectrdladle-dialog.component.html',
  styleUrl: './bfselectrdladle-dialog.component.css'
})
export class BfselectrdladleDialogComponent implements OnInit, OnDestroy {
  constructor(
    @Inject(MAT_DIALOG_DATA) public data: any[],
    private dialogRef: MatDialogRef<BfselectrdladleDialogComponent>,
    private service: MainserviceService,
    private sessionTimeoutService: SessionTimeoutService,
    private cdr: ChangeDetectorRef,
    private ngZone: NgZone

  ) { }

  locoOptions: any;
  selectedLocoID: number | null = null;
  userID: string | null = null;

  // ✅ FIX: blocks a second submit / second close while one is in flight
  isSubmitting: boolean = false;

  private pollingSubscription: Subscription | null = null;
  destinations: string[] = ['SMS', 'DIP', 'PCM'];
  private sessionTimeoutSubscription: Subscription | null = null; // Subscription for session timeout
  isAllLocked: boolean = true;

  ngOnInit(): void {
    this.initializeLadleData();
    this.startPolling();

    // Subscribe to session timeout events
    this.sessionTimeoutSubscription = this.sessionTimeoutService.sessionTimeoutSubject.subscribe(() => {
      this.closeDialog(); // Close the dialog on session timeout
    });
  }

  ngOnDestroy(): void {
    this.stopPolling();

    if (this.sessionTimeoutSubscription) {
      this.sessionTimeoutSubscription.unsubscribe();
    }
  }

  toggleLock(ladle: any): void {
    ladle.isLocked = !ladle.isLocked;
  }

  // Initialize ladle data (set default destination if BookingLocation is not 'NA')
  initializeLadleData(): void {
    this.data.forEach(ladle => {

      if (ladle.BookingLocation !== 'NA') {
        ladle.destination = ladle.BookingLocation;
      } else {
        ladle.destination = 'SMS';  // <-- add this line
      }
    });
  }

  // Fetch all free locos
  getAllFreeLoco(): void {
    this.service.getAllOccupiedLoco().subscribe((res: any) => {
      const newLocoOptions = res.data.filter((loco: any) => loco.IsOccupied === 0);
      if (JSON.stringify(this.locoOptions) !== JSON.stringify(newLocoOptions)) {
        this.locoOptions = newLocoOptions;
        if (this.selectedLocoID && !this.locoOptions.some((loco: { LocoID: any | null; }) => loco.LocoID === this.selectedLocoID)) {
          this.selectedLocoID = null;
        }
        this.cdr.markForCheck();
      }
    });
  }

  //Selected Loco Alert Message
  onLocoChange(selectedId: any): void {
    const selectedLoco = this.locoOptions.find(
      (loco: any) => loco.LocoID === Number(selectedId)
    );

    if (selectedLoco && selectedLoco.LadleCount > 0) {
      Swal.fire({
        icon: 'info',
        title: 'Loco Info',
        html: `The selected <b>${selectedLoco.LocoName}</b> has <b>${selectedLoco.LadleCount}</b> ladle(s).<br>Do you want to continue with this Loco?`,
        showCancelButton: true,
        confirmButtonText: 'Yes',
        cancelButtonText: 'Cancel',
      }).then(result => {
        if (!result.isConfirmed) {
          this.selectedLocoID = null;
        }
      });
    }
  }

  // Start polling to check for ladle data updates
  startPolling(): void {
    this.getAllFreeLoco();
    this.pollingSubscription = interval(5000).subscribe(() => {
      this.updateLadleData();
      this.getAllFreeLoco();
    });
  }
  toggleAllLock(): void {
    this.isAllLocked = !this.isAllLocked;
  }

  // Stop polling to prevent memory leaks
  stopPolling(): void {
    if (this.pollingSubscription) {
      this.pollingSubscription.unsubscribe();
      this.pollingSubscription = null;
    }
  }

  // Fetch updated ladle data and preserve user selections
  updateLadleData(): void {
    this.service.getAllBFLadle().subscribe((res: any) => {
      const updatedLadles = res.data || [];
      this.data.forEach(existingLadle => {
        const newLadle = updatedLadles.find((item: { ladleNo: any; }) => item.ladleNo === existingLadle.ladleNo);
        if (newLadle) {
          existingLadle.locationName = newLadle.locationName;
          existingLadle.BookingLocation = newLadle.BookingLocation;
          if (newLadle.BookingLocation !== 'NA') {
            existingLadle.destination = newLadle.BookingLocation;
          }
        }
      });
    });
  }

  // Close the dialog
  closeDialog(): void {
    this.dialogRef.close();
  }

  // Submit ladle movement
  submitLadleMovement(): void {
    // ✅ FIX: ignore repeat clicks while a submit is already running
    if (this.isSubmitting) { return; }

    const hasEmptyDestination = this.data.some(ladle => !ladle.destination);

    if (!this.selectedLocoID) {
      Swal.fire({
        icon: 'warning',
        title: 'Missing Loco',
        text: 'Please select a Loco before creating a movement.'
      });
      return;
    }

    if (hasEmptyDestination) {
      Swal.fire({
        icon: 'warning',
        title: 'Missing Destination',
        text: 'Please select a destination location for all ladles.'
      });
      return;
    }

    const summary = this.data
      .map(ladle => `${ladle.ladleNo} → ${ladle.destination}`)
      .join('<br>');

    Swal.fire({
      title: 'Confirm Movement',
      html: `Do you want to create movement for the following ladles?<br><br>${summary}`,
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Yes, Create',
      cancelButtonText: 'Cancel'
    }).then(result => {
      if (result.isConfirmed) {
        // ✅ FIX: lock before the request goes out
        this.isSubmitting = true;
        this.cdr.markForCheck();

        const webMovement = {
          ID: uuidv4(),
          userID: (this.userID = sessionStorage.getItem('userID')),
          transactionDateTime: this.getFormattedLocalDateTime(),
          locoID: this.selectedLocoID,
          movementDetails: this.data.map(ladle => ({
            castID: ladle.castID,
            ladleNo: ladle.ladleNo,
            movementTypeID: 5,
            castNo: ladle.castNo,
            sourceLocationName: ladle.locationName,
            destinationName: ladle.destination,
            transactionDateTime: this.getFormattedLocalDateTime(),
          }))
        };

        this.service.addWEBMovement(webMovement).subscribe(
          (res: any) => {
            console.log('CreateWEBMovement response:', res);

            if (res && res.status) {
              this.stopPolling();

              this.ngZone.run(() => {
                this.dialogRef.close(true);
              });
            } else {
              // ✅ FIX: unlock so the user can retry
              this.isSubmitting = false;
              this.cdr.markForCheck();

              Swal.fire({
                icon: 'error',
                title: 'Error',
                text: (res && res.message) || 'Something went wrong. Please try again.'
              });
            }
          },
          err => {
            // ✅ FIX: unlock so the user can retry
            this.isSubmitting = false;
            this.cdr.markForCheck();

            console.error('CreateWEBMovement HTTP error:', err);
            Swal.fire({
              icon: 'error',
              title: 'Server Error',
              text: 'There was an error connecting to the server.'
            });
          }
        );
      }
    });
  }

  // Format date and time
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