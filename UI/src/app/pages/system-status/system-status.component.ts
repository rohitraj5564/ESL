import { Component, OnInit, OnDestroy } from '@angular/core';
import { interval, Subscription } from 'rxjs';
import { MainserviceService } from '../service/mainservice.service';

export type StatusTab = 'reader' | 'camera' | 'controller' | 'application';

export interface SystemStatusItem {
  ID?: string;
  LocationName: string;
  ReaderIP?: string;
  ReaderModifiedDatetime?: string | Date;
  Controller_1IP?: string;
  Controller1ModifiedDatetime?: string | Date;
  Controller_2IP?: string;
  Controller2ModifiedDatetime?: string | Date;
  CameraIP?: string;
  CameraModifiedDatetime?: string | Date;
  ApplicationHealth?: boolean | number;
  ApplicationModifiedDatetime?: string | Date;
  IsActive?: boolean;
  ServerDateTime?: string | Date;

  // Subsystem computed flags & timestamps
  isReaderOnline: boolean;
  readerLastPing: string;

  isCameraOnline: boolean;
  cameraLastPing: string;

  isControllerOnline: boolean;
  controllerLastPing: string;

  isAppRunning: boolean;
  appLastPing: string;
}

@Component({
  selector: 'app-system-status',
  templateUrl: './system-status.component.html',
  styleUrls: ['./system-status.component.css'],
  standalone: false
})
export class SystemStatusComponent implements OnInit, OnDestroy {

  activeTab: StatusTab = 'reader';
  isLoading = false;
  lastUpdate = '';

  systemStatusData: SystemStatusItem[] = [];

  // Categorized lists
  readerList: SystemStatusItem[] = [];
  cameraList: SystemStatusItem[] = [];
  controllerList: SystemStatusItem[] = [];
  applicationList: SystemStatusItem[] = [];

  // Summary counts
  get readerOnlineCount(): number { return this.readerList.filter(r => r.isReaderOnline).length; }
  get readerOfflineCount(): number { return this.readerList.filter(r => !r.isReaderOnline).length; }

  get cameraOnlineCount(): number { return this.cameraList.filter(c => c.isCameraOnline).length; }
  get cameraOfflineCount(): number { return this.cameraList.filter(c => !c.isCameraOnline).length; }

  get controllerOnlineCount(): number { return this.controllerList.filter(c => c.isControllerOnline).length; }
  get controllerOfflineCount(): number { return this.controllerList.filter(c => !c.isControllerOnline).length; }

  get appRunningCount(): number { return this.applicationList.filter(a => a.isAppRunning).length; }
  get appStoppedCount(): number { return this.applicationList.filter(a => !a.isAppRunning).length; }

  private updateSub?: Subscription;
  private timeSub?: Subscription;

  constructor(private mainService: MainserviceService) {}

  ngOnInit(): void {
    this.loadSystemStatusData();
    this.updateLastUpdate();

    // Auto-refresh every 5 seconds internally
    this.updateSub = interval(5000).subscribe(() => {
      this.loadSystemStatusDataSilent();
    });

    // Live clock ticker
    this.timeSub = interval(1000).subscribe(() => {
      this.updateLastUpdate();
    });
  }

  ngOnDestroy(): void {
    this.updateSub?.unsubscribe();
    this.timeSub?.unsubscribe();
  }

  setTab(tab: StatusTab): void {
    this.activeTab = tab;
  }

  refreshData(): void {
    this.loadSystemStatusData();
  }

  private updateLastUpdate(): void {
    const now = new Date();
    const pad = (n: number) => String(n).padStart(2, '0');
    this.lastUpdate = `${pad(now.getDate())}-${pad(now.getMonth() + 1)}-${now.getFullYear()} `
      + `${pad(now.getHours())}:${pad(now.getMinutes())}:${pad(now.getSeconds())}`;
  }

  loadSystemStatusData(): void {
    this.isLoading = true;
    this.mainService.getSystemStatus().subscribe({
      next: (res: any) => {
        this.isLoading = false;
        const rawList = res?.data ? res.data : (Array.isArray(res) ? res : []);
        this.processSystemStatusData(rawList);
      },
      error: (err) => {
        console.error('Failed to load system status data:', err);
        this.isLoading = false;
      }
    });
  }

  private loadSystemStatusDataSilent(): void {
    this.mainService.getSystemStatus().subscribe({
      next: (res: any) => {
        const rawList = res?.data ? res.data : (Array.isArray(res) ? res : []);
        this.processSystemStatusData(rawList);
        console.log(`[System Status] Internal auto-refresh completed at ${new Date().toLocaleTimeString()}`);
      },
      error: () => {}
    });
  }

  private isWithin10Minutes(dateVal?: string | Date | null): boolean {
    if (!dateVal) return false;
    const timeMs = new Date(dateVal).getTime();
    if (isNaN(timeMs)) return false;
    const now = new Date().getTime();
    const diffMinutes = Math.abs(now - timeMs) / (1000 * 60);
    return diffMinutes <= 10;
  }

  private formatTimestamp(dateVal?: string | Date | null): string {
    if (!dateVal) return '—';
    const date = new Date(dateVal);
    if (isNaN(date.getTime())) return '—';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${pad(date.getDate())}-${pad(date.getMonth() + 1)}-${date.getFullYear()} `
      + `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
  }

  private processSystemStatusData(rawList: any[]): void {
    this.systemStatusData = rawList.map(item => {
      // Reader status & ping
      const isReaderOnline = this.isWithin10Minutes(item.ReaderModifiedDatetime);
      const readerLastPing = this.formatTimestamp(item.ReaderModifiedDatetime);

      // Camera status & ping
      const isCameraOnline = this.isWithin10Minutes(item.CameraModifiedDatetime);
      const cameraLastPing = this.formatTimestamp(item.CameraModifiedDatetime);

      // Controller status & ping (active if either Controller 1 or Controller 2 was updated within 10 min)
      const isControllerOnline = this.isWithin10Minutes(item.Controller1ModifiedDatetime) ||
                                 this.isWithin10Minutes(item.Controller2ModifiedDatetime);
      const controllerLastPing = this.formatTimestamp(item.Controller1ModifiedDatetime || item.Controller2ModifiedDatetime);

      // Application Health: 1 / true -> Running, 0 / false / null -> Stopped
      const isAppRunning = item.ApplicationHealth === true ||
                           item.ApplicationHealth === 1 ||
                           item.ApplicationHealth === '1' ||
                           item.ApplicationHealth === 'True' ||
                           item.ApplicationHealth === 'true';
      const appLastPing = this.formatTimestamp(item.ApplicationModifiedDatetime);

      return {
        ...item,
        LocationName: (item.LocationName || '').trim(),
        isReaderOnline,
        readerLastPing,
        isCameraOnline,
        cameraLastPing,
        isControllerOnline,
        controllerLastPing,
        isAppRunning,
        appLastPing
      };
    });

    // Populate filtered lists
    this.readerList = this.systemStatusData.filter(item => !!item.ReaderIP && item.ReaderIP.trim() !== '');
    this.cameraList = this.systemStatusData.filter(item => !!item.CameraIP && item.CameraIP.trim() !== '');
    this.controllerList = this.systemStatusData.filter(item =>
      (!!item.Controller_1IP && item.Controller_1IP.trim() !== '') ||
      (!!item.Controller_2IP && item.Controller_2IP.trim() !== '')
    );
    this.applicationList = [...this.systemStatusData];
  }
}
