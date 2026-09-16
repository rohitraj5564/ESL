import { CommonModule } from '@angular/common';
import { Component, ElementRef, QueryList, ViewChildren } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { CustomCheckboxComponent } from '../custom-checkbox/custom-checkbox.component';

import { MainserviceService } from '../service/mainservice.service';
import { SessionTimeoutService } from '../service/session-timeout.service';
import Swal from 'sweetalert2';
import { Router } from '@angular/router';
import { v4 as uuidv4 } from 'uuid';
import { FooterComponent } from '../footer/footer.component';

@Component({
  selector: 'app-esl-maintenance',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    CustomCheckboxComponent,
    FooterComponent
  ],
  templateUrl: './esl-maintenance.component.html',
  styleUrl: './esl-maintenance.component.css'
})
export class EslMaintenanceComponent {
  @ViewChildren('scrollableTable') scrollableTables!: QueryList<ElementRef>;

  private refreshInterval: any;

  reversalLadles: any[] = [];
  transaction: any[] = [];
  summary: any[] = [];

  isChecked = false;
  indeterminate = false;

  userName: string | null = null;
  userLocationName: string | null = null;
  userLocationId: number | null = null;

  constructor(
    private service: MainserviceService,
    private sessionTimeoutService: SessionTimeoutService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.getUserDetails();
    this.fetchProductionDetails();
    this.fetchTransactionLadles();

    this.refreshInterval = setInterval(() => {
      this.getUserDetails();
      this.fetchProductionDetails();
      this.fetchTransactionLadles();
    }, 3000);
  }

  ngOnDestroy(): void {
    if (this.refreshInterval) {
      clearInterval(this.refreshInterval);
    }
  }

  ngAfterViewInit(): void {
    const tableElements = this.scrollableTables.map(
      ref => ref.nativeElement
    );

    this.sessionTimeoutService.setupActivityTracking(
      tableElements
    );
  }

  getUserDetails(): void {
    this.userName = sessionStorage.getItem('userFirstName');

    const loggedInLocationId = Number(
      sessionStorage.getItem('userLocationId') || 0
    );

    const loggedInLocationName =
      sessionStorage.getItem('userLocationName') || '';

    const selectedLocationId = Number(
      sessionStorage.getItem('selectedLocationId') || 0
    );

    const selectedLocationName =
      sessionStorage.getItem('selectedLocationName') || '';

    this.userLocationId =
      loggedInLocationId === 3 && selectedLocationId > 0
        ? selectedLocationId
        : loggedInLocationId;

    this.userLocationName =
      loggedInLocationId === 3 && selectedLocationName
        ? selectedLocationName
        : loggedInLocationName;
  }

  get effectiveLocationId(): number {
    const loggedInLocationId = Number(
      sessionStorage.getItem('userLocationId') || 0
    );

    const selectedLocationId = Number(
      sessionStorage.getItem('selectedLocationId') || 0
    );

    if (loggedInLocationId === 3 && selectedLocationId > 0) {
      return selectedLocationId;
    }

    return loggedInLocationId;
  }

  private get effectiveLocationName(): string {
    const loggedInLocationId = Number(
      sessionStorage.getItem('userLocationId') || 0
    );

    const loggedInLocationName =
      sessionStorage.getItem('userLocationName') || '';

    const selectedLocationName =
      sessionStorage.getItem('selectedLocationName') || '';

    if (loggedInLocationId === 3 && selectedLocationName) {
      return selectedLocationName;
    }

    return loggedInLocationName;
  }

  fetchProductionDetails(): void {
    const clientDeviceID = 'device123';
    const userID = sessionStorage.getItem('userID');
    const locationID = this.effectiveLocationId;

    const preserveCheckedState = (
      oldList: any[],
      newList: any[]
    ) => {
      return newList.map(item => {
        const existing = oldList.find(
          old => old.ladleNo === item.ladleNo
        );

        return {
          ...item,
          isSelected: existing
            ? existing.isSelected
            : false
        };
      });
    };

    this.service
      .getProductionDetails(
        clientDeviceID,
        userID,
        locationID
      )
      .subscribe((res: any) => {
        if (res?.status && res?.data) {
          const newLadles = (
            res.data.reversalLadles || []
          ).map((ladle: any) => ({
            ...ladle,
            isSelected: false
          }));

          this.reversalLadles = preserveCheckedState(
            this.reversalLadles,
            newLadles
          );

          this.summary = res.data.summary || [];

          this.updateSelectAllCheckbox();
        } else {
          this.reversalLadles = [];
          this.summary = [];
          this.updateSelectAllCheckbox();
        }
      });
  }

