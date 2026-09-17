import { NgModule, APP_INITIALIZER } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { ToastrModule } from 'ngx-toastr';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { LoginComponent } from './pages/login/login.component';
import { LogoutComponent } from './pages/logout/logout.component';
import { HomeComponent } from './pages/home/home.component';
import { NavbarComponent } from './pages/navbar/navbar.component';

import { RouterModule, RouterOutlet } from '@angular/router';

import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatSortModule } from '@angular/material/sort';
import { MatSelectModule } from '@angular/material/select';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';
import { MatIconModule } from '@angular/material/icon';
import { HashLocationStrategy, LocationStrategy } from '@angular/common';
import { MatDialogModule } from '@angular/material/dialog';
import { ScrollingModule } from '@angular/cdk/scrolling';
import { HTTP_INTERCEPTORS } from '@angular/common/http';
import { AuthInterceptor } from './pages/interceptors/auth.interceptor';

import { SafeHtmlPipe } from './pages/pipes/safe.pipe';
import { AppConfigService } from './pages/service/app-config.service';
import { DateRangeDialogComponent } from './pages/date-range-dialog/date-range-dialog.component';

import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';

import { DashboardComponent } from './pages/dashboard/dashboard.component';

// Standalone components
import { EslBlastFurnaceComponent } from './pages/esl-blast-furnace/esl-blast-furnace.component';
import { EslProductionComponent } from './pages/esl-production/esl-production.component';
import { EslMaintenanceComponent } from './pages/esl-maintenance/esl-maintenance.component';
import { CustomCheckboxComponent } from './pages/custom-checkbox/custom-checkbox.component';
import { CastAssignmentComponent } from './pages/cast-assignment/cast-assignment.component';
import { ProductionOrderMappingComponent } from './pages/production-order-mapping/production-order-mapping.component';
import { MapComponent } from './pages/map/map.component';
import { FooterComponent } from './pages/footer/footer.component';

import { DateAdapter, MAT_DATE_FORMATS } from '@angular/material/core';
import { CustomDateAdapter, CUSTOM_DATE_FORMATS } from './pages/service/custom-date-adapter';
import { CastAssignmentReportComponent } from './pages/cast-assignment-report/cast-assignment-report.component';
import { PendingRequestComponent } from './pages/pending-request/pending-request.component';
import { CompletedRequestComponent } from './pages/completed-request/completed-request.component';
import { ProductionreportComponent } from './pages/productionreport/productionreport.component';
import { ReportComponent } from './pages/report/report.component';

// Merged ESL Components & Dialogs
import { WaitComponent } from './pages/wait/wait.component';
import { ConfirmDialogComponent } from './pages/confirm/confirm-dialog.component';
import { BfreportComponent } from './pages/bfreport/bfreport.component';
import { SmsreportComponent } from './pages/smsreport/smsreport.component';
import { WgreportComponent } from './pages/wgreport/wgreport.component';
import { EslchannelComponent } from './pages/eslchannel/eslchannel.component';
import { EsldashboardComponent } from './pages/esldashboard/esldashboard.component';
import { ReaderstatusComponent } from './pages/readerstatus/readerstatus.component';
import { TransactionsumComponent } from './pages/transactionsum/transactionsum.component';
import { LadlereportComponent } from './pages/ladlereport/ladlereport.component';
import { LocationsummaryComponent } from './pages/locationsummary/locationsummary.component';
import { DepartmentreportComponent } from './pages/departmentreport/departmentreport.component';
import { SystemStatusComponent } from './pages/system-status/system-status.component';
import { LadleWeighmentReportComponent } from './pages/ladleweighmentreport/ladleweighmentreport.component';
import { SlagReportComponent } from './pages/slagreport/slagreport.component';
import { ManualVsAutoReportComponent } from './pages/manual-vs-auto-report/manual-vs-auto-report.component';
import { UserLoginHistoryReportComponent } from './pages/user-login-history-report/user-login-history-report.component';

function initializeApp(configService: AppConfigService) {
  return () => configService.loadConfig();
}

@NgModule({
  declarations: [
    AppComponent,
    LogoutComponent,
    HomeComponent,
    NavbarComponent,
    DateRangeDialogComponent,
    DashboardComponent,
    PendingRequestComponent,
    CompletedRequestComponent,

    // Merged ESL Declarations
    WaitComponent,
    ConfirmDialogComponent,
    BfreportComponent,
    SmsreportComponent,
    WgreportComponent,
    EslchannelComponent,
    EsldashboardComponent,
    ReaderstatusComponent,
    TransactionsumComponent,
    LadlereportComponent,
    LocationsummaryComponent,
    DepartmentreportComponent,
    LadleWeighmentReportComponent,
    SlagReportComponent,
    ManualVsAutoReportComponent,
    UserLoginHistoryReportComponent,
    SystemStatusComponent
  ],

  imports: [
    BrowserModule,
    BrowserAnimationsModule,

    EslBlastFurnaceComponent,
    EslProductionComponent,
    EslMaintenanceComponent,
    CustomCheckboxComponent,
    CastAssignmentComponent,
    ProductionOrderMappingComponent,
    MapComponent,
    FooterComponent,
    CastAssignmentReportComponent,
    LoginComponent,
    SafeHtmlPipe,
    ProductionreportComponent,
    ReportComponent,

    ToastrModule.forRoot({
      timeOut: 5000,
      positionClass: 'toast-top-right',
      preventDuplicates: true,
      closeButton: true,
      progressBar: true
    }),

    AppRoutingModule,
    RouterOutlet,
    RouterModule,

    MatCardModule,
    MatCheckboxModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatSelectModule,
    FormsModule,
    ReactiveFormsModule,
    HttpClientModule,
    MatIconModule,
    MatDialogModule,
    ScrollingModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatTooltipModule
  ],

  providers: [
    {
      provide: LocationStrategy,
      useClass: HashLocationStrategy
    },
    {
      provide: HTTP_INTERCEPTORS,
      useClass: AuthInterceptor,
      multi: true
    },
    AppConfigService,
    {
      provide: APP_INITIALIZER,
      useFactory: initializeApp,
      deps: [AppConfigService],
      multi: true
    },
    {
      provide: DateAdapter,
      useClass: CustomDateAdapter
    },
    {
      provide: MAT_DATE_FORMATS,
      useValue: CUSTOM_DATE_FORMATS
    }
  ],

  bootstrap: [AppComponent]
})
export class AppModule {}