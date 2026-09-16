import {
  Component, OnInit, OnDestroy, AfterViewInit,
  TemplateRef, ViewChild, ElementRef, NgZone, ChangeDetectorRef
} from '@angular/core';
import { Router } from '@angular/router';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { interval, Subscription } from 'rxjs';
import { MainserviceService } from '../service/mainservice.service';
import { ThemeService } from '../service/theme.service';

@Component({
  selector: 'app-esldashboard',
  templateUrl: './esldashboard.component.html',
  styleUrls: ['./esldashboard.component.css'],
  standalone: false
})
export class EsldashboardComponent implements OnInit, AfterViewInit, OnDestroy {

  // ── Data ──────────────────────────────────────────────────────────────────
  public RData: any;
  public isLoading = false;
  public bf1Data: any;
  public bf2Data: any;
  public bf3Data: any;
  public wbData: any;
  public smsData: any;
  public dipData: any;
  public pcmData: any;
  public lrsData: any;
  public productionInputData: any;
  public qualityData: any;
  public totalLadleCount: number = 38;
  public activeLadleCount: number = 8;
  public completetripscount: number = 51;
  public pendingtripscount: number = 0;
  public inactiveLadleCount: number = 30;
  public inactiveLadle: any[] = [];
  public averageTat: any;
  public holdingTat: any;
  public InTransitData: any;
  public serviceData: any;
  public activeView: 'esldashboard' | 'dashboard' = 'esldashboard';

  // ── ViewChild refs ────────────────────────────────────────────────────────
  @ViewChild('bf1Slot', { read: ElementRef }) bf1Slot!: ElementRef<HTMLElement>;
  @ViewChild('bf2Slot', { read: ElementRef }) bf2Slot!: ElementRef<HTMLElement>;
  @ViewChild('bf3Slot', { read: ElementRef }) bf3Slot!: ElementRef<HTMLElement>;
  @ViewChild('wbBox',   { read: ElementRef }) wbBox!:   ElementRef<HTMLElement>;
  @ViewChild('inTransitBox', { read: ElementRef }) inTransitBox!: ElementRef<HTMLElement>;
  @ViewChild('smsSlot', { read: ElementRef }) smsSlot!: ElementRef<HTMLElement>;
  @ViewChild('dipSlot', { read: ElementRef }) dipSlot!: ElementRef<HTMLElement>;
  @ViewChild('pcmSlot', { read: ElementRef }) pcmSlot!: ElementRef<HTMLElement>;
  @ViewChild('lrsSlot', { read: ElementRef }) lrsSlot!: ElementRef<HTMLElement>;
  @ViewChild('connectorCol1', { read: ElementRef }) connectorCol1!: ElementRef<HTMLElement>;
  @ViewChild('connectorCol2', { read: ElementRef }) connectorCol2!: ElementRef<HTMLElement>;

  // ── Dynamic SVG connector values ─────────────────────────────────────────
  gridHeight = 500;

  // Left connector (BF -> WB)
  bf1Y = 60;
  bf2Y = 200;
  bf3Y = 340;
  wbYLeft = 200;
  bfConnPath1 = '';
  bfConnPath2 = '';
  bfConnPath3 = '';

  // Right connector (WB/InTransit -> SMS/DIP/PCM/LRS)
  wbYRight = 200;
  inTransitYRight = 320;
  smsY = 60;
  dipY = 180;
  pcmY = 310;
  lrsY = 430;
  wbConnPath1 = '';
  wbConnPath2 = '';
  wbConnPath3 = '';
  wbConnPath4 = '';
  wbConnPath5 = '';
  // InTransit → stations
  itConnPath1 = '';
  itConnPath2 = '';
  itConnPath3 = '';
  itConnPath4 = '';

  // ── Internals ─────────────────────────────────────────────────────────────
  private updateSubscription?: Subscription;
  private currentDialogRef?: MatDialogRef<any>;
  private resizeObserver?: ResizeObserver;

  constructor(
    public mainService: MainserviceService,
    private dialog: MatDialog,
    public themeService: ThemeService,
    private router: Router,
    private ngZone: NgZone,
    private cdr: ChangeDetectorRef
  ) {}

