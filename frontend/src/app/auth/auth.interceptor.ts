import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

import { AuthService } from './auth.service';

/** Adds "Authorization: Bearer <token>" to API calls and sends the user to /login on 401. */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const token = auth.token;
  const isAuthCall = request.url.includes('/api/auth/');

  const authorized = token && !isAuthCall
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;

  return next(authorized).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && !isAuthCall) {
        auth.handleUnauthorized();
      }
      return throwError(() => error);
    })
  );
};