  toggleSelectAll(isChecked: boolean): void {
    this.isChecked = isChecked;

    this.reversalLadles.forEach(
      ladle => (ladle.isSelected = this.isChecked)
    );
  }

  updateSelectAllCheckbox(): void {
    if (this.reversalLadles.length === 0) {
      this.isChecked = false;
      this.indeterminate = false;
      return;
    }

    const allSelected = this.reversalLadles.every(
      ladle => ladle.isSelected
    );

    const someSelected = this.reversalLadles.some(
      ladle => ladle.isSelected
    );

    this.isChecked = allSelected;
    this.indeterminate =
      someSelected && !allSelected;
  }

  onLadleCheckboxChange(): void {
    this.updateSelectAllCheckbox();
  }

  fetchTransactionLadles(): void {
    const clientDeviceID = 'device123';
    const userID = sessionStorage.getItem('userID');
    const locationID = this.effectiveLocationId;

    this.service
      .getTransactionLadles(
        clientDeviceID,
        userID,
        locationID
      )
      .subscribe((res: any) => {
        if (res?.status && res?.data) {
          this.transaction = res.data;
        } else {
          this.transaction = [];
        }
      });
  }

  proceedLadleMovement(): void {
    const selectedLadles =
      this.reversalLadles.filter(
        ladle => ladle.isSelected
      );

    if (selectedLadles.length === 0) {
      Swal.fire(
        'No ladles selected',
        'Please select at least one ladle to proceed.',
        'warning'
      );
      return;
    }

    Swal.fire({
      title: 'Are you sure?',
      html: `
        You are about to proceed with the following ladle(s):
        <strong>${selectedLadles
          .map(l => l.ladleNo)
          .join(', ')}</strong>
      `,
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Yes, proceed',
      cancelButtonText: 'Cancel'
    }).then(result => {
      if (!result.isConfirmed) {
        return;
      }

      const payload = {
        clientDeviceID: 'device123',
        userID: sessionStorage.getItem('userID'),
        source: this.effectiveLocationName,
        transactionDateTime:
          this.getFormattedLocalDateTime(),
        reversalDetails: selectedLadles.map(ladle => ({
          ID: uuidv4(),
          ladleNo: ladle.ladleNo,
          movementType: 'Available',
          destination: '0',
          castID: null
        }))
      };

      this.service
        .createRevarsalMovement(payload)
        .subscribe(
          (res: any) => {
            if (res.status) {
              Swal.fire({
                icon: 'success',
                title: 'Success',
                text:
                  res.message ||
                  'Submitted successfully.',
                timer: 3000,
                showConfirmButton: false
              });
            } else {
              Swal.fire({
                icon: 'error',
                title: 'Error',
                text:
                  res.message ||
                  'Something went wrong. Please try again.'
              });
            }

            this.fetchProductionDetails();
            this.fetchTransactionLadles();
          },
          err => {
            Swal.fire({
              icon: 'error',
              title: 'Server Error',
              text:
                'There was an error connecting to the server.'
            });

            console.error(
              'Error submitting data:',
              err
            );
          }
        );
    });
  }

  getFormattedLocalDateTime(): string {
    const now = new Date();

    const pad = (n: number) =>
      n.toString().padStart(2, '0');

    const yyyy = now.getFullYear();
    const MM = pad(now.getMonth() + 1);
    const dd = pad(now.getDate());
    const HH = pad(now.getHours());
    const mm = pad(now.getMinutes());
    const ss = pad(now.getSeconds());

    const ms = now
      .getMilliseconds()
      .toString()
      .padStart(3, '0');

    return `${yyyy}-${MM}-${dd} ${HH}:${mm}:${ss}.${ms}`;
  }
}