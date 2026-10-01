import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

import { AuthService } from './auth.service';

/** Adds "Authorization: Bearer <token>" to API calls and sends the user to /login on 401. */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const token = auth.token;
  // login / register are anonymous; /api/auth/me, profile etc. need the token
  const isAuthCall = /\/api\/auth\/(login|register)$/.test(request.url);

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
