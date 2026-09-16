import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface AppConfig {
  apiUrl: string;
  sessionTimeoutMinutes?: number;
}

@Injectable({ providedIn: 'root' })
export class AppConfigService {
  private config: AppConfig = { apiUrl: '', sessionTimeoutMinutes: 15 };

  constructor(private http: HttpClient) {}

  async loadConfig(): Promise<void> {
    const data = await firstValueFrom(
      this.http.get<AppConfig>('assets/config/config.json')
    );
    this.config = data;
  }

  get apiUrl(): string {
    return this.config.apiUrl;
  }

  get sessionTimeoutMinutes(): number {
    return this.config.sessionTimeoutMinutes ?? 15;
  }
}