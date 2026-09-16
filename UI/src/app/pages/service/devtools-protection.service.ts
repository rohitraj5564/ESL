import { Injectable, NgZone } from '@angular/core';
import { Router } from '@angular/router';

@Injectable({
  providedIn: 'root'
})
export class DevtoolsProtectionService {

  private devtoolsOpen = false;
  private threshold = 200;
  private checkInterval: any;

  constructor(private router: Router, private ngZone: NgZone) { }

  startProtection(): void {
    // Disabled right-click protection for debugging / console inspection
  }

  private disableRightClick(): void {
    // Disabled
  }

  stopProtection(): void {
    // No-op
  }
}