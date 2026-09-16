import { Component, OnInit, HostListener } from '@angular/core';
import { Router, NavigationEnd } from '@angular/router';
import { DevtoolsProtectionService } from './pages/service/devtools-protection.service';
import { LayoutService } from './pages/service/layout.service';
import { AutoLogoutService } from './pages/service/auto-logout.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  standalone: false,
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  title = 'ladleapproval';
  showNav = true;

  constructor(
    private devtoolsProtection: DevtoolsProtectionService,
    private autoLogout: AutoLogoutService,
    private router: Router,
    public layoutService: LayoutService
  ) { }


  @HostListener('document:contextmenu', ['$event'])
  onRightClick(event: MouseEvent): void {
    event.preventDefault();
  }

  ngOnInit(): void {
    this.router.events.subscribe(event => {
      if (event instanceof NavigationEnd) {
        this.showNav = !event.urlAfterRedirects.includes('/login');
      }
    });
  }
}