  // ── Lifecycle ─────────────────────────────────────────────────────────────
  ngOnInit() {
    this.isLoading = true;
    this.loadDashboardData();
    this.loadServiceStatus();

    this.updateSubscription = interval(5000).subscribe(() => {
      this.refreshDataSilent();
      this.loadServiceStatus();
    });
  }

  ngAfterViewInit() {
    // Give the browser one frame to finish layout, then measure
    requestAnimationFrame(() => {
      this.recalculateConnectors();
      this.cdr.detectChanges();
    });

    // Watch for resize / layout changes
    if (typeof ResizeObserver !== 'undefined' && this.connectorCol1?.nativeElement) {
      this.resizeObserver = new ResizeObserver(() => {
        this.ngZone.run(() => {
          this.recalculateConnectors();
          this.cdr.detectChanges();
        });
      });
      this.resizeObserver.observe(this.connectorCol1.nativeElement);
    }
  }

  ngOnDestroy() {
    this.updateSubscription?.unsubscribe();
    this.resizeObserver?.disconnect();
  }

  // ── Connector Calculation ─────────────────────────────────────────────────
  /**
   * Measures the actual rendered top/height of each card slot relative to
   * the connector column and builds the SVG path strings accordingly.
   */
  recalculateConnectors(): void {
    const col1 = this.connectorCol1?.nativeElement;
    const col2 = this.connectorCol2?.nativeElement;
    if (!col1 || !col2) return;

    const col1Rect = col1.getBoundingClientRect();
    const col2Rect = col2.getBoundingClientRect();

    const h1 = col1Rect.height || 500;
    const h2 = col2Rect.height || 500;
    this.gridHeight = Math.max(h1, h2);

    // Helper: get vertical midpoint of an element relative to a reference top
    const midY = (el: ElementRef | undefined, refTop: number): number => {
      if (!el?.nativeElement) return 0;
      const r = el.nativeElement.getBoundingClientRect();
      return r.top - refTop + r.height / 2;
    };

    // ── LEFT CONNECTOR (BF -> WB) ──────────────────────────────────────────
    const refTop1 = col1Rect.top;
    this.bf1Y    = midY(this.bf1Slot, refTop1);
    this.bf2Y    = midY(this.bf2Slot, refTop1);
    this.bf3Y    = midY(this.bf3Slot, refTop1);
    this.wbYLeft = midY(this.wbBox,   refTop1);

    const mx1 = 28; // x of vertical spine
    // All 3 BF cards go through the spine to WB — no straight line for BF2
    this.bfConnPath1 = `M 0 ${this.bf1Y} L ${mx1} ${this.bf1Y} L ${mx1} ${this.wbYLeft} L 60 ${this.wbYLeft}`;
    this.bfConnPath2 = `M 0 ${this.bf2Y} L ${mx1} ${this.bf2Y} L ${mx1} ${this.wbYLeft} L 60 ${this.wbYLeft}`;
    this.bfConnPath3 = `M 0 ${this.bf3Y} L ${mx1} ${this.bf3Y} L ${mx1} ${this.wbYLeft} L 60 ${this.wbYLeft}`;

    // ── RIGHT CONNECTOR (WB/InTransit -> SMS/DIP/PCM/LRS) ──────────────────
    const refTop2 = col2Rect.top;
    this.wbYRight        = midY(this.wbBox,        refTop2);
    this.inTransitYRight = midY(this.inTransitBox, refTop2);
    this.smsY = midY(this.smsSlot, refTop2);
    this.dipY = midY(this.dipSlot, refTop2);
    this.pcmY = midY(this.pcmSlot, refTop2);
    this.lrsY = midY(this.lrsSlot, refTop2);

    const mx2 = 30; // x of vertical spine
    // WB → SMS  (go up from WB spine)
    this.wbConnPath1 = `M 0 ${this.wbYRight} L ${mx2} ${this.wbYRight} L ${mx2} ${this.smsY} L 60 ${this.smsY}`;
    // WB → DIP  (L-shape spine like PCM, NOT diagonal)
    this.wbConnPath2 = `M 0 ${this.wbYRight} L ${mx2} ${this.wbYRight} L ${mx2} ${this.dipY} L 60 ${this.dipY}`;
    // WB → PCM
    this.wbConnPath3 = `M 0 ${this.wbYRight} L ${mx2} ${this.wbYRight} L ${mx2} ${this.pcmY} L 60 ${this.pcmY}`;
    // WB → LRS
    this.wbConnPath4 = `M 0 ${this.wbYRight} L ${mx2} ${this.wbYRight} L ${mx2} ${this.lrsY} L 60 ${this.lrsY}`;
    // WB → InTransit: vertical drop at CENTER of connector (x=30 spine), not at edge
    this.wbConnPath5 = `M ${mx2} ${this.wbYRight} L ${mx2} ${this.inTransitYRight}`;

    // InTransit → SMS, DIP, PCM, LRS (same spine structure as WB)
    this.itConnPath1 = `M 0 ${this.inTransitYRight} L ${mx2} ${this.inTransitYRight} L ${mx2} ${this.smsY} L 60 ${this.smsY}`;
    this.itConnPath2 = `M 0 ${this.inTransitYRight} L ${mx2} ${this.inTransitYRight} L ${mx2} ${this.dipY} L 60 ${this.dipY}`;
    this.itConnPath3 = `M 0 ${this.inTransitYRight} L ${mx2} ${this.inTransitYRight} L ${mx2} ${this.pcmY} L 60 ${this.pcmY}`;
    this.itConnPath4 = `M 0 ${this.inTransitYRight} L ${mx2} ${this.inTransitYRight} L ${mx2} ${this.lrsY} L 60 ${this.lrsY}`;
  }

