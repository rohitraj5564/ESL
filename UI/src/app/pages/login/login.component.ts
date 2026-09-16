import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';

import { MainserviceService } from '../service/mainservice.service';
import { EncryptionService } from '../service/encryption.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    HttpClientModule
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent implements OnInit {

  showModal: boolean = false;
  keyInput: string = '';
  loginPayload: any;
  
  showPassword = false;
  loginFailed: boolean = false;
  usernameError: boolean = false;
  passwordError: boolean = false;
  enteredUserName: string = '';

  isLoading: boolean = false;
  errorMessage: string = '';

  activationKey: string | null = null;

  constructor(
    private service: MainserviceService,
    private router: Router,
    private toastr: ToastrService,
    private encryption: EncryptionService
  ) { }

  // ============================================================
  // INITIALIZATION
  // ============================================================

  ngOnInit(): void {
    this.fetchActivationKey();
  }

  // ============================================================
  // ACTIVATION KEY
  // ============================================================

  fetchActivationKey(): void {
    const defaultKey = sessionStorage.getItem('activationKey') || '78536E5557697A637453794168454A354F53434F54773D3D';
    this.activationKey = defaultKey;
    this.service.setKey(defaultKey);

    this.service.getActivationKey().subscribe({
      next: (res: any) => {
        if (res?.status && res?.data) {
          this.activationKey = res.data;
          this.service.setKey(res.data);
          sessionStorage.setItem('activationKey', res.data);
        }
      },
      error: (err) => {
        console.warn('Could not fetch remote activation key, using default:', err);
      }
    });
  }

  // ============================================================
  // DEVTOOLS CHECK
  // ============================================================

  private isDevToolsOpen(): boolean {
    // Disabled window-dimension heuristic which causes false-positive lockouts on high-DPI/dual displays
    return false;
  }

  // ============================================================
  // INPUT VALIDATION
  // ============================================================

  private isValidInput(
    username: string,
    password: string
  ): boolean {

    const sqlPatterns =
      /(\b(SELECT|INSERT|UPDATE|DELETE|DROP|UNION|EXEC|CAST)\b)|(--)|(;)/gi;

    if (
      sqlPatterns.test(username) ||
      sqlPatterns.test(password)
    ) {

      this.toastr.error(
        'Invalid input detected!',
        'Security Alert',
        {
          timeOut: 3000,
          closeButton: true,
          progressBar: true
        }
      );

      return false;
    }

    const xssPatterns =
      /(<script|<\/script>|javascript:|onerror=|onload=|alert\()/gi;

    if (
      xssPatterns.test(username) ||
      xssPatterns.test(password)
    ) {

      this.toastr.error(
        'Invalid input detected!',
        'Security Alert',
        {
          timeOut: 3000,
          closeButton: true,
          progressBar: true
        }
      );

      return false;
    }

    if (
      username.length > 50 ||
      password.length > 100
    ) {

      this.toastr.error(
        'Input too long!',
        'Validation Error',
        {
          timeOut: 3000,
          closeButton: true,
          progressBar: true
        }
      );

      return false;
    }

    return true;
  }

  // ============================================================
  // LOGIN FAILED ANIMATION
  // ============================================================

  private triggerLoginFailedBlink(): void {

    this.loginFailed = false;

    setTimeout(() => {

      this.loginFailed = true;

      setTimeout(() => {
        this.loginFailed = false;
      }, 1000);

    }, 10);
  }

  // ============================================================
  // SAVE LOGIN SESSION
  // ============================================================

  private saveLoginSession(
    response: any,
    activationKey: string
  ): void {

    sessionStorage.setItem(
      'IslogedIn',
      'True'
    );

    sessionStorage.setItem(
      'userID',
      response.data.userID
    );

    const resolvedUserName = response?.data?.userName || this.enteredUserName || '';
    sessionStorage.setItem('userName', resolvedUserName);
    localStorage.setItem('userID', response?.data?.userID || '');
    localStorage.setItem('userName', resolvedUserName);

    sessionStorage.setItem(
      'userLocationId',
      response.data.userLocationId
    );

    sessionStorage.setItem(
      'userLocationName',
      response.data.userLocationName
    );

    sessionStorage.setItem(
      'userFirstName',
      response.data.userFirstName
    );

    sessionStorage.setItem(
      'sessionToken',
      response.data.sessionToken
    );

    sessionStorage.setItem(
      'apiUrl',
      this.service.getApiUrl()
    );

    sessionStorage.setItem(
      'activationKey',
      activationKey
    );

    sessionStorage.setItem(
      'jwtToken',
      response.data.jwtToken
    );
  }

  // ============================================================
  // USER LOCATION BASED ROUTING
  // ============================================================

  private navigateByUserLocation(
    locationId: number
  ): void {

    // Blast Furnace
    // BF1 = 9
    // BF2 = 1
    // BF3 = 2
    if ([1, 2, 9].includes(locationId)) {

      this.router.navigate([
        '/blastFurnace'
      ]);

      return;
    }

    // Production
    // SMS = 4
    // DIP = 5
    // PCM = 6
    if ([4, 5, 6].includes(locationId)) {

      this.router.navigate([
        '/production'
      ]);

      return;
    }

    // Maintenance
    // LRS = 7
    if (locationId === 7) {

      this.router.navigate([
        '/maintenance'
      ]);

      return;
    }

    // Live Tracking / WB
    // WB = 3
    if (locationId === 3) {

      this.router.navigate([
        '/esldashboard'
      ]);

      return;
    }

    // Default / Live Tracking
    this.router.navigate([
      '/esldashboard'
    ]);
  }

  // ============================================================
  // HANDLE SUCCESSFUL LOGIN
  // ============================================================

  private handleSuccessfulLogin(
    response: any,
    activationKey: string
  ): void {
    this.isLoading = false;
    this.errorMessage = '';

    this.saveLoginSession(
      response,
      activationKey
    );

    const locationId = Number(response?.data?.userLocationId);
    this.navigateByUserLocation(locationId);

    this.toastr.success(
      response.message || 'Login Successfully',
      'Login Successfully',
      {
        timeOut: 2000,
        closeButton: true,
        progressBar: true
      }
    );
  }

  // ============================================================
  // CHECK WHETHER RESPONSE IS A LOGIN FAILURE
  // ============================================================

  private isLoginFailure(response: any): boolean {

    return !response?.status;
  }

  // ============================================================
  // SHOW LOGIN RESPONSE ERROR
  // ============================================================

  private handleLoginResponseError(
    response: any
  ): void {
    this.isLoading = false;
    this.errorMessage = response?.message || 'Login Failed';

    if (
      response?.message &&
      response.message.includes(
        'already logged in'
      )
    ) {

      this.toastr.warning(
        'This user is already logged in on another device. Please logout first.',
        'Already Logged In',
        {
          timeOut: 3000,
          closeButton: true,
          progressBar: true
        }
      );

      return;
    }

    if (response?.message === '777') {

      this.showModal = true;

      return;
    }

    this.triggerLoginFailedBlink();

    this.toastr.error(
      response?.message || 'Login Failed',
      'Login Error',
      {
        timeOut: 3000,
        closeButton: true,
        progressBar: true
      }
    );
  }

  // ============================================================
  // HANDLE HTTP ERROR
  // ============================================================

  private handleHttpError(
    error: any
  ): void {
    this.isLoading = false;

    let errorMessage =
      'An unexpected error occurred';

    let errorTitle =
      'Error';

    if (error.status === 0) {

      errorMessage =
        'Network error. Please check your internet connection.';

      errorTitle =
        'Network Error';

    } else if (error.status === 500) {

      if (
        error.error?.message &&
        error.error.message.includes(
          'already logged in'
        )
      ) {

        this.toastr.warning(
          'This user is already logged in on another device. Please logout first.',
          'Already Logged In',
          {
            timeOut: 3000,
            closeButton: true,
            progressBar: true
          }
        );

        return;
      }

      errorMessage =
        error.error?.message || 'Internal server error. Please try again later.';

      errorTitle =
        'Server Error';

    } else if (error.status === 503) {

      errorMessage =
        'Server error. The server is temporarily unavailable.';

      errorTitle =
        'Server Error';

    } else if (error.status === 504) {

      errorMessage =
        'Request timed out. Please try again.';

      errorTitle =
        'Timeout Error';
    }

    this.errorMessage = errorMessage;
    this.triggerLoginFailedBlink();

    this.toastr.error(
      errorMessage,
      errorTitle,
      {
        timeOut: 2000,
        closeButton: true,
        progressBar: true
      }
    );
  }

  // ============================================================
  // MAIN LOGIN
  // ============================================================

  Login(
    UserName: string,
    Password: string
  ): void {
    this.enteredUserName = UserName ? UserName.trim() : '';

    if (this.isLoading) {
      return;
    }

    this.errorMessage = '';

    if (this.isDevToolsOpen()) {

      this.toastr.error(
        'Please close Developer Tools before logging in.',
        'Security Alert',
        {
          timeOut: 3000,
          closeButton: true,
          progressBar: true
        }
      );

      return;
    }

    if (!this.activationKey) {
      this.activationKey = sessionStorage.getItem('activationKey') || '78536E5557697A637453794168454A354F53434F54773D3D';
      this.service.setKey(this.activationKey);
    }

    this.usernameError = false;
    this.passwordError = false;

    if (!UserName?.trim()) {

      this.usernameError = true;

      this.toastr.error(
        'Username is required',
        'Validation Error',
        {
          timeOut: 2000,
          closeButton: true,
          progressBar: true
        }
      );

      return;
    }

    if (!Password?.trim()) {

      this.passwordError = true;

      this.toastr.error(
        'Password is required',
        'Validation Error',
        {
          timeOut: 2000,
          closeButton: true,
          progressBar: true
        }
      );

      return;
    }

    if (
      !this.isValidInput(
        UserName,
        Password
      )
    ) {
      return;
    }

    const encryptedUsername =
      this.encryption.encrypt(
        UserName
      );

    const encryptedPassword =
      this.encryption.encrypt(
        Password
      );

    this.loginPayload = {

      userName:
        encryptedUsername,

      password:
        encryptedPassword,

      activationKey:
        this.activationKey
    };

    // ========================================================
    // FIRST: Dashboard / WB LOGIN
    // ========================================================

    this.isLoading = true;
    this.errorMessage = '';

    this.service
      .loginDashboard(
        this.loginPayload
      )
      .subscribe({

        next: (response: any) => {

          // Dashboard login succeeded
          if (
            response?.status === true
          ) {

            this.handleSuccessfulLogin(
              response,
              this.activationKey!
            );

            return;
          }

          // Direct failure from Dashboard login (DB presence, IsActive, or AD credentials error)
          this.handleLoginResponseError(response);

        },

        error: (error) => {

          // If Dashboard API is unavailable,
          // do not blindly switch on server/network errors.
          if (
            error.status === 401 ||
            error.status === 403
          ) {

            this.tryPDALogin();

            return;
          }

          this.handleHttpError(error);
        }
      });
  }

  // ============================================================
  // PDA LOGIN
  // ============================================================

  private tryPDALogin(): void {

    if (!this.loginPayload) {
      return;
    }

    this.service
      .loginPDADashboard(
        this.loginPayload
      )
      .subscribe({

        next: (response: any) => {

          if (
            response?.status === true
          ) {

            this.handleSuccessfulLogin(
              response,
              this.activationKey!
            );

            return;
          }

          this.handleLoginResponseError(
            response
          );
        },

        error: (error) => {

          this.handleHttpError(
            error
          );
        }
      });
  }

  // ============================================================
  // MANUAL ACTIVATION KEY OVERRIDE
  // ============================================================

  submitKey(): void {

    if (this.isDevToolsOpen()) {

      this.toastr.error(
        'Please close Developer Tools before logging in.',
        'Security Alert',
        {
          timeOut: 3000,
          closeButton: true,
          progressBar: true
        }
      );

      return;
    }

    if (!this.keyInput.trim()) {

      this.toastr.error(
        'Please enter a valid key',
        'Validation Error',
        {
          timeOut: 2000,
          closeButton: true,
          progressBar: true
        }
      );

      return;
    }

    if (
      !this.isValidInput(
        this.loginPayload?.userName || '',
        this.keyInput
      )
    ) {
      return;
    }

    this.service.setKey(
      this.keyInput
    );

    this.activationKey =
      this.keyInput;

    this.loginPayload = {

      ...this.loginPayload,

      activationKey:
        this.keyInput
    };

    // ========================================================
    // FIRST: Dashboard Login
    // ========================================================

    this.isLoading = true;
    this.errorMessage = '';

    this.service
      .loginDashboard(
        this.loginPayload
      )
      .subscribe({

        next: (response: any) => {

          if (
            response?.status === true
          ) {

            this.handleSuccessfulLogin(
              response,
              this.keyInput
            );

            this.closeModal();

            return;
          }

          // If Dashboard login fails,
          // try PDA login.
          this.tryPDAKeyLogin();

        },

        error: (error) => {

          if (
            error.status === 401 ||
            error.status === 403
          ) {

            this.tryPDAKeyLogin();

            return;
          }

          this.handleHttpError(
            error
          );
        }
      });
  }

  // ============================================================
  // PDA MANUAL KEY LOGIN
  // ============================================================

  private tryPDAKeyLogin(): void {

    if (!this.loginPayload) {
      return;
    }

    this.service
      .loginPDADashboard(
        this.loginPayload
      )
      .subscribe({

        next: (response: any) => {

          if (
            response?.status === true
          ) {

            this.handleSuccessfulLogin(
              response,
              this.keyInput
            );

            this.closeModal();

            return;
          }

          if (
            response?.message === '777'
          ) {

            this.toastr.error(
              'Please enter valid key',
              'Invalid Key',
              {
                timeOut: 2000,
                closeButton: true,
                progressBar: true
              }
            );

            return;
          }

          this.handleLoginResponseError(
            response
          );
        },

        error: (error) => {

          this.handleHttpError(
            error
          );
        }
      });
  }

  // ============================================================
  // CLOSE ACTIVATION KEY MODAL
  // ============================================================

  closeModal(): void {

    this.showModal = false;
    this.keyInput = '';
  }
}