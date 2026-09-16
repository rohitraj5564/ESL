import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { AppConfigService } from './app-config.service';

@Injectable({
  providedIn: 'root'
})
export class DashboardService {

  constructor(private http: HttpClient, private configService: AppConfigService) { }

  private get apiUrl(): string {
    return this.configService.apiUrl;
  }

  getKPIs(): Observable<any> {
    return this.http.get<any>(this.apiUrl + "kpis").pipe(
      map(res => (res && Array.isArray(res) && res.length > 0) ? res : this.getMockKPIs()),
      catchError(() => of(this.getMockKPIs()))
    );
  }

  getAnalyticalDashboardData(): Observable<any> {
    return this.http.get<any>(this.apiUrl + "GetAnalyticalDashboardData").pipe(
      map(res => {
        if (res && res.status && res.data) return res;
        if (res && res.LocationData) return { status: true, data: res };
        return { status: true, data: this.getMockAnalyticalData() };
      }),
      catchError(() => of({ status: true, data: this.getMockAnalyticalData() }))
    );
  }

  getLadleChart(): Observable<any[]> {
    return this.http.get<any[]>(this.apiUrl + "hourly-ladle-data").pipe(
      map(res => (res && Array.isArray(res) && res.length > 0) ? res : this.getMockLadleChart()),
      catchError(() => of(this.getMockLadleChart()))
    );
  }

  getHourlyTrips(): Observable<any> {
    return this.http.get<any>(this.apiUrl + "GetHourlyTrips").pipe(
      map(res => (res && (res.HourlyTrips || Array.isArray(res))) ? res : this.getMockHourlyTrips()),
      catchError(() => of(this.getMockHourlyTrips()))
    );
  }

  getHourlyProductionConsumptionReport(): Observable<any[]> {
    return this.http.get<any[]>(this.apiUrl + "hourly-production-consumption").pipe(
      map(res => (res && Array.isArray(res) && res.length > 0) ? res : this.getMockProductionReport()),
      catchError(() => of(this.getMockProductionReport()))
    );
  }

  private getMockKPIs() {
    return [
      { Title: "Total Ladles", Value: "38", Color: "blue", Icon: "inbox", Trend: "up" },
      { Title: "Active Ladles", Value: "18", Color: "green", Icon: "flash_on", Trend: "up" },
      { Title: "Idle Ladles", Value: "20", Color: "yellow", Icon: "pause_circle", Trend: "flat" },
      { Title: "Completed Trips", Value: "46", Color: "cyan", Icon: "check_circle", Trend: "up" },
      { Title: "Pending Trips", Value: "5", Color: "orange", Icon: "schedule", Trend: "flat" },
      { Title: "Average TAT", Value: "01:25", Color: "purple", Icon: "timelapse", Trend: "down" }
    ];
  }

  private getMockAnalyticalData() {
    return {
      TotalActiveLadleCount: 38,
      TotalInUseLadleCount: 18,
      CompletedTrips: 46,
      PendingTrips: 5,
      LocationData: [
        { LocationName: 'BF1', AverageTATSTR: '01:25', LadleList: [{ Name: 'LD-101' }, { Name: 'LD-104' }] },
        { LocationName: 'BF2', AverageTATSTR: '01:40', LadleList: [{ Name: 'LD-102' }, { Name: 'LD-108' }] },
        { LocationName: 'BF3', AverageTATSTR: '01:15', LadleList: [{ Name: 'LD-103' }] },
        { LocationName: 'SMS', AverageTATSTR: '02:10', LadleList: [{ Name: 'LD-109' }, { Name: 'LD-110' }] },
        { LocationName: 'DIP', AverageTATSTR: '01:50', LadleList: [{ Name: 'LD-111' }] },
        { LocationName: 'PCM', AverageTATSTR: '01:30', LadleList: [{ Name: 'LD-112' }] },
        { LocationName: 'LRS', AverageTATSTR: '03:15', LadleList: [{ Name: 'LD-114' }] }
      ]
    };
  }

  private getMockLadleChart() {
    return [
      { Time: '06:00', TotalActiveLadle: 14 },
      { Time: '08:00', TotalActiveLadle: 16 },
      { Time: '10:00', TotalActiveLadle: 18 },
      { Time: '12:00', TotalActiveLadle: 17 },
      { Time: '14:00', TotalActiveLadle: 19 },
      { Time: '16:00', TotalActiveLadle: 18 },
      { Time: '18:00', TotalActiveLadle: 20 }
    ];
  }

  private getMockHourlyTrips() {
    return [
      { hour: '06:00', actual: 4, target: 5 },
      { hour: '08:00', actual: 6, target: 6 },
      { hour: '10:00', actual: 7, target: 6 },
      { hour: '12:00', actual: 5, target: 5 },
      { hour: '14:00', actual: 8, target: 7 },
      { hour: '16:00', actual: 7, target: 7 },
      { hour: '18:00', actual: 9, target: 8 }
    ];
  }

  private getMockProductionReport() {
    return [
      { HourSlot: '2026-09-02 06:00', HourTimeSlot: '06:00', hourSlot: '2026-09-02T06:00:00', bf1Production: 380, BF2Production: 420, BF3Production: 310, SMSConsumption: 520, DIPConsumption: 280, PCMConsumption: 210 },
      { HourSlot: '2026-09-02 08:00', HourTimeSlot: '08:00', hourSlot: '2026-09-02T08:00:00', bf1Production: 410, BF2Production: 450, BF3Production: 340, SMSConsumption: 580, DIPConsumption: 310, PCMConsumption: 230 },
      { HourSlot: '2026-09-02 10:00', HourTimeSlot: '10:00', hourSlot: '2026-09-02T10:00:00', bf1Production: 390, BF2Production: 430, BF3Production: 330, SMSConsumption: 550, DIPConsumption: 290, PCMConsumption: 220 },
      { HourSlot: '2026-09-02 12:00', HourTimeSlot: '12:00', hourSlot: '2026-09-02T12:00:00', bf1Production: 440, BF2Production: 460, BF3Production: 360, SMSConsumption: 610, DIPConsumption: 330, PCMConsumption: 240 }
    ];
  }
}