  // ── Data Loading ──────────────────────────────────────────────────────────
  loadDashboardData() {
    this.mainService.getLiveDashboard().subscribe({
      next: (res: any) => {
        this.isLoading = false;
        const payload = res?.data ? res.data : res;
        this.processDashboardResponse(payload);
        // Recalculate after data loads (cards may change size)
        setTimeout(() => { this.recalculateConnectors(); this.cdr.detectChanges(); }, 150);
      },
      error: (err) => {
        console.error('Error fetching live dashboard data:', err);
        this.isLoading = false;
      }
    });
  }

  loadServiceStatus() {
    this.mainService.getServiceActiveStatus().subscribe({
      next: (res: any) => {
        if (res && res.length > 0) {
          this.serviceData = res[0];
        }
      },
      error: (err) => {
        console.error('Error fetching service status:', err);
      }
    });
  }

  refreshData() {
    this.isLoading = true;
    this.loadDashboardData();
    this.loadServiceStatus();
  }

  refreshDataSilent() {
    this.mainService.getLiveDashboard().subscribe({
      next: (res: any) => {
        const payload = res?.data ? res.data : res;
        this.processDashboardResponse(payload);
        console.log(`[ESL Live Tracking] Internal auto-refresh completed at ${new Date().toLocaleTimeString()}`);
      },
      error: (err) => {
        console.error('Error in background dashboard refresh:', err);
      }
    });
  }

