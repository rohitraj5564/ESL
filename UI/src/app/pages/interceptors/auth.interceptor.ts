import { Injectable } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Router } from '@angular/router';
import Swal from 'sweetalert2';

@Injectable()
export class AuthInterceptor implements HttpInterceptor {

  constructor(private router: Router) { }

  intercept(req: HttpRequest<any>,
    next: HttpHandler): Observable<HttpEvent<any>> {

    const cleanUrl = req.url.replace(/(https?:\/\/)|(\/)+/g, (m, p1) => p1 || '/');
    const jwtToken = sessionStorage.getItem('jwtToken');

    if (jwtToken) {
      req = req.clone({
        url: cleanUrl,
        setHeaders: {
          Authorization: `Bearer ${jwtToken}`,
          'X-Api-Version': '1.0'
        }
      });
    } else if (cleanUrl !== req.url) {
      req = req.clone({ url: cleanUrl });
    }

    return next.handle(req).pipe(
      catchError((error: HttpErrorResponse) => {
        if (error.status === 401) {
          console.warn('401 Unauthorized on request:', req.url);

          const terminationMsg = error.error?.message || 'Your session was terminated because this account was logged in from another browser or device.';

          sessionStorage.clear();
          localStorage.clear();

          if (!Swal.isVisible()) {
            Swal.fire({
              icon: 'warning',
              title: 'Session Terminated',
              text: terminationMsg,
              confirmButtonText: 'OK',
              allowOutsideClick: false
            }).then(() => {
              this.router.navigate(['/login']);
            });
          } else {
            this.router.navigate(['/login']);
          }
        }
        return throwError(() => error);
      })
    );
  }
}