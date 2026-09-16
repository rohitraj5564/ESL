import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { LoginComponent } from './pages/login/login.component';
import { HomeComponent } from './pages/home/home.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';

import { EslBlastFurnaceComponent } from './pages/esl-blast-furnace/esl-blast-furnace.component';
import { EslProductionComponent } from './pages/esl-production/esl-production.component';
import { EslMaintenanceComponent } from './pages/esl-maintenance/esl-maintenance.component';

import { CastAssignmentComponent } from './pages/cast-assignment/cast-assignment.component';
import { ProductionOrderMappingComponent } from './pages/production-order-mapping/production-order-mapping.component';
import { MapComponent } from './pages/map/map.component';

import { authGuard } from './pages/auth/auth.guard';
import { CastAssignmentReportComponent } from './pages/cast-assignment-report/cast-assignment-report.component';

import { PendingRequestComponent } from './pages/pending-request/pending-request.component';
import { CompletedRequestComponent } from './pages/completed-request/completed-request.component';
import { ReportComponent } from './pages/report/report.component';
import { ProductionreportComponent } from './pages/productionreport/productionreport.component';
import { LocoLadleMovementComponent } from './pages/loco-ladle-movement/loco-ladle-movement.component';

// Merged ESL Components
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

const routes: Routes = [
  {
    path: 'login',
    component: LoginComponent
  },

  {
    path: 'dashboard',
    component: DashboardComponent,
    canActivate: [authGuard]
  },

  {
    path: 'esldashboard',
    component: EsldashboardComponent,
    canActivate: [authGuard]
  },

  {
    path: 'home',
    component: HomeComponent,
    canActivate: [authGuard]
  },

  {
    path: 'blastFurnace',
    component: EslBlastFurnaceComponent,
    canActivate: [authGuard]
  },

  {
    path: 'production',
    component: EslProductionComponent,
    canActivate: [authGuard]
  },

  {
    path: 'maintenance',
    component: EslMaintenanceComponent,
    canActivate: [authGuard]
  },

  {
    path: 'castAssignment',
    component: CastAssignmentComponent,
    canActivate: [authGuard]
  },

  {
    path: 'castAssignmentReport',
    component: CastAssignmentReportComponent,
    canActivate: [authGuard]
  },

  {
    path: 'productionOrderMapping',
    component: ProductionOrderMappingComponent,
    canActivate: [authGuard]
  },

  {
    path: 'map',
    component: MapComponent,
    canActivate: [authGuard]
  },

  {
    path: 'pendingRequest',
    component: PendingRequestComponent,
    canActivate: [authGuard]
  },

  {
    path: 'completedRequest',
    component: CompletedRequestComponent,
    canActivate: [authGuard]
  },

  {
    path: 'productionreport',
    component: ProductionreportComponent,
    canActivate: [authGuard]
  },

  {
    path: 'report',
    component: ReportComponent,
    canActivate: [authGuard]
  },

  {
    path: 'locoLadleMovement',
    component: LocoLadleMovementComponent,
    canActivate: [authGuard]
  },

  // Merged ESL Report Routes
  {
    path: 'readerstatus',
    component: ReaderstatusComponent,
    canActivate: [authGuard]
  },
  {
    path: 'transactionsum',
    component: TransactionsumComponent,
    canActivate: [authGuard]
  },
  {
    path: 'ladlereport',
    component: LadlereportComponent,
    canActivate: [authGuard]
  },
  {
    path: 'locationsummary',
    component: LocationsummaryComponent,
    canActivate: [authGuard]
  },
  {
    path: 'departmentreport',
    component: DepartmentreportComponent,
    canActivate: [authGuard]
  },
  {
    path: 'ladleweighmentreport',
    component: LadleWeighmentReportComponent,
    canActivate: [authGuard]
  },
  {
    path: 'slagreport',
    component: SlagReportComponent,
    canActivate: [authGuard]
  },
  {
    path: 'manualvsautoreport',
    component: ManualVsAutoReportComponent,
    canActivate: [authGuard]
  },

  {
    path: 'systemstatus',
    component: SystemStatusComponent,
    canActivate: [authGuard]
  },

  {
    path: '',
    redirectTo: '/login',
    pathMatch: 'full'
  },

  {
    path: '**',
    redirectTo: '/login'
  }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule {}