import { Component, OnDestroy, OnInit } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter, Subject, takeUntil } from 'rxjs';
import Swal from 'sweetalert2';

import { MainserviceService } from '../service/mainservice.service';
import { SessionTimeoutService } from '../service/session-timeout.service';
import { LayoutService } from '../service/layout.service';
import { ThemeService } from '../service/theme.service';

@Component({
  selector: 'app-navbar',
  standalone: false,
  templateUrl: './navbar.component.html',
  styleUrl: './navbar.component.css'
})
export class NavbarComponent implements OnInit, OnDestroy {

  isSidebarOpen = true;
  activeMenu = 'dashboard'; 
  isDarkMode = false;
  userLocationId = 0;

  showLadleRequestSubmenu = false;
  showEslReportsSubmenu = false;
  selectedAdminLocationId: number | null = null;

  private readonly destroy$ = new Subject<void>();

  private readonly BLAST_FURNACE_LOCATIONS = [1, 2, 9];
  private readonly PRODUCTION_LOCATIONS = [4, 5, 6];
  private readonly MAINTENANCE_LOCATIONS = [7];
  private readonly HOME_LOCATION = 3;

  constructor(
    private readonly router: Router,
    private readonly mainService: MainserviceService,
    private readonly sessionTimeoutService: SessionTimeoutService,
    private readonly layoutService: LayoutService,
    public readonly themeService: ThemeService
  ) {}

