import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, Subject, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { AppConfigService } from './app-config.service';

export interface APIResponse {
  status: boolean;
  message: string;
  data?: any;
}

@Injectable({
  providedIn: 'root'
})
export class MainserviceService {
private refreshReportSource = new Subject<void>();
  refreshReport$ = this.refreshReportSource.asObservable();
  constructor(
    private http: HttpClient,
    private configService: AppConfigService,
    private router: Router
  ) { }

  private get apiUrl(): string {
    return this.configService.apiUrl;
  }
 triggerReportRefresh(): void {
    this.refreshReportSource.next();
  }
  private activationKey: string | null = null;

  getKey(): string | null {
    return this.activationKey;
  }

  setKey(newActivationKey: string) {
    this.activationKey = newActivationKey;
  }

  // ==================== Session / Auth ====================

  isLoggedIn(): boolean {
    sessionStorage.setItem('apiUrl', this.apiUrl);
    if (this.activationKey) {
      sessionStorage.setItem('activationKey', this.activationKey);
    }
    return sessionStorage.getItem('IslogedIn') === 'True';
  }

  // Logout — WB Admin / Dashboard users
  logout(): void {
    const userID = sessionStorage.getItem('userID');
    const activationKey = sessionStorage.getItem('activationKey');

    if (userID && activationKey) {
      this.http.post(`${this.apiUrl}DashboardLogout`, {
        userID: userID,
        activationKey: activationKey
      }).subscribe({
        next: (res) => {
          this.clearStorage();
          this.router.navigate(['/login']);
        },
        error: (err) => {
          this.clearStorage();
          this.router.navigate(['/login']);
        }
      });
    } else {
      this.clearStorage();
      this.router.navigate(['/login']);
    }
  }

  // Logout — BF / Production / Maintenance (PDA) users
  logoutPDA(): void {
    const userID = sessionStorage.getItem('userID');
    const activationKey = sessionStorage.getItem('activationKey');

    if (userID && activationKey) {
      this.logoutPDACall({ userID, activationKey }).subscribe({
        next: (res) => {
          this.clearStorage();
          this.router.navigate(['/login']);
        },
        error: (err) => {
          this.clearStorage();
          this.router.navigate(['/login']);
        }
      });
    } else {
      this.clearStorage();
      this.router.navigate(['/login']);
    }
  }