  // ── Response Parsing ──────────────────────────────────────────────────────
  processDashboardResponse(data: any) {
    if (!data) return;
    this.RData = data;

    const findLocation = (names: string[]) => {
      if (!data.LocationData || !Array.isArray(data.LocationData)) return undefined;
      return data.LocationData.find((loc: any) => {
        const n = (loc?.LocationName || '').toLowerCase().replace(/[\s_-]/g, '');
        return names.some(target => target.toLowerCase().replace(/[\s_-]/g, '') === n);
      });
    };

    // Blast Furnaces (Left Column)
    this.bf1Data = data.BF1 || findLocation(['BF1', 'BF 1', 'BF-1', 'BlastFurnace1']);
    this.bf2Data = data.BF2 || findLocation(['BF2', 'BF 2', 'BF-2', 'BlastFurnace2']);
    this.bf3Data = data.BF3 || findLocation(['BF3', 'BF 3', 'BF-3', 'BlastFurnace3']);

    // Center Hub (Weighbridge & In Transit)
    this.wbData = data.WB || findLocation(['Weighbridge', 'WB', 'WeighBridge', 'WEIGHMENT']);
    this.InTransitData = data.InTransit || findLocation(['In Transit', 'InTransit', 'IN_TRANSIT', 'INTRANSIT']);

    // Refining & Casting Stations (Right Column)
    this.smsData = data.SMS || findLocation(['SMS', 'SMS 1', 'SMS1']);
    this.dipData = data.DIP || findLocation(['DIP', 'DIP 1', 'DIP1']);
    this.pcmData = data.PCM || findLocation(['PCM', 'PCM 1', 'PCM1']);
    this.lrsData = data.LRS || findLocation(['LRS', 'LRS 1', 'LRS1']);

    const ensureTimeMetrics = (loc: any) => {
      if (!loc) return;
      if (!loc.AverageTATSTR || loc.AverageTATSTR === '0' || loc.AverageTATSTR === '00:00:00') {
        loc.AverageTATSTR = '00:00';
      }
      if (!loc.AverageHoldTimeSTR || loc.AverageHoldTimeSTR === '0' || loc.AverageHoldTimeSTR === '00:00:00') {
        loc.AverageHoldTimeSTR = '00:00';
      }
    };

    ensureTimeMetrics(this.smsData);
    ensureTimeMetrics(this.dipData);
    ensureTimeMetrics(this.pcmData);
    ensureTimeMetrics(this.lrsData);
    ensureTimeMetrics(this.bf1Data);
    ensureTimeMetrics(this.bf2Data);
    ensureTimeMetrics(this.bf3Data);

    // KPI Counters
    if (data.TotalLadleCount !== undefined) {
      this.totalLadleCount = data.TotalLadleCount;
    } else if (data.TotalActiveLadleCount !== undefined) {
      this.totalLadleCount = data.TotalActiveLadleCount;
    }

    if (data.ActiveLadleCount !== undefined) {
      this.activeLadleCount = data.ActiveLadleCount;
    } else if (data.TotalInUseLadleCount !== undefined) {
      this.activeLadleCount = data.TotalInUseLadleCount;
    }

    if (data.completetripscount !== undefined) {
      this.completetripscount = data.completetripscount;
    } else if (data.CompletedTrips !== undefined) {
      this.completetripscount = data.CompletedTrips;
    }

    if (data.pendingtripscount !== undefined) {
      this.pendingtripscount = data.pendingtripscount;
    } else if (data.PendingTrips !== undefined) {
      this.pendingtripscount = data.PendingTrips;
    }

    if (data.inactiveLadleCount !== undefined) {
      this.inactiveLadleCount = data.inactiveLadleCount;
    } else if (data.UnusedLadles && Array.isArray(data.UnusedLadles)) {
      this.inactiveLadleCount = data.UnusedLadles.length;
    } else if (this.totalLadleCount !== undefined && this.activeLadleCount !== undefined) {
      this.inactiveLadleCount = Math.max(0, this.totalLadleCount - this.activeLadleCount);
    }

    if (data.inactiveLadle) {
      this.inactiveLadle = data.inactiveLadle;
    } else if (data.UnusedLadles) {
      this.inactiveLadle = data.UnusedLadles;
    }

    // Right Tables Data
    this.productionInputData = data.productionInputData || data.ProductionSummary || [];
    this.qualityData = data.qualityData || data.LimsSummary || data.QualityData || [];
  }

  // ── View Switching ────────────────────────────────────────────────────────
  setView(view: 'esldashboard' | 'dashboard') {
    this.activeView = view;
    if (view === 'dashboard') {
      this.router.navigate(['/dashboard']);
    }
  }

  // ── Modals & Dialogs ──────────────────────────────────────────────────────
  open(content: TemplateRef<any>) {
    this.currentDialogRef = this.dialog.open(content, {
      width: '650px',
      maxHeight: '85vh',
      panelClass: 'esl-custom-dialog'
    });
  }

  closeDialog() {
    if (this.currentDialogRef) {
      this.currentDialogRef.close();
    }
  }
}
