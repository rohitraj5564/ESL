import { Injectable, NgZone } from '@angular/core';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';

@Injectable({
  providedIn: 'root'
})
export class AutoLogoutService {

  // 20 minutes of inactivity before auto-logout (VAPT session management standard)
  private readonly TIMEOUT_MS = 20 * 60 * 1000;
  private timer: any;
  private isListening = false;

  constructor(
    private router: Router,
    private ngZone: NgZone,
    private toastr: ToastrService
  ) {
    this.initListener();
  }

  private initListener(): void {
    if (this.isListening) return;
    this.isListening = true;

    // Run event listeners outside Angular zone to avoid triggering change detection on every mouse move
    this.ngZone.runOutsideAngular(() => {
      const activityEvents = [
        'mousemove',
        'mousedown',
        'keydown',
        'touchstart',
        'scroll',
        'click'
      ];

      activityEvents.forEach((eventName) => {
        window.addEventListener(eventName, () => this.resetTimer(), { passive: true });
      });
    });

    this.startTimer();
  }

  private resetTimer(): void {
    if (this.isLoggedIn()) {
      this.startTimer();
    }
  }

  private startTimer(): void {
    if (this.timer) {
      clearTimeout(this.timer);
    }

    if (!this.isLoggedIn()) {
      return;
    }

    this.timer = setTimeout(() => {
      this.onTimeout();
    }, this.TIMEOUT_MS);
  }

  private onTimeout(): void {
    if (!this.isLoggedIn()) {
      return;
    }

    this.ngZone.run(() => {
      sessionStorage.clear();
      localStorage.clear();

      this.toastr.warning(
        'Your session expired due to 20 minutes of inactivity. Please sign in again.',
        'Session Expired',
        {
          timeOut: 5000,
          closeButton: true,
          progressBar: true
        }
      );

      this.router.navigate(['/login']);
    });
  }

  private isLoggedIn(): boolean {
    return sessionStorage.getItem('IslogedIn') === 'True';
  }

  public stop(): void {
    if (this.timer) {
      clearTimeout(this.timer);
    }
  }
}