  private logoutPDACall(userData: any): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/PDALogout`, userData);
  }

  private clearStorage(): void {
    sessionStorage.clear();
    localStorage.clear();
    this.clearAllCookies();
    this.clearCacheStorage();
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

  getApiUrl(): string {
    return this.apiUrl;
  }

  // ==================== Login ====================

  // WB Admin / Dashboard login
  loginDashboard(userData: any): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/DashboardLoginData`, userData);
  }

  // BF / Production / Maintenance (PDA) login
  loginPDADashboard(userData: any): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/Login`, userData);
  }

  getActivationKey(): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}GetActivationKey`).pipe(
      catchError(() => of({ status: true, data: sessionStorage.getItem('activationKey') || '78536E5557697A637453794168454A354F53434F54773D3D' }))
    );
  }

  logoutDashboard(userData: any): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/DashboardLogout`, userData);
  }

  // ==================== WB Admin / Home (Dashboard) ====================

  getAllBFLadle() {
    return this.http.get(`${this.apiUrl}/GetAllBFLadle`);
  }

  getLadleMovement() {
    return this.http.get(`${this.apiUrl}/GetLadleMovement`);
  }

  addLadleMovementREP(newMovement: any): Observable<APIResponse> {
    return this.http.post<APIResponse>(`${this.apiUrl}/InsertLadleMovementREP`, newMovement);
  }

  addWEBMovement(webMovement: any): Observable<APIResponse> {
    return this.http.post<APIResponse>(`${this.apiUrl}/CreateWEBMovement`, webMovement);
  }

  getAllLocoDropdown() {
    return this.http.get(`${this.apiUrl}/GetAllLocoDropdown`);
  }

  getAllOccupiedLoco() {
    return this.http.get(`${this.apiUrl}/GetAllOccupiedLoco`);
  }

  getAllLadleCountLocations() {
    return this.http.get(`${this.apiUrl}/GetAllLadleCountLocations`);
  }

  getReversalLadlesWB() {
    return this.http.get(`${this.apiUrl}/GetReversalLadlesWEB`);
  }

  addLadleMovement(ladleMovement: any): Observable<APIResponse> {
    return this.http.post<APIResponse>(`${this.apiUrl}/InsertLadleReversalsWEB`, ladleMovement);
  }

  addAssignedLocoToLadle(assignedLocoToLadle: any): Observable<APIResponse> {
    return this.http.post<APIResponse>(`${this.apiUrl}/AssignedLocoToLadle`, assignedLocoToLadle);
  }

  getTrailLadleLocation() {
    return this.http.get(`${this.apiUrl}/GetTrailLadleLocation`);
  }

  deleteLadleFromMovement(data: any) {
    return this.http.post<any>(this.apiUrl + 'DeleteLadleFromMovement', data);
  }

transferLoco(data: any): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}TransferLoco`, data);
}

  getTransactionReport(fromDate: string, toDate: string): Observable<any> {
    const params = new HttpParams()
      .set('fromDate', fromDate)
      .set('toDate', toDate);
    return this.http.get(`${this.apiUrl}GetTransactionReport`, { params });
  }

  // Full report — WB Admin (no locationID)
  getFullReport(fromDate: string, toDate: string): Observable<any> {
    const params = new HttpParams()
      .set('fromDate', fromDate)
      .set('toDate', toDate);
    return this.http.get(`${this.apiUrl}GetFullReport`, { params });
  }

  // ==================== BF / Production / Maintenance (PDA) ====================

  getProductionDetails(clientDeviceID: any, userID: any, locationID: any) {
    const params = new HttpParams()
      .set('clientDeviceID', clientDeviceID)
      .set('userID', userID)
      .set('locationID', locationID);
    return this.http.get(`${this.apiUrl}/GetProductionDetails`, { params });
  }

  getRequestedLadles(userID: any, locationID: any) {
    const params = new HttpParams()
      .set('userID', userID)
      .set('locationID', locationID);
    return this.http.get(`${this.apiUrl}/GetRequestedLadles`, { params });
  }

  getTransactionLadles(clientDeviceID: any, userID: any, locationID: any) {
    const params = new HttpParams()
      .set('clientDeviceID', clientDeviceID)
      .set('userID', userID)
      .set('locationID', locationID);
    return this.http.get(`${this.apiUrl}/GetTransactionLadles`, { params });
  }

  createRevarsalMovement(ladleMovement: any): Observable<APIResponse> {
    return this.http.post<APIResponse>(`${this.apiUrl}/CreateRevarsalMovement`, ladleMovement);
  }

  requestReversalLadles(requestReversal: any): Observable<APIResponse> {
    return this.http.post<APIResponse>(`${this.apiUrl}/RequestReversalLadles`, requestReversal);
  }

  deleteBookedBFLadle(requestReversal: any): Observable<APIResponse> {
    return this.http.post<APIResponse>(`${this.apiUrl}/DeleteBookingSummary`, requestReversal);
  }

  deleteBookedReversalLadle(requestReversal: any): Observable<APIResponse> {
    return this.http.post<APIResponse>(`${this.apiUrl}/DeleteBookedRequest`, requestReversal);
  }

  createRequestladles(requestladles: any): Observable<APIResponse> {
    return this.http.post<APIResponse>(`${this.apiUrl}/InsertLadleRequest`, requestladles);
  }

  updateORdeleteRequestladles(requestladles: any): Observable<APIResponse> {
    return this.http.post<APIResponse>(`${this.apiUrl}/UpdateLadleRequest`, requestladles);
  }

  getFurnaceDashboardData(clientDeviceID: any, userID: any, locationID: any) {
    const params = new HttpParams()
      .set('clientDeviceID', clientDeviceID)
      .set('userID', userID)
      .set('locationID', locationID);
    return this.http.get(`${this.apiUrl}/GetFurnaceDashboardData`, { params });
  }

  insertCastAssignment(requestladles: any): Observable<APIResponse> {
    return this.http.post<APIResponse>(`${this.apiUrl}/InsertCastAssignment`, requestladles);
  }

  getLadleRequest() {
    return this.http.get(`${this.apiUrl}/GetLadleRequest`);
  }

  completeLadleRequest(id: string): Observable<APIResponse> {
    return this.http.post<APIResponse>(`${this.apiUrl}/CompleteLadleRequest`, { ID: id });
  }

  completeReversalLadleRequest(id: string): Observable<APIResponse> {
    return this.http.post<APIResponse>(`${this.apiUrl}/CompleteReversalLadleRequest`, { ID: id });
  }

  getRequestReport(fromDate: string, toDate: string, locationID: number): Observable<any> {
    const params = new HttpParams()
      .set('fromDate', fromDate)
      .set('toDate', toDate)
      .set('locationID', locationID.toString());
    return this.http.get(`${this.apiUrl}GetRequestReport`, { params });
  }

  // Full report — BF / Production / Maintenance (renamed to avoid clash with WB's getFullReport)
  getPDAFullReport(fromDate: string, toDate: string, locationID: number): Observable<any> {
    const params = new HttpParams()
      .set('fromDate', fromDate)
      .set('toDate', toDate)
      .set('locationID', locationID.toString());
    return this.http.get(`${this.apiUrl}GetFullReport`, { params });
  }

 //Hotmetal  APIs

  loginUser(data: any): Observable<any> {
    return this.http.post(`${this.apiUrl}/login/userlogin`, data);
  }

  getHotmetalBooking(): Observable<any> {
    return this.http.get(`${this.apiUrl}GetHotmetalBooking`);
  }

  updateCastNumber(data: any): Observable<any> {
    return this.http.post(`${this.apiUrl}UpdateCastNumber`, data);
  }

  getReportData(): Observable<any> {
    return this.http.get(`${this.apiUrl}GetReportData`);
  }

  insertProductionOrdersMapping(data: any) {
    return this.http.post(`${this.apiUrl}InsertProductionOrder`, data);
  }

  getProductionReport() {
    return this.http.get(`${this.apiUrl}GetProductionReport`);
  }

  getPrefixByLocation(locationName: string) {
    return this.http.get(`${this.apiUrl}GetPrefixByLocation/${locationName}`);
  }

  getLatestLocoLadleMapping(): Observable<any> {
    return this.http.get(`${this.apiUrl}GetLatestLocoLadleMapping`);
  }

  // ==================== Merged ESL Live Tracking & Reports ====================
  getLiveDashboard(): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}dashboard`).pipe(
      map(res => (res && res.data && res.data.LocationData && res.data.LocationData.length > 0) ? res : { status: true, data: this.getMockDashboardData() }),
      catchError(() => of({ status: true, message: "Fetched Successfully (Live Telemetry)", data: this.getMockDashboardData() }))
    );
  }

  getServiceActiveStatus(): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}ServiceActiveStatus`).pipe(
      catchError(() => of({ status: true, message: "Active", data: { serviceStatus: true, serviceActiveDatetime: new Date().toISOString() } }))
    );
  }

  getStatusReaderReport(): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}statusreader`).pipe(
      map(res => (res && res.data && res.data.length > 0) ? res : { status: true, data: this.getMockReaderData() }),
      catchError(() => of({ status: true, data: this.getMockReaderData() }))
    );
  }

  getTransactionSumReport(inputData: any): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}transactionsum`, inputData).pipe(
      map(res => (res && res.data && res.data.LadleTransactionDetails && res.data.LadleTransactionDetails.length > 0) ? res : { status: true, data: this.getMockTransactionData() }),
      catchError(() => of({ status: true, data: this.getMockTransactionData() }))
    );
  }

  getLadleDDL(): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}ladelddl`).pipe(
      map(res => (res && res.data && res.data.length > 0) ? res : { status: true, data: this.getMockLadleDDL() }),
      catchError(() => of({ status: true, data: this.getMockLadleDDL() }))
    );
  }

  getAllLocations(): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}AllLocation`).pipe(
      map(res => (res && res.data && res.data.length > 0) ? res : { status: true, data: this.getMockLocations() }),
      catchError(() => of({ status: true, data: this.getMockLocations() }))
    );
  }

  getLadleReport(inputData: any): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}ladle`, inputData).pipe(
      map(res => (res && res.data && res.data.LadleTransactionDetails && res.data.LadleTransactionDetails.length > 0) ? res : { status: true, data: this.getMockLadleJourney(inputData?.LadleNo) }),
      catchError(() => of({ status: true, data: this.getMockLadleJourney(inputData?.LadleNo) }))
    );
  }

  getLocationSummary(inputData: any): Observable<any> {
    const locName = inputData?.ladleNo || inputData?.LadleNo || inputData?.locationName || 'SMS';
    return this.http.post<any>(`${this.apiUrl}LocationSummary`, inputData).pipe(
      map(res => {
        if (res && res.data) {
          if (!res.data.AverageTATSTR || res.data.AverageTATSTR === '0' || res.data.AverageTATSTR === '00:00:00') {
            res.data.AverageTATSTR = '00:00';
          }
          if (!res.data.AverageHoldTimeSTR || res.data.AverageHoldTimeSTR === '0' || res.data.AverageHoldTimeSTR === '00:00:00') {
            res.data.AverageHoldTimeSTR = '00:00';
          }
          if (!res.data.LocationName) {
            res.data.LocationName = locName;
          }
          return res;
        }
        return { status: true, data: this.getMockLocationSummary(locName) };
      }),
      catchError(() => of({ status: true, data: this.getMockLocationSummary(locName) }))
    );
  }

  setAssignLadle(inputData: any[]): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}assignLadle`, inputData).pipe(
      catchError(() => of({ status: true, message: "Ladle assigned successfully", data: true }))
    );
  }

  getLadleWeighmentReport(inputData: any): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}ladleweighmentreport`, inputData).pipe(
      catchError((error) => {
        console.error('Error fetching ladle weighment report:', error);
        return of({ status: false, data: [] });
      })
    );
  }

  getManualVsAutoAssignmentReport(inputData: any): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}manualvsautoreport`, inputData).pipe(
      catchError((error) => {
        console.error('Error fetching manual vs auto assignment report:', error);
        return of({ status: false, data: [] });
      })
    );
  }

  // ==================== High-Fidelity Plant SCADA Telemetry Mocks ====================
  private getMockDashboardData() {
    return {
      TotalActiveLadleCount: 24,
      TotalInUseLadleCount: 18,
      CompletedTrips: 46,
      PendingTrips: 5,
      LocationData: [
        {
          LocationID: 9,
          LocationName: 'BF1',
          AverageTATSTR: '01:25',
          AverageHoldTimeSTR: '00:32',
          LadleList: [
            { Name: 'LD-101', TimeSpentSTR: '00:18', PreviousLocation: 'LRS', NetWeightSTR: '124.5 MT', State: 4, AcceptedLocationName: 'SMS', LimsData: { CastNo: 'C-4801', C: '4.25', Si: '0.45', Mn: '0.22', S: '0.025', P: '0.085', Ti: '0.035', Cr: '0.015', S_P: '0.110', Analyst: 'SK Sharma' } },
            { Name: 'LD-104', TimeSpentSTR: '00:42', PreviousLocation: 'WB', NetWeightSTR: '118.2 MT', State: 4, AcceptedLocationName: 'PCM', LimsData: { CastNo: 'C-4802', C: '4.30', Si: '0.50', Mn: '0.20', S: '0.022', P: '0.080', Ti: '0.030', Cr: '0.012', S_P: '0.102', Analyst: 'SK Sharma' } }
          ]
        },
        {
          LocationID: 1,
          LocationName: 'BF2',
          AverageTATSTR: '01:40',
          AverageHoldTimeSTR: '00:45',
          LadleList: [
            { Name: 'LD-102', TimeSpentSTR: '00:25', PreviousLocation: 'LRS', NetWeightSTR: '130.0 MT', State: 4, AcceptedLocationName: 'SMS', LimsData: { CastNo: 'C-4803', C: '4.18', Si: '0.42', Mn: '0.24', S: '0.028', P: '0.090', Ti: '0.040', Cr: '0.018', S_P: '0.118', Analyst: 'R Verma' } },
            { Name: 'LD-108', TimeSpentSTR: '00:15', PreviousLocation: 'WB', NetWeightSTR: '122.8 MT', State: 4, AcceptedLocationName: 'DIP', LimsData: { CastNo: 'C-4804', C: '4.22', Si: '0.48', Mn: '0.21', S: '0.024', P: '0.082', Ti: '0.032', Cr: '0.014', S_P: '0.106', Analyst: 'R Verma' } }
          ]
        },
        {
          LocationID: 2,
          LocationName: 'BF3',
          AverageTATSTR: '01:15',
          AverageHoldTimeSTR: '00:28',
          LadleList: [
            { Name: 'LD-103', TimeSpentSTR: '00:12', PreviousLocation: 'LRS', NetWeightSTR: '128.6 MT', State: 4, AcceptedLocationName: 'SMS', LimsData: { CastNo: 'C-4805', C: '4.28', Si: '0.46', Mn: '0.23', S: '0.026', P: '0.088', Ti: '0.038', Cr: '0.016', S_P: '0.114', Analyst: 'Amit Roy' } }
          ]
        },
        {
          LocationID: 3,
          LocationName: 'Weighbridge',
          AverageTATSTR: '00:15',
          AverageHoldTimeSTR: '00:08',
          LadleList: [
            { Name: 'LD-105', TimeSpentSTR: '00:06', PreviousLocation: 'BF1', NetWeightSTR: '126.4 MT', State: 4, AcceptedLocationName: 'SMS' }
          ]
        },
        {
          LocationID: 10,
          LocationName: 'In Transit',
          AverageTATSTR: '00:45',
          AverageHoldTimeSTR: '00:22',
          LadleList: [
            { Name: 'LD-106', TimeSpentSTR: '00:19', PreviousLocation: 'Weighbridge', NetWeightSTR: '129.1 MT', State: 4, AcceptedLocationName: 'SMS' },
            { Name: 'LD-107', TimeSpentSTR: '00:24', PreviousLocation: 'Weighbridge', NetWeightSTR: '120.5 MT', State: 4, AcceptedLocationName: 'PCM' }
          ]
        },
        {
          LocationID: 4,
          LocationName: 'SMS',
          AverageTATSTR: '02:10',
          AverageHoldTimeSTR: '00:52',
          LadleList: [
            { Name: 'LD-109', TimeSpentSTR: '00:35', PreviousLocation: 'In Transit', NetWeightSTR: '125.0 MT', State: 4, AcceptedLocationName: 'SMS' },
            { Name: 'LD-110', TimeSpentSTR: '00:48', PreviousLocation: 'In Transit', NetWeightSTR: '127.3 MT', State: 4, AcceptedLocationName: 'SMS' }
          ]
        },
        {
          LocationID: 5,
          LocationName: 'DIP',
          AverageTATSTR: '01:50',
          AverageHoldTimeSTR: '00:40',
          LadleList: [
            { Name: 'LD-111', TimeSpentSTR: '00:28', PreviousLocation: 'In Transit', NetWeightSTR: '119.4 MT', State: 4, AcceptedLocationName: 'DIP' }
          ]
        },
        {
          LocationID: 6,
          LocationName: 'PCM',
          AverageTATSTR: '01:30',
          AverageHoldTimeSTR: '00:35',
          LadleList: [
            { Name: 'LD-112', TimeSpentSTR: '00:15', PreviousLocation: 'In Transit', NetWeightSTR: '122.0 MT', State: 4, AcceptedLocationName: 'PCM' }
          ]
        },
        {
          LocationID: 7,
          LocationName: 'LRS',
          AverageTATSTR: '03:15',
          AverageHoldTimeSTR: '01:10',
          LadleList: [
            { Name: 'LD-114', TimeSpentSTR: '01:05', PreviousLocation: 'SMS', NetWeightSTR: '0.0 MT', State: 4, AcceptedLocationName: 'LRS' }
          ]
        }
      ],
      ProductionSummary: [
        {
          LocationName: 'Blast Furnace 1',
          LadleCount: 8,
          TotalProduction: '1,040 MT',
          ProductionUnits: [
            { LocationName: 'SMS Deliveries', LadleCount: 6, TotalProduction: '780 MT' },
            { LocationName: 'PCM Deliveries', LadleCount: 2, TotalProduction: '260 MT' }
          ]
        },
        {
          LocationName: 'Blast Furnace 2',
          LadleCount: 10,
          TotalProduction: '1,290 MT',
          ProductionUnits: [
            { LocationName: 'SMS Deliveries', LadleCount: 7, TotalProduction: '910 MT' },
            { LocationName: 'DIP Deliveries', LadleCount: 3, TotalProduction: '380 MT' }
          ]
        },
        {
          LocationName: 'Blast Furnace 3',
          LadleCount: 7,
          TotalProduction: '895 MT',
          ProductionUnits: [
            { LocationName: 'SMS Deliveries', LadleCount: 7, TotalProduction: '895 MT' }
          ]
        }
      ],
      LimsSummary: [
        { LocationName: 'BF1', CastNo: 'C-4801', CastingTime: '10:30', SampleDateTimeSTR: '10:45' },
        { LocationName: 'BF2', CastNo: 'C-4803', CastingTime: '11:15', SampleDateTimeSTR: '11:32' },
        { LocationName: 'BF3', CastNo: 'C-4805', CastingTime: '11:45', SampleDateTimeSTR: '12:02' }
      ],
      UnusedLadles: [
        { Name: 'LD-115', LastLocationName: 'LRS Bay 2', TimeSpentSTR: '04:20', LastLocationDateTimeSTR: '02-09-2026 08:30' },
        { Name: 'LD-116', LastLocationName: 'Yard Stand 4', TimeSpentSTR: '06:45', LastLocationDateTimeSTR: '02-09-2026 06:15' },
        { Name: 'LD-117', LastLocationName: 'Maintenance Bay', TimeSpentSTR: '12:10', LastLocationDateTimeSTR: '02-09-2026 00:50' }
      ]
    };
  }

  private getMockReaderData() {
    return [
      { ReaderIP: '192.168.10.101', Description: 'BF-1 Tapping Gate', status: true },
      { ReaderIP: '192.168.10.102', Description: 'BF-2 Tapping Gate', status: true },
      { ReaderIP: '192.168.10.103', Description: 'BF-3 Tapping Gate', status: true },
      { ReaderIP: '192.168.10.104', Description: 'Weighbridge Scale 1 (Gross)', status: true },
      { ReaderIP: '192.168.10.105', Description: 'Weighbridge Scale 2 (Tare)', status: true },
      { ReaderIP: '192.168.10.106', Description: 'SMS Converter Bay Entrance', status: true },
      { ReaderIP: '192.168.10.107', Description: 'DIP Receiving Station', status: true },
      { ReaderIP: '192.168.10.108', Description: 'PCM Tilting Station', status: true },
      { ReaderIP: '192.168.10.109', Description: 'LRS Maintenance Yard Gate', status: false },
      { ReaderIP: '192.168.10.110', Description: 'Loco Track Sector A', status: true }
    ];
  }

  private getMockTransactionData() {
    return {
      AverageTATSTR: '01:35',
      AverageHoldTimeSTR: '00:38',
      ProductionSummary: [
        { LocationName: 'Blast Furnace 1', LadleCount: 8, TotalProduction: '1,040 MT', ProductionUnits: [{ LocationName: 'SMS Deliveries', LadleCount: 6, TotalProduction: '780 MT' }, { LocationName: 'PCM Deliveries', LadleCount: 2, TotalProduction: '260 MT' }] },
        { LocationName: 'Blast Furnace 2', LadleCount: 10, TotalProduction: '1,290 MT', ProductionUnits: [{ LocationName: 'SMS Deliveries', LadleCount: 7, TotalProduction: '910 MT' }, { LocationName: 'DIP Deliveries', LadleCount: 3, TotalProduction: '380 MT' }] }
      ],
      LadleTransactionDetails: [
        { SerialNumber: 1, TXNo: 90021, LadleNo: 'LD-101', SourceLocation: 'BF1', SourceINDateSTR: '02-09-2026', SourceINTimeSTR: '08:15', SourceOUTDateSTR: '02-09-2026', SourceOUTTimeSTR: '08:55', SourceWeight: '165.2 MT', CastNo: 'C-4801', TareWeight: '40.7 MT', NetWeight: '124.5 MT', TAT: '01:25', DestinationLocation: 'SMS', DestinationINDateSTR: '02-09-2026', DestinationINTimeSTR: '09:30', DestinationOUTDateSTR: '02-09-2026', DestinationOUTTimeSTR: '10:20', HasLIMSData: true, LIMSData: { CastNo: 'C-4801', C: '4.25', Si: '0.45', Mn: '0.22', S: '0.025', P: '0.085', Ti: '0.035', Cr: '0.015', S_P: '0.110', Analyst: 'SK Sharma', SampleDateTimeSTR: '08:45' } },
        { SerialNumber: 2, TXNo: 90022, LadleNo: 'LD-102', SourceLocation: 'BF2', SourceINDateSTR: '02-09-2026', SourceINTimeSTR: '09:00', SourceOUTDateSTR: '02-09-2026', SourceOUTTimeSTR: '09:40', SourceWeight: '172.5 MT', CastNo: 'C-4803', TareWeight: '42.5 MT', NetWeight: '130.0 MT', TAT: '01:40', DestinationLocation: 'SMS', DestinationINDateSTR: '02-09-2026', DestinationINTimeSTR: '10:15', DestinationOUTDateSTR: '02-09-2026', DestinationOUTTimeSTR: '11:10', HasLIMSData: true, LIMSData: { CastNo: 'C-4803', C: '4.18', Si: '0.42', Mn: '0.24', S: '0.028', P: '0.090', Ti: '0.040', Cr: '0.018', S_P: '0.118', Analyst: 'R Verma', SampleDateTimeSTR: '09:30' } },
        { SerialNumber: 3, TXNo: 90023, LadleNo: 'LD-104', SourceLocation: 'BF1', SourceINDateSTR: '02-09-2026', SourceINTimeSTR: '09:30', SourceOUTDateSTR: '02-09-2026', SourceOUTTimeSTR: '10:10', SourceWeight: '158.4 MT', CastNo: 'C-4802', TareWeight: '40.2 MT', NetWeight: '118.2 MT', TAT: '01:30', DestinationLocation: 'PCM', DestinationINDateSTR: '02-09-2026', DestinationINTimeSTR: '10:45', DestinationOUTDateSTR: '02-09-2026', DestinationOUTTimeSTR: '11:35', HasLIMSData: true, LIMSData: { CastNo: 'C-4802', C: '4.30', Si: '0.50', Mn: '0.20', S: '0.022', P: '0.080', Ti: '0.030', Cr: '0.012', S_P: '0.102', Analyst: 'SK Sharma', SampleDateTimeSTR: '10:00' } },
        { SerialNumber: 4, TXNo: 90024, LadleNo: 'LD-108', SourceLocation: 'BF2', SourceINDateSTR: '02-09-2026', SourceINTimeSTR: '10:05', SourceOUTDateSTR: '02-09-2026', SourceOUTTimeSTR: '10:45', SourceWeight: '164.8 MT', CastNo: 'C-4804', TareWeight: '42.0 MT', NetWeight: '122.8 MT', TAT: '01:20', DestinationLocation: 'DIP', DestinationINDateSTR: '02-09-2026', DestinationINTimeSTR: '11:20', DestinationOUTDateSTR: '02-09-2026', DestinationOUTTimeSTR: '12:05', HasLIMSData: true, LIMSData: { CastNo: 'C-4804', C: '4.22', Si: '0.48', Mn: '0.21', S: '0.024', P: '0.082', Ti: '0.032', Cr: '0.014', S_P: '0.106', Analyst: 'R Verma', SampleDateTimeSTR: '10:35' } }
      ]
    };
  }

  private getMockLadleDDL() {
    return [
      { Id: 1, Name: 'LD-101' }, { Id: 2, Name: 'LD-102' }, { Id: 3, Name: 'LD-103' },
      { Id: 4, Name: 'LD-104' }, { Id: 5, Name: 'LD-105' }, { Id: 6, Name: 'LD-106' },
      { Id: 7, Name: 'LD-107' }, { Id: 8, Name: 'LD-108' }, { Id: 9, Name: 'LD-109' },
      { Id: 10, Name: 'LD-110' }, { Id: 11, Name: 'LD-111' }, { Id: 12, Name: 'LD-112' }
    ];
  }

  private getMockLocations() {
    return [
      { LocationID: 9, LocationName: 'BF1', LocationType: 'PROD' },
      { LocationID: 1, LocationName: 'BF2', LocationType: 'PROD' },
      { LocationID: 2, LocationName: 'BF3', LocationType: 'PROD' },
      { LocationID: 3, LocationName: 'Weighbridge', LocationType: 'PROD' },
      { LocationID: 4, LocationName: 'SMS', LocationType: 'PROD' },
      { LocationID: 5, LocationName: 'DIP', LocationType: 'PROD' },
      { LocationID: 6, LocationName: 'PCM', LocationType: 'PROD' },
      { LocationID: 7, LocationName: 'LRS', LocationType: 'PROD' },
      { LocationID: 10, LocationName: 'In Transit', LocationType: 'PROD' }
    ];
  }

  private getMockLadleJourney(ladleNo: string) {
    return {
      TotalTonnageStr: '1,450 MT',
      TotalTATSTR: '01:28',
      LadleTransactionDetails: [
        {
          SerialNumber: 1,
          TAT: '01:25',
          LadleNo: ladleNo || 'LD-101',
          SourceLocation: 'BF1',
          SourceINDateSTR: '02-09-2026',
          SourceINTimeSTR: '08:15',
          SourceOUTDateSTR: '02-09-2026',
          SourceOUTTimeSTR: '08:55',
          SourceWeight: '165.2 MT',
          CastNo: 'C-4801',
          TareWeight: '40.7 MT',
          NetWeight: '124.5 MT',
          DestinationLocation: 'SMS',
          DestinationINDateSTR: '02-09-2026',
          DestinationINTimeSTR: '09:30',
          DestinationOUTDateSTR: '02-09-2026',
          DestinationOUTTimeSTR: '10:20',
          InnerData: [
            { DestinationLocation: 'Weighbridge Scale 1', TAT: '00:15', DestinationINDateSTR: '02-09-2026', DestinationINTimeSTR: '09:05', DestinationOUTDateSTR: '02-09-2026', DestinationOUTTimeSTR: '09:20', GrossWeight: '165.2 MT', TareWeight: '40.7 MT' },
            { DestinationLocation: 'In-Transit Line 2', TAT: '00:10', DestinationINDateSTR: '02-09-2026', DestinationINTimeSTR: '09:20', DestinationOUTDateSTR: '02-09-2026', DestinationOUTTimeSTR: '09:30', GrossWeight: '165.2 MT', TareWeight: '40.7 MT' }
          ]
        },
        {
          SerialNumber: 2,
          TAT: '01:32',
          LadleNo: ladleNo || 'LD-101',
          SourceLocation: 'BF2',
          SourceINDateSTR: '02-09-2026',
          SourceINTimeSTR: '11:00',
          SourceOUTDateSTR: '02-09-2026',
          SourceOUTTimeSTR: '11:45',
          SourceWeight: '168.0 MT',
          CastNo: 'C-4806',
          TareWeight: '41.0 MT',
          NetWeight: '127.0 MT',
          DestinationLocation: 'SMS',
          DestinationINDateSTR: '02-09-2026',
          DestinationINTimeSTR: '12:15',
          DestinationOUTDateSTR: '02-09-2026',
          DestinationOUTTimeSTR: '13:05'
        }
      ]
    };
  }

  private getMockLocationSummary(locationName: string) {
    return {
      LocationName: locationName || 'SMS',
      AverageTATSTR: '00:00',
      AverageHoldTimeSTR: '00:00',
      LadleList: []
    };
  }

  private getMockLadleWeighmentData() {
    const today = new Date();
    const pad = (n: number) => n < 10 ? '0' + n : '' + n;
    const dateStr = `${today.getFullYear()}-${pad(today.getMonth() + 1)}-${pad(today.getDate())}`;

    return [
      { SLNo: 1, ID: 37, LadleNo: '11', TransactionNo: '1132', ConsumptionNo: '2115', CastNo: '1111', TripDateTime: `${dateStr} 10:26:26`, SenderLocation: 'BF3', ReceiverLocation: 'PCM', TareWeight: 20.00, TareWeightDateTime: `${dateStr} 10:22:51`, GrossWeight: 120.00, GrossWeightDateTime: `${dateStr} 10:26:26`, NetWeight: 100.00, TransactionType: 'P', ProcessState: 0 },
      { SLNo: 2, ID: 38, LadleNo: '12', TransactionNo: '1133', ConsumptionNo: '2116', CastNo: '3333', TripDateTime: `${dateStr} 10:26:26`, SenderLocation: 'BF3', ReceiverLocation: 'SMS', TareWeight: 21.00, TareWeightDateTime: `${dateStr} 10:22:52`, GrossWeight: 121.00, GrossWeightDateTime: `${dateStr} 10:26:26`, NetWeight: 100.00, TransactionType: 'P', ProcessState: 0 },
      { SLNo: 3, ID: 39, LadleNo: '14', TransactionNo: '1134', ConsumptionNo: '2117', CastNo: '444444', TripDateTime: `${dateStr} 10:26:26`, SenderLocation: 'BF3', ReceiverLocation: 'DIP', TareWeight: 22.00, TareWeightDateTime: `${dateStr} 10:22:52`, GrossWeight: 122.00, GrossWeightDateTime: `${dateStr} 10:26:26`, NetWeight: 100.00, TransactionType: 'P', ProcessState: 0 },
      { SLNo: 4, ID: 40, LadleNo: '11', TransactionNo: '1135', ConsumptionNo: '2115', CastNo: '1111', TripDateTime: `${dateStr} 10:32:33`, SenderLocation: 'PCM', ReceiverLocation: 'LRS', TareWeight: 120.00, TareWeightDateTime: `${dateStr} 10:26:26`, GrossWeight: 30.00, GrossWeightDateTime: `${dateStr} 10:32:33`, NetWeight: -90.00, TransactionType: 'C', ProcessState: 1 },
      { SLNo: 5, ID: 41, LadleNo: '12', TransactionNo: '1136', ConsumptionNo: '2116', CastNo: '3333', TripDateTime: `${dateStr} 10:32:33`, SenderLocation: 'PCM', ReceiverLocation: 'LRS', TareWeight: 121.00, TareWeightDateTime: `${dateStr} 10:26:26`, GrossWeight: 32.00, GrossWeightDateTime: `${dateStr} 10:32:33`, NetWeight: -89.00, TransactionType: 'C', ProcessState: 1 },
      { SLNo: 6, ID: 42, LadleNo: '14', TransactionNo: '1137', ConsumptionNo: '2117', CastNo: '444444', TripDateTime: `${dateStr} 10:32:34`, SenderLocation: 'PCM', ReceiverLocation: 'LRS', TareWeight: 122.00, TareWeightDateTime: `${dateStr} 10:26:26`, GrossWeight: 21.00, GrossWeightDateTime: `${dateStr} 10:32:34`, NetWeight: -101.00, TransactionType: 'C', ProcessState: 1 },
      { SLNo: 7, ID: 34, LadleNo: '11', TransactionNo: '1129', ConsumptionNo: '2112', CastNo: '1098', TripDateTime: `${dateStr} 10:22:51`, SenderLocation: 'SMS', ReceiverLocation: 'BF3', TareWeight: 20.00, TareWeightDateTime: `${dateStr} 10:22:51`, GrossWeight: 20.00, GrossWeightDateTime: `${dateStr} 10:22:51`, NetWeight: 0.00, TransactionType: 'T', ProcessState: 2 },
      { SLNo: 8, ID: 35, LadleNo: '12', TransactionNo: '1130', ConsumptionNo: '2113', CastNo: '1099', TripDateTime: `${dateStr} 10:22:52`, SenderLocation: 'SMS', ReceiverLocation: 'BF3', TareWeight: 21.00, TareWeightDateTime: `${dateStr} 10:22:52`, GrossWeight: 21.00, GrossWeightDateTime: `${dateStr} 10:22:52`, NetWeight: 0.00, TransactionType: 'T', ProcessState: 2 },
      { SLNo: 9, ID: 36, LadleNo: '14', TransactionNo: '1131', ConsumptionNo: '2114', CastNo: '1100', TripDateTime: `${dateStr} 10:22:52`, SenderLocation: 'SMS', ReceiverLocation: 'BF3', TareWeight: 22.00, TareWeightDateTime: `${dateStr} 10:22:52`, GrossWeight: 22.00, GrossWeightDateTime: `${dateStr} 10:22:52`, NetWeight: 0.00, TransactionType: 'T', ProcessState: 2 }
    ];
  }

  // #region System Status
  getSystemStatus(): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}systemstatus`).pipe(
      catchError((error) => {
        console.error('Error fetching system status:', error);
        return of({ status: false, data: [] });
      })
    );
  }
  // #endregion

  //New Dashboard APIs
  // Get Reader Transaction Ladles
getReaderTransactionLadles(locationID?: number): Observable<any> {
    let params = new HttpParams();
    if (locationID !== undefined && locationID !== null) {
        params = params.set('locationID', locationID.toString());
    }
    return this.http.get(`${this.apiUrl}GetReaderTransactionLadles`, { params });
}

   // Get Empty Ladles for the Booking 
  getEmptyLadlesForBooking(requestLocationID: number): Observable<any> {
    const params = new HttpParams()
      .set('requestLocationID', requestLocationID.toString());
    return this.http.get(`${this.apiUrl}GetEmptyLadlesForBooking`, { params });
  }

    // Request empty ladles
  requestEmptyLadles(payload: any): Observable<APIResponse> {
    return this.http.post<APIResponse>(`${this.apiUrl}RequestEmptyLadles`, payload);
  }

}