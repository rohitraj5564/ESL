import { Injectable } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Router } from '@angular/router';

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
          console.error('401 Unauthorized on request:', req.url);

          const isLoggedIn = sessionStorage.getItem('IslogedIn');
          const userID = sessionStorage.getItem('userID');
          const activationKey = sessionStorage.getItem('activationKey');
          const apiUrl = sessionStorage.getItem('apiUrl');

          if (isLoggedIn === 'True' && userID && apiUrl && activationKey) {
            fetch(`${apiUrl}DashboardLogout`, {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ userID, activationKey }),
              keepalive: true
            }).finally(() => {
              sessionStorage.clear();
              localStorage.clear();
              this.router.navigate(['/login']);
            });
          } else {
            sessionStorage.clear();
            localStorage.clear();
            this.router.navigate(['/login']);
          }
        }
        return throwError(() => error);
      })
    );
  }
}