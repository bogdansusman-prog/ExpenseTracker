export interface AuthUser {
  id: string;
  email: string;
  displayName: string;
}

export interface AuthResponse {
  token: string;
  expiresAt: string;
  user: AuthUser;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest extends LoginRequest {
  displayName: string;
}
