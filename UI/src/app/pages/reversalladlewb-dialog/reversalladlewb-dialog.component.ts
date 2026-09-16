import { Component, Inject, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { v4 as uuidv4 } from 'uuid';
import { interval, Subscription } from 'rxjs';
import Swal from 'sweetalert2';
import { MainserviceService } from '../service/mainservice.service';
import { SessionTimeoutService } from '../service/session-timeout.service';

@Component({
  selector: 'app-reversalladlewb-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './reversalladlewb-dialog.component.html',
  styleUrl: './reversalladlewb-dialog.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReversalladlewbDialogComponent {
  constructor(
    @Inject(MAT_DIALOG_DATA) public data: any[],
    private dialogRef: MatDialogRef<ReversalladlewbDialogComponent>,
    private service: MainserviceService,
    private sessionTimeoutService: SessionTimeoutService,
    private cdr: ChangeDetectorRef
  ) { }

  locoOptions: any[] = [];
  trailLadleLocation: any;
  selectedLocoID: number | null = null;
  userID: string | null = null;

  // ✅ FIX: blocks a second submit / second close while one is in flight
  isSubmitting: boolean = false;

  private pollingSubscription: Subscription | null = null;
  // ✅ All possible destinations
  allDestinations: string[] = ['SMS', 'DIP', 'PCM', 'LRS', 'BF1', 'BF2', 'BF3'];
  private sessionTimeoutSubscription: Subscription | null = null;

  ngOnInit(): void {
    this.initializeLadleData();
    this.startPolling();
    this.sessionTimeoutSubscription = this.sessionTimeoutService.sessionTimeoutSubject.subscribe(() => {
      this.closeDialog();
    });
  }

  ngOnDestroy(): void {
    this.stopPolling();
    if (this.sessionTimeoutSubscription) {
      this.sessionTimeoutSubscription.unsubscribe();
    }
  }

  closeDialog() {
    this.dialogRef.close();
  }

  // ✅ FIXED: Show ALL locations EXCEPT the source location
  private getBaseOptions(sourceName: string, movementType: string): string[] {
    return this.allDestinations.filter(dest => dest !== sourceName);
  }

  initializeLadleData(): void {
    this.data.forEach(ladle => {
      if (ladle.destinationName !== 'NA' && ladle.movementType !== 'Full Consumption') {
        ladle.destination = ladle.destinationName;
        ladle.baseOptions = [ladle.destinationName];
        ladle.availableDestinations = [ladle.destinationName];
      } else {
        ladle.destination = '';
        ladle.baseOptions = this.getBaseOptions(ladle.sourceName, ladle.movementType);
        ladle.availableDestinations = ladle.baseOptions;
      }
    });
  }

  getAllFreeLoco(): void {
    this.service.getAllOccupiedLoco().subscribe((res: any) => {
      const newLocoOptions = res.data.filter((loco: any) => loco.IsOccupied === 0);
      if (JSON.stringify(this.locoOptions) !== JSON.stringify(newLocoOptions)) {
        this.locoOptions = newLocoOptions;
        if (this.selectedLocoID && !this.locoOptions.some(loco => loco.LocoID === this.selectedLocoID)) {
          this.selectedLocoID = null;
        }
        this.cdr.markForCheck();
      }
    });
  }

  getTrailLadleLocation(): void {
    this.service.getTrailLadleLocation().subscribe((res: any) => {
      const trailMap = new Map<string, string[]>();
      const trailDetailMap = new Map<string, any[]>();

      res.data.forEach((item: any) => {

        console.log('LADLE RECORD =>', item);

        trailMap.set(
          item.LadleNo,
          item.MovementTrailLocation
            ? item.MovementTrailLocation.split(',').map((loc: string) => loc.trim())
            : []
        );

        if (item.MovementTrailType) {

          console.log(
            'MovementTrailType FOUND for',
            item.LadleNo,
            '=',
            item.MovementTrailType
          );

          const details = item.MovementTrailType
            .split(',')
            .map((t: string) => ({
              display: t.trim()
            }));

          console.log('trailDetails created =>', details);

          trailDetailMap.set(item.LadleNo, details);

        } else {

          console.log(
            'MovementTrailType MISSING for',
            item.LadleNo
          );
        }
      });

      this.data.forEach((ladle: any) => {

        const trailLocations = trailMap.get(ladle.ladleNo);
        ladle.trailLocations = trailLocations || [];

        const trailDetails = trailDetailMap.get(ladle.ladleNo);

        ladle.trailDetails = trailDetails || ladle.trailLocations.map((loc: string) => ({
          display: `${loc} - NA`
        }));

        // ✅ FIXED: Always show all destinations except source
        if (
          ladle.destinationName !== 'NA' &&
          ladle.movementType !== 'Full Consumption'
        ) {
          ladle.availableDestinations = [ladle.destinationName];
        } else {
          // ✅ FIXED: Show ALL locations except source (no trail filtering)
          ladle.availableDestinations = this.getBaseOptions(ladle.sourceName, ladle.movementType);
        }
      });

      this.cdr.markForCheck();
    });
  }

  onLocoChange(selectedId: any): void {
    const selectedLoco = this.locoOptions.find(loco => loco.LocoID === Number(selectedId));
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
          this.cdr.markForCheck();
        }
      });
    }
  }

  startPolling(): void {
    this.getAllFreeLoco();
    this.getTrailLadleLocation();
    this.pollingSubscription = interval(5000).subscribe(() => {
      this.getAllFreeLoco();
      this.getTrailLadleLocation();
      this.updateLadleData();
    });
  }

  stopPolling(): void {
    if (this.pollingSubscription) {
      this.pollingSubscription.unsubscribe();
      this.pollingSubscription = null;
    }
  }

  updateLadleData(): void {
    this.service.getReversalLadlesWB().subscribe((res: any) => {
      const updatedLadles = res.data || [];
      updatedLadles.forEach((newLadle: any) => {
        const existingLadle = this.data.find(ladle => ladle.ladleNo === newLadle.ladleNo);
        if (existingLadle) {
          existingLadle.sourceName = newLadle.sourceName;
          existingLadle.destinationName = newLadle.destinationName;

          if (newLadle.destinationName !== 'NA' && existingLadle.movementType !== 'Full Consumption') {
            existingLadle.destination = newLadle.destinationName;
            existingLadle.baseOptions = [newLadle.destinationName];
            existingLadle.availableDestinations = [newLadle.destinationName];
          } else {
            // ✅ FIXED: Use simplified getBaseOptions
            existingLadle.baseOptions = this.getBaseOptions(
              newLadle.sourceName,
              existingLadle.movementType
            );
            // ✅ FIXED: No trail filtering — show all except source
            existingLadle.availableDestinations = existingLadle.baseOptions;
          }
        }
      });
      this.cdr.markForCheck();
    });
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

  // ladle history
  toggleHistory(ladle: any): void {
    this.data.forEach(l => {
      if (l !== ladle) l.showHistory = false;
    });
    ladle.showHistory = !ladle.showHistory;
    this.cdr.markForCheck();
  }

  submitLadleMovement() {
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
      .map(ladle => `${ladle.ladleNo} → ${ladle.sourceName} → ${ladle.destination}`)
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

        const ladleMovement = {
          ID: uuidv4(),
          userID: (this.userID = sessionStorage.getItem('userID')),
          transactionDateTime: this.getFormattedLocalDateTime(),
          locoID: this.selectedLocoID,
          revLadles: this.data.map(ladle => ({
            castID: ladle.castID === 'NA' ? null : ladle.castID,
            ladleNo: ladle.ladleNo,
            movementType: ladle.movementType,
            sourceLocationName: ladle.sourceName,
            destinationName: ladle.destination,
            transactionDateTime: this.getFormattedLocalDateTime(),
          }))
        };

        this.service.addLadleMovement(ladleMovement).subscribe(
          (res: any) => {
            if (res.status) {
              // ✅ FIX: stop polling before closing
              this.stopPolling();

              Swal.fire({
                icon: 'success',
                title: 'Success',
                text: res.message || 'Submitted successfully.',
                timer: 3000,
                showConfirmButton: false
              });
              this.dialogRef.close(true);
            } else {
              // ✅ FIX: unlock so the user can retry
              this.isSubmitting = false;
              this.cdr.markForCheck();

              Swal.fire({
                icon: 'error',
                title: 'Error',
                text: res.message || 'Something went wrong. Please try again.',
              });
            }
          },
          (err) => {
            // ✅ FIX: unlock so the user can retry
            this.isSubmitting = false;
            this.cdr.markForCheck();

            Swal.fire({
              icon: 'error',
              title: 'Server Error',
              text: 'There was an error connecting to the server.',
            });
            console.error('Error submitting data:', err);
          }
        );
      }
    });
  }
}