  ngOnInit(): void {
    this.loadUserLocation();

    this.isDarkMode = this.themeService.isDark;

    this.loadSelectedAdminLocation();

    this.updateActiveMenu(this.router.url);

    this.router.events
      .pipe(
        filter(
          (event): event is NavigationEnd =>
            event instanceof NavigationEnd
        ),
        takeUntil(this.destroy$)
      )
      .subscribe((event: NavigationEnd) => {
        this.updateActiveMenu(event.urlAfterRedirects);
        this.loadUserLocation();
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.sessionTimeoutService.cleanup();
  }

  get isWBUser(): boolean {
    return this.userLocationId === this.HOME_LOCATION;
  }

  get isLadleRequestDisabled(): boolean {
    return this.isWBUser;
  }

  get isLadleMovementDisabled(): boolean {
    return !this.isWBUser;
  }

  selectMenu(menu: string): void {
    this.loadUserLocation();

    if (
      menu === 'ladleRequest' &&
      this.isLadleRequestDisabled
    ) {
      this.toggleLadleRequestMenu();
      return;
    }

    if (
      menu === 'ladleMovement' &&
      this.isLadleMovementDisabled
    ) {
      return;
    }

    // Auto-close Ladle Request submenu if clicking any other item
    if (menu !== 'ladleRequest') {
      this.showLadleRequestSubmenu = false;
    }

    // Auto-close ESL Reports submenu if clicking any item outside ESL Reports
    const eslReports = ['transactionsum', 'ladlereport', 'locationsummary', 'departmentreport', 'ladleweighmentreport', 'slagreport', 'manualvsautoreport', 'userloginreport'];
    if (!eslReports.includes(menu) && menu !== 'eslReports') {
      this.showEslReportsSubmenu = false;
    }

    this.activeMenu = menu;

    switch (menu) {

      case 'dashboard':
        this.navigate('/dashboard');
        break;

      case 'esldashboard':
        this.navigate('/esldashboard');
        break;

      case 'home':
        this.openHome();
        break;

      case 'ladleRequest':
        this.openLadleRequest();
        break;

      case 'ladleMovement':
        this.openLadleMovement();
        break;

      case 'map':
        this.navigate('/map');
        break;

      case 'pendingRequest':
        this.navigate('/pendingRequest');
        break;

      case 'completedRequest':
        this.navigate('/completedRequest');
        break;

      case 'castAssignment':
        this.navigate('/castAssignment');
        break;

      case 'productionOrderMapping':
        this.navigate('/productionOrderMapping');
        break;

      case 'locoLadleMovement':
        this.navigate('/locoLadleMovement');
        break;

      case 'readerstatus':
        this.navigate('/readerstatus');
        break;

      case 'transactionsum':
        this.navigate('/transactionsum');
        break;

      case 'ladlereport':
        this.navigate('/ladlereport');
        break;

      case 'locationsummary':
        this.navigate('/locationsummary');
        break;

      case 'departmentreport':
        this.navigate('/departmentreport');
        break;

      case 'ladleweighmentreport':
        this.navigate('/ladleweighmentreport');
        break;

      case 'slagreport':
        this.navigate('/slagreport');
        break;

      case 'manualvsautoreport':
        this.navigate('/manualvsautoreport');
        break;

      case 'userloginreport':
        this.navigate('/userloginreport');
        break;

      case 'systemstatus':
        this.navigate('/systemstatus');
        break;

      default:
        break;
    }
  }

  toggleLadleRequestMenu(): void {
    if (!this.isWBUser) {
      this.openLadleRequest();
      return;
    }

    this.showEslReportsSubmenu = false;
    this.activeMenu = 'ladleRequest';
    this.showLadleRequestSubmenu = !this.showLadleRequestSubmenu;
  }

  toggleEslReportsMenu(): void {
    this.showLadleRequestSubmenu = false;
    this.showEslReportsSubmenu = !this.showEslReportsSubmenu;
  }

  selectAdminLocation(
    locationId: number,
    locationName: string
  ): void {

    if (!this.isWBUser) {
      return;
    }

    this.showEslReportsSubmenu = false;
    this.selectedAdminLocationId = locationId;

    sessionStorage.setItem(
      'selectedLocationId',
      locationId.toString()
    );

    sessionStorage.setItem(
      'selectedLocationName',
      locationName
    );

    this.activeMenu = 'ladleRequest';

    if (this.BLAST_FURNACE_LOCATIONS.includes(locationId)) {
      this.navigate('/blastFurnace');
      return;
    }

    if (this.PRODUCTION_LOCATIONS.includes(locationId)) {
      this.navigate('/production');
      return;
    }

    if (this.MAINTENANCE_LOCATIONS.includes(locationId)) {
      this.navigate('/maintenance');
      return;
    }
  }

  private openHome(): void {
    this.loadUserLocation();

    if (this.isWBUser) {
      this.navigate('/home');
      return;
    }

    Swal.fire({
      icon: 'error',
      title: 'Access Denied',
      text: 'Home is available only for the WB user.'
    });
  }

  private openLadleRequest(): void {
    this.loadUserLocation();

    if (this.isWBUser) {
      this.toggleLadleRequestMenu();
      return;
    }

    if (
      this.BLAST_FURNACE_LOCATIONS.includes(
        this.userLocationId
      )
    ) {
      this.navigate('/blastFurnace');
      return;
    }

    if (
      this.PRODUCTION_LOCATIONS.includes(
        this.userLocationId
      )
    ) {
      this.navigate('/production');
      return;
    }

    if (
      this.MAINTENANCE_LOCATIONS.includes(
        this.userLocationId
      )
    ) {
      this.navigate('/maintenance');
      return;
    }

    Swal.fire({
      icon: 'error',
      title: 'Access Denied',
      text: 'Your location is not configured.'
    });
  }

  private openLadleMovement(): void {
    this.loadUserLocation();

    if (this.isWBUser) {
      this.navigate('/home');
    }
  }

  private updateActiveMenu(url: string): void {

    if (url.includes('/dashboard')) {
      this.activeMenu = 'dashboard';
      this.showLadleRequestSubmenu = false;
      this.showEslReportsSubmenu = false;
      return;
    }

    if (url.includes('/esldashboard')) {
      this.activeMenu = 'esldashboard';
      this.showLadleRequestSubmenu = false;
      this.showEslReportsSubmenu = false;
      return;
    }

    if (url.includes('/home')) {
      this.activeMenu = 'home';
      this.showLadleRequestSubmenu = false;
      this.showEslReportsSubmenu = false;
      return;
    }

    if (url.includes('/castAssignment')) {
      this.activeMenu = 'castAssignment';
      this.showLadleRequestSubmenu = false;
      this.showEslReportsSubmenu = false;
      return;
    }

    if (url.includes('/productionOrderMapping')) {
      this.activeMenu = 'productionOrderMapping';
      this.showLadleRequestSubmenu = false;
      this.showEslReportsSubmenu = false;
      return;
    }

    if (url.includes('/locoLadleMovement')) {
      this.activeMenu = 'locoLadleMovement';
      this.showLadleRequestSubmenu = false;
      this.showEslReportsSubmenu = false;
      return;
    }

    if (url.includes('/pendingRequest')) {
      this.activeMenu = 'pendingRequest';
      this.showLadleRequestSubmenu = false;
      this.showEslReportsSubmenu = false;
      return;
    }

    if (url.includes('/completedRequest')) {
      this.activeMenu = 'completedRequest';
      this.showLadleRequestSubmenu = false;
      this.showEslReportsSubmenu = false;
      return;
    }

    if (
      url.includes('/blastFurnace') ||
      url.includes('/production') ||
      url.includes('/maintenance')
    ) {
      this.activeMenu = 'ladleRequest';
      this.showEslReportsSubmenu = false;

      if (this.isWBUser) {
        this.showLadleRequestSubmenu = true;
      }

      return;
    }

    if (url.includes('/map')) {
      this.activeMenu = 'map';
      this.showLadleRequestSubmenu = false;
      this.showEslReportsSubmenu = false;
      return;
    }

    if (url.includes('/systemstatus')) {
      this.activeMenu = 'systemstatus';
      this.showLadleRequestSubmenu = false;
      this.showEslReportsSubmenu = false;
      return;
    }

    if (
      url.includes('/transactionsum') ||
      url.includes('/ladlereport') ||
      url.includes('/locationsummary') ||
      url.includes('/departmentreport') ||
      url.includes('/ladleweighmentreport') ||
      url.includes('/slagreport') ||
      url.includes('/manualvsautoreport')
    ) {
      this.showEslReportsSubmenu = true;
      this.showLadleRequestSubmenu = false;
      if (url.includes('/transactionsum')) this.activeMenu = 'transactionsum';
      if (url.includes('/ladlereport')) this.activeMenu = 'ladlereport';
      if (url.includes('/locationsummary')) this.activeMenu = 'locationsummary';
      if (url.includes('/departmentreport')) this.activeMenu = 'departmentreport';
      if (url.includes('/ladleweighmentreport')) this.activeMenu = 'ladleweighmentreport';
      if (url.includes('/slagreport')) this.activeMenu = 'slagreport';
      if (url.includes('/manualvsautoreport')) this.activeMenu = 'manualvsautoreport';
      return;
    }

    this.activeMenu = '';
    this.showLadleRequestSubmenu = false;
    this.showEslReportsSubmenu = false;
  }

  private loadSelectedAdminLocation(): void {
    const value =
      sessionStorage.getItem('selectedLocationId');

    this.selectedAdminLocationId =
      value ? Number(value) : null;
  }

  private loadUserLocation(): void {
    this.userLocationId = Number(
      sessionStorage.getItem('userLocationId') ?? 0
    );
  }

  private navigate(route: string): void {
    this.router.navigate([route]);
  }

  toggleSidebar(): void {
    this.layoutService.toggle();
    this.isSidebarOpen = this.layoutService.isOpen;
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
    this.isDarkMode = this.themeService.isDark;
  }

  logout(): void {
    Swal.fire({
      title: 'Are you sure?',
      text: 'Do you want to logout?',
      icon: 'warning',
      showCancelButton: true,
      confirmButtonText: 'Yes, logout',
      cancelButtonText: 'No, stay logged in'
    }).then((result) => {

      if (result.isConfirmed) {
        sessionStorage.removeItem('selectedLocationId');
        sessionStorage.removeItem('selectedLocationName');

        this.mainService.logout();
      }
    });
  }
}