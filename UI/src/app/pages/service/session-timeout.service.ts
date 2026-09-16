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

  private heartbeatInterval: any;

  constructor(
    private router: Router,
    private toastr: ToastrService,
    private configService: AppConfigService
  ) {
    this.setupActivityTracking();
    this.setupTabCloseTracking();
    this.startSessionHeartbeat();
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
    // Clear refresh flag on init
    sessionStorage.removeItem('isRefresh');

    // Track reload shortcut keys (F5, Ctrl+R, Cmd+R)
    window.addEventListener('keydown', (e: KeyboardEvent) => {
      if (e.key === 'F5' || (e.key === 'r' && (e.ctrlKey || e.metaKey))) {
        sessionStorage.setItem('isRefresh', 'true');
      }
    });

    const sendCloseBeacon = () => {
      const userID = sessionStorage.getItem('userID') || localStorage.getItem('userID');
      const userName = sessionStorage.getItem('userName') || localStorage.getItem('userName');
      const isLoggedIn = sessionStorage.getItem('IslogedIn') || localStorage.getItem('IslogedIn');
      const isRefresh = sessionStorage.getItem('isRefresh');

      // ✅ Only logout if tab is closing NOT refreshing
      if ((userID || userName) && isLoggedIn === 'True' && !isRefresh) {
        const apiUrl = sessionStorage.getItem('apiUrl') || localStorage.getItem('apiUrl');
        const activationKey = sessionStorage.getItem('activationKey');

        if (apiUrl) {
          fetch(`${apiUrl}EndUserSession`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ userID, userName, reason: 'Browser Closed', activationKey }),
            keepalive: true
          });
        }

        // Clear all storage
        sessionStorage.clear();
        localStorage.clear();
        this.clearAllCookies();
      }
    };

    window.addEventListener('beforeunload', sendCloseBeacon);
    window.addEventListener('pagehide', (e: PageTransitionEvent) => {
      if (!e.persisted) {
        sendCloseBeacon();
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

    const userID = sessionStorage.getItem('userID') || localStorage.getItem('userID');
    const userName = sessionStorage.getItem('userName') || localStorage.getItem('userName');
    const apiUrl = sessionStorage.getItem('apiUrl') || localStorage.getItem('apiUrl');
    const activationKey = sessionStorage.getItem('activationKey');

    // ✅ fetch with keepalive to record Inactivity Timeout
    if ((userID || userName) && apiUrl) {
      fetch(`${apiUrl}EndUserSession`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userID, userName, reason: 'Inactivity Timeout', activationKey }),
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
    if (this.heartbeatInterval) {
      clearInterval(this.heartbeatInterval);
    }
    sessionStorage.clear();
    localStorage.clear();
    this.clearAllCookies();
    this.clearCacheStorage();
  }

  // ✅ Periodically verify active session to kick out superseded browsers
  public startSessionHeartbeat(): void {
    if (this.heartbeatInterval) {
      clearInterval(this.heartbeatInterval);
    }

    this.heartbeatInterval = setInterval(() => {
      const isLoggedIn = sessionStorage.getItem('IslogedIn');
      const jwtToken = sessionStorage.getItem('jwtToken');
      const apiUrl = sessionStorage.getItem('apiUrl');

      if (isLoggedIn === 'True' && jwtToken && apiUrl) {
        fetch(`${apiUrl}CheckSession`, {
          method: 'GET',
          headers: {
            'Authorization': `Bearer ${jwtToken}`,
            'X-Api-Version': '1.0'
          }
        }).then(response => {
          if (response.status === 401) {
            response.json().then(data => {
              const msg = data?.message || 'Your session was terminated because this account was logged in from another browser or device.';
              this.handleSessionTermination(msg);
            }).catch(() => {
              this.handleSessionTermination('Your session was terminated because this account was logged in from another browser or device.');
            });
          }
        }).catch(() => {
          // Ignore transient network errors
        });
      }
    }, 5000);
  }

  private handleSessionTermination(msg: string): void {
    if (this.heartbeatInterval) {
      clearInterval(this.heartbeatInterval);
    }
    this.clearStorage();
    if (!Swal.isVisible()) {
      Swal.fire({
        icon: 'warning',
        title: 'Session Terminated',
        text: msg,
        confirmButtonText: 'OK',
        allowOutsideClick: false
      }).then(() => {
        this.router.navigate(['/login']);
      });
    } else {
      this.router.navigate(['/login']);
    }
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
    if (this.heartbeatInterval) {
      clearInterval(this.heartbeatInterval);
    }

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