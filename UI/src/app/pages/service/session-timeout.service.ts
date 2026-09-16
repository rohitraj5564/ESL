import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';
import { Router } from '@angular/router';
import Swal from 'sweetalert2';
import { ToastrService } from 'ngx-toastr';
import { AppConfigService } from './app-config.service';

@Injectable({
  providedIn: 'root'
})
export class SessionTimeoutService {
  private inactivityTimeout: any;
  private activitySubject = new Subject<void>();
  public sessionTimeoutSubject = new Subject<void>();

  private readonly activityEvents = [
    'mousemove',
    'mousedown',
    'mouseup',
    'click',
    'dblclick',
    'keydown',
    'keyup',
    'keypress',
    'touchstart',
    'touchend',
    'scroll',
    'wheel'
  ];

  constructor(
    private router: Router,
    private toastr: ToastrService,
    private configService: AppConfigService
  ) {
    this.setupActivityTracking();
    this.setupTabCloseTracking();
  }

  // Get timeout duration in milliseconds from config (defaults to 15 min if not configured)
  private get timeoutDuration(): number {
    const minutes = this.configService?.sessionTimeoutMinutes;
    return (minutes && minutes > 0 ? minutes : 15) * 60 * 1000;
  }

  // Initialize the session timeout
  initSessionTimeout() {
    this.resetTimeout();
  }

  // Reset the timeout on user activity
  resetTimeout() {
    clearTimeout(this.inactivityTimeout);
    this.inactivityTimeout = setTimeout(() => {
      this.logoutDueToInactivity();
    }, this.timeoutDuration);
  }

  // Track user activity (mouse and keyboard)
  setupActivityTracking(tableElements?: HTMLElement[]) {
    this.activityEvents.forEach(event => {
      window.addEventListener(event, this.handleActivity, { passive: true });
      document.addEventListener(event, this.handleActivity, { passive: true });
    });

    if (tableElements) {
      tableElements.forEach(table => {
        table.addEventListener('scroll', this.handleActivity, { passive: true });
      });
    }

    this.activitySubject.subscribe(() => {
      this.resetTimeout();
    });
  }

  private handleActivity = () => {
    this.activitySubject.next();
  };

  //  Handle Tab Close / Browser Close
  private setupTabCloseTracking(): void {

    // Set refresh flag on every page load
    sessionStorage.setItem('isRefresh', 'true');

    // Remove flag after 2 seconds
    setTimeout(() => {
      sessionStorage.removeItem('isRefresh');
    }, 2000);

    window.addEventListener('beforeunload', () => {
      const userID = sessionStorage.getItem('userID');
      const isLoggedIn = sessionStorage.getItem('IslogedIn');
      const isRefresh = sessionStorage.getItem('isRefresh');

      // ✅ Only logout if tab is closing NOT refreshing
      if (userID && isLoggedIn === 'True' && !isRefresh) {
        const apiUrl = sessionStorage.getItem('apiUrl');
        const activationKey = sessionStorage.getItem('activationKey');

        if (apiUrl && activationKey) {
          // ✅ fetch with keepalive — fixes CORS OPTIONS issue
          fetch(`${apiUrl}DashboardLogout`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ userID, activationKey }),
            keepalive: true
          });
        }

        // Clear all storage
        sessionStorage.clear();
        localStorage.clear();
        this.clearAllCookies();
      }
    });
  }

  // ✅ Handle logout due to inactivity
  private logoutDueToInactivity() {
    if (Swal.isVisible()) {
      Swal.close();
    }
    this.toastr.clear();
    this.sessionTimeoutSubject.next();

    const userID = sessionStorage.getItem('userID');
    const apiUrl = sessionStorage.getItem('apiUrl');
    const activationKey = sessionStorage.getItem('activationKey');

    // ✅ fetch with keepalive — fixes CORS OPTIONS issue
    if (userID && apiUrl && activationKey) {
      fetch(`${apiUrl}DashboardLogout`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userID, activationKey }),
        keepalive: true
      }).finally(() => {
        this.clearStorage();
        this.router.navigate(['/login']);
      });
    } else {
      this.clearStorage();
      this.router.navigate(['/login']);
    }
  }

  // ✅ Single method to clear everything
  private clearStorage(): void {
    sessionStorage.clear();
    localStorage.clear();
    this.clearAllCookies();
    this.clearCacheStorage();
  }

  private clearAllCookies(): void {
    const cookies = document.cookie.split(';');
    for (const cookie of cookies) {
      const eqPos = cookie.indexOf('=');
      const name = eqPos > -1 ? cookie.substring(0, eqPos).trim() : cookie.trim();
      document.cookie = `${name}=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/`;
      document.cookie = `${name}=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/;domain=${location.hostname}`;
    }
  }

  private clearCacheStorage(): void {
    if ('caches' in window) {
      caches.keys().then(cacheNames => {
        cacheNames.forEach(cacheName => caches.delete(cacheName));
      });
    }
  }

  // Clean up event listeners and timeout
  cleanup(tableElements?: HTMLElement[]) {
    clearTimeout(this.inactivityTimeout);

    this.activityEvents.forEach(event => {
      window.removeEventListener(event, this.handleActivity);
      document.removeEventListener(event, this.handleActivity);
    });

    if (tableElements) {
      tableElements.forEach(table => {
        table.removeEventListener('scroll', this.handleActivity);
      });
    }
  }
}