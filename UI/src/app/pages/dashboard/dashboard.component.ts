import { Component, ElementRef, ViewChild, AfterViewInit, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import * as echarts from 'echarts';
import * as ExcelJS from 'exceljs';
import * as fs from 'file-saver';
import { DashboardService } from '../service/dashboard.service';
import { ThemeService } from '../service/theme.service';
import { Subscription, forkJoin, of } from 'rxjs';
import { catchError, finalize, tap } from 'rxjs/operators';

@Component({
  selector: 'app-dashboard',
  standalone: false,
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent implements OnInit, AfterViewInit, OnDestroy {
  isLoading = false;
  lastUpdate = '';
  private timeInterval!: any;
  private chartInterval!: any;
  isDarkMode = false;
  private themeSub!: Subscription;

  ladleData = { active: 0, idle: 0 };
  tripData = { completed: 0, pending: 0 };
  areaData: { category: string, value: number }[] = [];
  activeLadleData: { time: string, value: number }[] = [];
  tripShiftData: { hour: string, actual: number, target: number }[] = [];
  retentionData: { time: string, bf: number, sms: number, lrs: number }[] = [];
  prodConsData: { category: string, prod: number | null, cons: number | null }[] = [];
  tatData: { ladle: string, value: number }[] = [];
  productionData: { category: string, value: number }[] = [];
  consumptionData: { category: string, value: number }[] = [];

  kpis: KPI[] = [];

  showExportModal = false;
  exportFromDate = '';
  exportToDate = '';

  exportPresets = [
    { label: 'Today', days: 0 },
    { label: 'Last 7D', days: 7 },
    { label: 'Last 30D', days: 30 },
    { label: 'Last 90D', days: 90 },
  ];

  private kpistimeInterval!: any;
  analyticalData: any;
  locationsData: any;
  activeLadleDataInUsed: { Time: string, TotalActiveLadle: number }[] = [];

  constructor(
    private cdr: ChangeDetectorRef,
    private dashboardService: DashboardService,
    private themeService: ThemeService,
    private router: Router
  ) { }

  getChartTextColor() { return this.isDarkMode ? '#94a3b8' : '#64748b'; }
  getChartLineColor() { return this.isDarkMode ? '#334155' : '#e2e8f0'; }
  getChartTitleColor() { return this.isDarkMode ? '#fff' : '#0f172a'; }

  navigateTo(route: string): void {
    this.router.navigate(['/' + route]);
  }

  ngOnInit() {
    this.themeSub = this.themeService.isDarkMode$.subscribe(isDark => {
      this.isDarkMode = isDark;
      // Re-render charts when theme changes (only if charts are initialized)
      if (this.ladleChartInstance) {
        this.reloadCharts();
      }
    });

    this.updateLastUpdate();
    this.loadAllData();
    this.timeInterval = setInterval(() => {
      this.updateLastUpdate();
      this.cdr.markForCheck();
    }, 1000);
  }

  refreshData() {
    this.loadAllData(false);
  }

  loadAllData(isSilent: boolean = false) {
    if (!isSilent) {
      this.isLoading = true;
      this.cdr.markForCheck();
    }

    forkJoin([
      this.loadChart(),
      this.loadcompletedTripsovertime(),
      this.loadHourlyData(),
      this.loadKPIs(),
      this.loadAnalyticalDashboard()
    ]).pipe(
      finalize(() => {
        if (!isSilent) {
          this.isLoading = false;
        }
        this.cdr.markForCheck();
        setTimeout(() => {
          this.reloadCharts();
        }, 100);
        if (isSilent) {
          console.log(`[Dashboard Analytics] Internal auto-refresh completed at ${new Date().toLocaleTimeString()}`);
        }
      })
    ).subscribe();
  }

  ngOnDestroy() {
    if (this.timeInterval) clearInterval(this.timeInterval);
    if (this.chartInterval) clearInterval(this.chartInterval);
    if (this.themeSub) this.themeSub.unsubscribe();

    const charts = [
      this.ladleChartInstance, this.tripChartInstance, this.areaChartInstance,
      this.activeLadleChartInstance, this.tripShiftChartInstance,
      this.retentionChartInstance, this.tatChartInstance,
      this.productionChartInstance, this.consumptionChartInstance
    ];
    charts.forEach(c => c?.dispose());
  }

  updateLastUpdate() {
    const now = new Date();
    const day = String(now.getDate()).padStart(2, '0');
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const year = now.getFullYear();
    let hours = now.getHours();
    const minutes = String(now.getMinutes()).padStart(2, '0');
    const ampm = hours >= 12 ? 'PM' : 'AM';
    hours = hours % 12 || 12;
    this.lastUpdate = `${day}-${month}-${year} ${hours}:${minutes} ${ampm}`;
  }

  @ViewChild('ladleChart') ladleChart!: ElementRef;
  @ViewChild('tripChart') tripChart!: ElementRef;
  @ViewChild('areaChart') areaChart!: ElementRef;
  @ViewChild('activeLadleChart') activeLadleChart!: ElementRef;
  @ViewChild('tripShiftChart') tripShiftChart!: ElementRef;
  @ViewChild('retentionChart') retentionChart!: ElementRef;

  @ViewChild('tatChart') tatChart!: ElementRef;
  @ViewChild('productionChart') productionChart!: ElementRef;
  @ViewChild('consumptionChart') consumptionChart!: ElementRef;

  public ladleChartInstance!: echarts.ECharts;
  public tripChartInstance!: echarts.ECharts;
  public areaChartInstance!: echarts.ECharts;
  public activeLadleChartInstance!: echarts.ECharts;
  public tripShiftChartInstance!: echarts.ECharts;
  public retentionChartInstance!: echarts.ECharts;

  public tatChartInstance!: echarts.ECharts;
  public productionChartInstance!: echarts.ECharts;
  public consumptionChartInstance!: echarts.ECharts;

  tripView: 'bar' | 'line' = 'bar';
  autoRefresh = true;

  ngAfterViewInit() {
    setTimeout(() => {
      this.initCharts();
      if (this.autoRefresh) {
        this.chartInterval = setInterval(() => {
          this.loadAllData(true);
        }, 5000);
      }
    }, 0);
  }

  initCharts() {
    this.initLadleChart();
    this.initTripChart();
    this.initAreaChart();
    this.initActiveLadleChart();
    this.initTripShiftChart();
    //this.initRetentionChart();
    this.initProductionChart();
    this.initConsumptionChart();
    //this.initTatChart();
  }

  reloadCharts() {
    this.updateLastUpdate();
    if (this.ladleChartInstance) this.updateLadleChart();
    if (this.tripChartInstance) this.updateTripChart();
    if (this.areaChartInstance) this.updateAreaChart();
    if (this.tripShiftChartInstance) this.updateTripShiftChart();
    if (this.activeLadleChartInstance) this.updateActiveLadleChart();
    if (this.retentionChartInstance) this.updateRetentionChart();
    if (this.productionChartInstance) this.updateProductionChart();
    if (this.consumptionChartInstance) this.updateConsumptionChart();
    if (this.tatChartInstance) this.updateTatChart();
  }

  private resizeObserverMap = new WeakMap<HTMLElement, ResizeObserver>();
  private observeResize(chart: echarts.ECharts) {
    const el = chart.getDom() as HTMLElement;
    if (this.resizeObserverMap.has(el)) return;
    const ro = new ResizeObserver(() => { requestAnimationFrame(() => chart.resize()); });
    ro.observe(el);
    this.resizeObserverMap.set(el, ro);
  }

  private base64ToBuffer(base64: string): ArrayBuffer {
    const binaryString = window.atob(base64);
    const len = binaryString.length;
    const bytes = new Uint8Array(len);
    for (let i = 0; i < len; i++) {
      bytes[i] = binaryString.charCodeAt(i);
    }
    return bytes.buffer;
  }

  getTodayStr(): string {
    const d = new Date();
    return d.toISOString().split('T')[0];
  }

  openExportModal() {
    this.exportToDate = this.getTodayStr();
    this.exportFromDate = this.getTodayStr();
    this.showExportModal = true;
  }

  closeExportModal(event: MouseEvent) {
    this.showExportModal = false;
  }

  applyPreset(days: number) {
    const to = new Date();
    const from = new Date();
    from.setDate(from.getDate() - days);
    this.exportToDate = to.toISOString().split('T')[0];
    this.exportFromDate = from.toISOString().split('T')[0];
  }

  async confirmExport() {
    this.showExportModal = false;
    await this.exportToExcel();
  }

  initLadleChart() {
    this.ladleChartInstance = echarts.init(this.ladleChart.nativeElement);
    this.observeResize(this.ladleChartInstance);
    this.updateLadleChart();
  }

  updateLadleChart() {
    if (!this.kpis || this.kpis.length === 0) {
      console.warn('KPI data not loaded yet');
      return;
    }

    const activeKPI = this.kpis.find(k => k.title === 'Active Ladles');
    const idleKPI = this.kpis.find(k => k.title === 'Idle Ladles');

    const active = activeKPI ? Number(activeKPI.value) : 0;
    const idle = idleKPI ? Number(idleKPI.value) : 0;

    this.ladleData = { active, idle };

    this.ladleChartInstance.setOption({
      animation: false,
      tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
      legend: {
        bottom: '0%',
        left: 'center',
        itemGap: 15,
        textStyle: { color: this.getChartTextColor(), fontSize: 12 },
        data: ['Active Ladles', 'Idle Ladles']
      },
      series: [{
        type: 'pie',
        radius: '65%',
        center: ['50%', '45%'],
        label: {
          show: true,
          position: 'inside',
          formatter: '{d}%',
          color: '#fff',
          fontWeight: 'bold'
        },
        itemStyle: {
          borderColor: this.isDarkMode ? '#0f172a' : '#fff',
          borderWidth: 2
        },
        data: [
          { value: active, name: 'Active Ladles', itemStyle: { color: '#22c55e' } },
          { value: idle, name: 'Idle Ladles', itemStyle: { color: '#facc15' } }
        ]
      }]
    });
  }

  initTripChart() {
    this.tripChartInstance = echarts.init(this.tripChart.nativeElement);
    this.observeResize(this.tripChartInstance);
    this.updateTripChart();
  }

  updateTripChart() {
    if (!this.kpis || this.kpis.length === 0) {
      console.warn('KPI data not loaded yet');
      return;
    }

    const completedKPI = this.kpis.find(k => k.title === 'Completed Trips');
    const pendingKPI = this.kpis.find(k => k.title === 'Pending Trips');

    const completed = completedKPI ? Number(completedKPI.value) : 0;
    const pending = pendingKPI ? Number(pendingKPI.value) : 0;

    const total = completed + pending;

    this.tripData = { completed, pending };

    this.tripChartInstance.setOption({
      animation: false,
      title: {
        text: total.toString(),
        subtext: 'Total Trips',
        left: 'center',
        top: '40%',
        textStyle: {
          color: this.getChartTitleColor(),
          fontSize: 28
        },
        subtextStyle: {
          color: this.getChartTextColor()
        },
      },
      tooltip: { trigger: 'item' },
      legend: {
        bottom: '0%',
        left: 'center',
        itemGap: 15,
        textStyle: { color: this.getChartTextColor(), fontSize: 12 },
        data: ['Completed', 'Pending']
      },
      series: [{
        type: 'pie',
        radius: ['60%', '75%'],
        center: ['50%', '45%'],
        label: { show: false },
        itemStyle: {
          borderColor: this.isDarkMode ? '#0f172a' : '#fff',
          borderWidth: 2
        },
        data: [
          { value: completed, name: 'Completed', itemStyle: { color: '#06b6d4' } },
          { value: pending, name: 'Pending', itemStyle: { color: '#f97316' } }
        ]
      }]
    });
  }

  initAreaChart() {
    this.areaChartInstance = echarts.init(this.areaChart.nativeElement);
    this.observeResize(this.areaChartInstance);
    this.updateAreaChart();
  }

  updateAreaChart() {
    if (!this.analyticalData || !this.analyticalData.LocationData) {
      console.warn('Location data not loaded yet');
      return;
    }

    const categories = this.analyticalData.LocationData.map((loc: any) => loc.LocationName);

    const values = this.analyticalData.LocationData.map((loc: any) =>
      loc.LadleList ? loc.LadleList.length : 0
    );

    this.areaData = this.analyticalData.LocationData.map((loc: any) => ({
      category: loc.LocationName,
      value: loc.LadleList ? loc.LadleList.length : 0
    }));

    this.areaChartInstance.setOption({
      animation: false,
      tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
      grid: { left: 60, right: 30, top: 20, bottom: 20 },

      xAxis: {
        type: 'value',
        axisLabel: { color: this.getChartTextColor() },
        splitLine: { show: false },
        axisLine: { show: true, lineStyle: { color: this.getChartLineColor() } }
      },

      yAxis: {
        type: 'category',
        data: categories,
        axisLabel: { color: this.getChartTextColor() },
        axisLine: { show: true, lineStyle: { color: this.getChartLineColor() } }
      },

      series: [{
        type: 'bar',
        data: values,
        barHeight: 14,
        itemStyle: {
          color: '#06b6d4',
          borderRadius: [0, 8, 8, 0]
        }
      }]
    });
  }

  initActiveLadleChart() {
    this.activeLadleChartInstance = echarts.init(this.activeLadleChart.nativeElement);
    this.observeResize(this.activeLadleChartInstance);

    this.updateActiveLadleChart();
  }

  updateActiveLadleChart() {
    const times = this.activeLadleDataInUsed.map(x => x.Time);
    const values = this.activeLadleDataInUsed.map(x => x.TotalActiveLadle);

    this.activeLadleDataInUsed = times.map((t, i) => ({ Time: t, TotalActiveLadle: values[i] }));

    this.activeLadleChartInstance.setOption({
      animation: false,

      tooltip: {
        trigger: 'axis'
      },

      grid: {
        left: 60,
        right: 20,
        top: 40,
        bottom: 60
      },

      xAxis: {
        type: 'category',
        name: 'Time',
        nameLocation: 'middle',
        nameGap: 40,
        data: times,

        axisLabel: {
          color: this.getChartTextColor(),
          rotate: 35
        },

        axisLine: {
          lineStyle: {
            color: this.getChartLineColor()
          }
        },

        nameTextStyle: {
          color: this.getChartTextColor(),
          fontSize: 14,
          fontWeight: 'bold'
        }
      },

      yAxis: {
        type: 'value',
        name: 'Ladles',
        nameLocation: 'middle',
        nameGap: 50,
        min: 1,
        max: 40,

        axisLabel: {
          color: this.getChartTextColor()
        },

        splitLine: {
          lineStyle: {
            color: this.getChartLineColor()
          }
        },

        axisLine: {
          lineStyle: {
            color: this.getChartLineColor()
          }
        },

        nameTextStyle: {
          color: this.getChartTextColor(),
          fontSize: 14,
          fontWeight: 'bold'
        }
      },

      series: [{
        type: 'line',
        smooth: true,
        symbolSize: 8,

        lineStyle: {
          width: 3,
          color: '#22d3ee'
        },

        itemStyle: {
          color: '#22d3ee'
        },

        areaStyle: {
          color: new echarts.graphic.LinearGradient(
            0, 0, 0, 1,
            [
              { offset: 0, color: 'rgba(34,211,238,0.35)' },
              { offset: 1, color: 'rgba(34,211,238,0)' }
            ]
          )
        },

        data: values
      }]
    });
  }

  initTripShiftChart() {
    this.tripShiftChartInstance = echarts.init(this.tripShiftChart.nativeElement);
    this.observeResize(this.tripShiftChartInstance);
    this.updateTripShiftChart();
  }

  loadcompletedTripsovertime() {
    return this.dashboardService.getHourlyTrips().pipe(
      tap((res: any) => {
        console.log('Hourly Trips:', res);
        if (res && res.HourlyTrips && Array.isArray(res.HourlyTrips)) {
          this.tripShiftData = res.HourlyTrips.map((x: any) => ({
            hour: x.Time,
            actual: x.CompletedTrips,
            target: x.TargetTrips
          }));
        } else if (Array.isArray(res)) {
          this.tripShiftData = res.map((x: any) => ({
            hour: x.hour || x.Time,
            actual: x.actual !== undefined ? x.actual : x.CompletedTrips,
            target: x.target !== undefined ? x.target : x.TargetTrips
          }));
        }
        if (this.tripShiftChartInstance) {
          this.updateTripShiftChart();
        }
      }),
      catchError(err => {
        console.error('Error fetching hourly trips:', err);
        return of([]);
      })
    );
  }

  updateTripShiftChart() {
    const hours = this.tripShiftData.map((x: any) => x.hour);
    const actual = this.tripShiftData.map((x: any) => x.actual);
    const target = this.tripShiftData.map((x: any) => x.target);

    this.tripShiftChartInstance.setOption({
      animation: false,
      tooltip: { trigger: 'axis' },
      legend: { data: ['Trips'], top: 10, textStyle: { color: this.getChartTextColor() } },
      grid: { left: 50, right: 20, top: 50, bottom: 40 },
      xAxis: { type: 'category', data: hours, axisLabel: { color: this.getChartTextColor() }, axisLine: { lineStyle: { color: this.getChartLineColor() } } },
      yAxis: { type: 'value', axisLabel: { color: this.getChartTextColor() }, splitLine: { lineStyle: { color: this.getChartLineColor() } } },
      series: [
        { name: 'Trips', type: this.tripView, data: actual, itemStyle: { color: '#22d3ee' }, smooth: true }
      ]
    });
  }

  initRetentionChart() {
    this.retentionChartInstance = echarts.init(this.retentionChart.nativeElement);
    this.observeResize(this.retentionChartInstance);
    this.updateRetentionChart();
  }

  updateRetentionChart() {
    if (!this.retentionChartInstance) return;
    const times = ['04:27', '04:32', '04:37', '04:42', '04:47', '04:52', '04:57', '05:02', '05:07', '05:12', '05:17'];
    const bfData = [32, 17, 24, 22, 21, 18, 25, 31, 20, 26, 22];
    const smsData = [35, 44, 34, 33, 34, 39, 35, 36, 41, 32, 31];
    const lrsData = [16, 21, 19, 17, 16, 14, 19, 17, 21, 15, 18];
    this.retentionData = times.map((t, i) => ({ time: t, bf: bfData[i], sms: smsData[i], lrs: lrsData[i] }));

    this.retentionChartInstance.setOption({
      animation: false,
      tooltip: { trigger: 'axis' },
      legend: { data: ['BF', 'SMS', 'LRS'], top: 10, textStyle: { color: this.getChartTextColor() } },
      grid: { left: 50, right: 20, top: 50, bottom: 40 },
      xAxis: { type: 'category', data: times, axisLabel: { color: this.getChartTextColor() }, axisLine: { lineStyle: { color: this.getChartLineColor() } } },
      yAxis: { type: 'value', name: 'Minutes', axisLabel: { color: this.getChartTextColor() }, splitLine: { lineStyle: { color: this.getChartLineColor() } } },
      series: [
        { name: 'BF', type: 'line', smooth: true, data: bfData, itemStyle: { color: '#22d3ee' } },
        { name: 'SMS', type: 'line', smooth: true, data: smsData, itemStyle: { color: '#fb923c' } },
        { name: 'LRS', type: 'line', smooth: true, data: lrsData, itemStyle: { color: '#22c55e' } }
      ]
    });
  }

  initTatChart() {
    this.tatChartInstance = echarts.init(this.tatChart.nativeElement);
    this.observeResize(this.tatChartInstance);
    this.updateTatChart();
  }

  updateTatChart() {
    if (!this.tatChartInstance) return;
    const ladles = ['L01', 'L02', 'L03', 'L04', 'L05', 'L06', 'L07', 'L08', 'L09', 'L10', 'L11', 'L12', 'L13', 'L14', 'L15', 'L16', 'L17', 'L18', 'L19', 'L20', 'L21', 'L22', 'L23', 'L24', 'L25', 'L26', 'L27', 'L28', 'L29', 'L30', 'L31', 'L32', 'L33', 'L34', 'L35', 'L36', 'L37', 'L38', 'L39', 'L40'];
    const values = [29, 29, 56, 26, 24, 30, 26, 35, 27, 28, 43, 22, 45, 30, 31, 29, 27, 33, 25, 26, 28, 24, 32, 30, 29, 27, 31, 26, 28, 34, 29, 27, 30, 25, 31, 28, 26, 29, 27, 30, 24];
    this.tatData = ladles.map((l, i) => ({ ladle: l, value: values[i] }));

    this.tatChartInstance.setOption({
      animation: false,
      tooltip: { trigger: 'axis' },
      grid: { left: 50, right: 20, top: 40, bottom: 50 },
      xAxis: { type: 'category', data: ladles, axisLabel: { color: this.getChartTextColor() }, axisLine: { lineStyle: { color: this.getChartLineColor() } } },
      yAxis: { type: 'value', name: 'Minutes', max: 60, axisLabel: { color: this.getChartTextColor() }, splitLine: { lineStyle: { color: this.getChartLineColor() } } },
      series: [{
        type: 'bar', barWidth: 30,
        data: values.map(v => ({ value: v, itemStyle: { color: v >= 50 ? '#ef4444' : '#06b6d4' } })),
        markLine: { symbol: 'none', lineStyle: { color: '#ef4444', type: 'dashed' }, label: { show: true, formatter: 'Threshold: 50 min', color: '#ef4444' }, data: [{ yAxis: 50 }] }
      }]
    });
  }

  initProductionChart() {
    this.productionChartInstance = echarts.init(this.productionChart.nativeElement);
    this.observeResize(this.productionChartInstance);
    this.updateProductionChart();
    setTimeout(() => this.productionChartInstance.resize(), 100);
  }

  updateProductionChart() {
    if (!this.productionChartInstance) return;
    const timeSlots = this.hourlyData.map(x =>
      this.formatHour(x.HourTimeSlot || x.HourSlot || x.hourSlot || '')
    );

    const bf2 = this.hourlyData.map(x => x.BF2Production || 0);
    const bf3 = this.hourlyData.map(x => x.BF3Production || 0);
    const bf1 = this.hourlyData.map(x => x.bf1Production || 0);

    this.productionData = timeSlots.map((t, i) => ({
      category: t,
      value: bf2[i],
      bf1: bf1[i],
      bf2: bf2[i],
      bf3: bf3[i],
    }));

    this.productionChartInstance.setOption({
      animation: false,

      tooltip: {
        trigger: 'axis',
        axisPointer: { type: 'shadow' }
      },

      legend: {
        data: ['BF1', 'BF2', 'BF3'],
        top: 10,
        textStyle: {
          color: this.getChartTextColor()
        }
      },

      grid: {
        left: 20,
        right: 20,
        top: 50,
        bottom: 20,
        containLabel: true
      },

      xAxis: {
        type: 'category',
        data: timeSlots,
        axisLabel: {
          color: this.getChartTextColor(),
          interval: 0,
          rotate: 35
        },
        axisLine: {
          lineStyle: {
            color: this.getChartLineColor()
          }
        },
        axisTick: {
          alignWithLabel: true
        }
      },

      yAxis: {
        type: 'value',
        name: 'Tonnes',
        axisLabel: {
          color: this.getChartTextColor()
        },
        splitLine: {
          lineStyle: {
            color: this.getChartLineColor()
          }
        }
      },

      series: [
        {
          name: 'BF1',
          type: 'bar',
          barWidth: '20%',
          data: bf1,
          itemStyle: {
            color: '#22c55e',
            borderRadius: [4, 4, 0, 0]
          }
        },

        {
          name: 'BF2',
          type: 'bar',
          barWidth: '20%',
          data: bf2,
          itemStyle: {
            color: '#22d3ee',
            borderRadius: [4, 4, 0, 0]
          }
        },

        {
          name: 'BF3',
          type: 'bar',
          barWidth: '20%',
          data: bf3,
          itemStyle: {
            color: '#f97316',
            borderRadius: [4, 4, 0, 0]
          }
        }
      ]
    });
  }

  initConsumptionChart() {
    this.consumptionChartInstance = echarts.init(this.consumptionChart.nativeElement);
    this.observeResize(this.consumptionChartInstance);
    this.updateConsumptionChart();
    setTimeout(() => this.consumptionChartInstance.resize(), 100);
  }

  updateConsumptionChart() {
    if (!this.consumptionChartInstance) return;
    const timeSlots = this.hourlyData.map(x =>
      this.formatHour(x.HourTimeSlot || x.HourSlot || x.hourSlot || '')
    );

    const SMS = this.hourlyData.map(x => x.SMSConsumption || 0);
    const DIP = this.hourlyData.map(x => x.DIPConsumption || 0);
    const PCM = this.hourlyData.map(x => x.PCMConsumption || 0);

    this.consumptionData = timeSlots.map((t, i) => ({
      category: t,
      value: SMS[i],
      sms: SMS[i],
      dip: DIP[i],
      pcm: PCM[i],
    }));

    this.consumptionChartInstance.setOption({
      animation: false,

      tooltip: {
        trigger: 'axis',
        axisPointer: { type: 'shadow' }
      },

      legend: {
        data: ['SMS', 'DIP', 'PCM'],
        top: 10,
        textStyle: {
          color: this.getChartTextColor()
        }
      },

      grid: {
        left: 20,
        right: 20,
        top: 50,
        bottom: 20,
        containLabel: true
      },

      xAxis: {
        type: 'category',
        data: timeSlots,
        axisLabel: {
          color: this.getChartTextColor(),
          interval: 0,
          rotate: 35
        },
        axisLine: {
          lineStyle: {
            color: this.getChartLineColor()
          }
        },
        axisTick: {
          alignWithLabel: true
        }
      },

      yAxis: {
        type: 'value',
        name: 'Tonnes',
        axisLabel: {
          color: this.getChartTextColor()
        },
        splitLine: {
          lineStyle: {
            color: this.getChartLineColor()
          }
        }
      },

      series: [
        {
          name: 'SMS',
          type: 'bar',
          barWidth: '20%',
          data: SMS,
          itemStyle: {
            color: '#22c55e',
            borderRadius: [4, 4, 0, 0]
          }
        },

        {
          name: 'DIP',
          type: 'bar',
          barWidth: '20%',
          data: DIP,
          itemStyle: {
            color: '#22d3ee',
            borderRadius: [4, 4, 0, 0]
          }
        },

        {
          name: 'PCM',
          type: 'bar',
          barWidth: '20%',
          data: PCM,
          itemStyle: {
            color: '#f97316',
            borderRadius: [4, 4, 0, 0]
          }
        }
      ]
    });
  }

  private addHeaderToSheet(sheet: ExcelJS.Worksheet) {
    const headerRow = sheet.getRow(1);
    sheet.mergeCells('A1:E1');
    const cell = sheet.getCell('A1');
    cell.value = {
      richText: [
        {
          text: 'Real-Time Ladle Operations Analytics\n',
          font: { bold: true, size: 16, color: { argb: 'FFFFFFFF' }, name: 'Arial' }
        },
        {
          text: 'Steel Plant Hot Metal Tracking System',
          font: { italic: true, size: 12, color: { argb: 'FFCBD5E1' }, name: 'Arial' }
        }
      ]
    };
    cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF4472C4' } };
    cell.alignment = { vertical: 'middle', horizontal: 'center', wrapText: true };
    headerRow.height = 45;

    const now = new Date();
    const day = String(now.getDate()).padStart(2, '0');
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const year = now.getFullYear();
    let hours = now.getHours();
    const minutes = String(now.getMinutes()).padStart(2, '0');
    const ampm = hours >= 12 ? 'PM' : 'AM';
    hours = hours % 12 || 12;
    const dateStr = `${day}-${month}-${year} ${hours}:${minutes} ${ampm}`;

    const dateRow = sheet.getRow(2);
    dateRow.getCell(1).value = `Report Generated On: ${dateStr}`;
    dateRow.font = { name: 'Arial', size: 10, italic: true, color: { argb: 'FF333333' } };
    dateRow.alignment = { horizontal: 'center' };
    sheet.mergeCells('A2:E2');
    sheet.getRow(3).values = [];
  }

  async exportToExcel() {
    console.log('Starting Export...');
    const workbook = new ExcelJS.Workbook();
    workbook.creator = 'Ladle System';
    workbook.created = new Date();

    const kpiSheet = workbook.addWorksheet('Executive Summary');
    this.addHeaderToSheet(kpiSheet);

    const kpiHeaderRow = kpiSheet.getRow(4);
    kpiHeaderRow.values = ['Metric', 'Value', 'Note'];
    kpiHeaderRow.font = { bold: true, size: 12, color: { argb: 'FFFFFFFF' } };
    kpiHeaderRow.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF224060' } };
    kpiHeaderRow.alignment = { horizontal: 'center', vertical: 'middle' };

    this.kpis.forEach(kpi => {
      const addedRow = kpiSheet.addRow([
        kpi.title,
        kpi.value + (kpi.unit ? ' ' + kpi.unit : ''),
      ]);
      addedRow.alignment = { horizontal: 'center', vertical: 'middle' };
    });

    kpiSheet.getColumn(1).width = 25;
    kpiSheet.getColumn(2).width = 20;
    kpiSheet.getColumn(3).width = 25;

    const addSheetWithChart = (sheetName: string, headers: string[], rows: any[][], chartInstance: echarts.ECharts) => {
      if (!chartInstance) {
        console.error(`Chart instance missing for ${sheetName}`);
        return;
      }

      const sheet = workbook.addWorksheet(sheetName);
      this.addHeaderToSheet(sheet);

      const headerRow = sheet.getRow(4);
      headerRow.values = headers;
      headerRow.font = { bold: true, color: { argb: 'FFFFFFFF' } };
      headerRow.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF4472C4' } };
      headerRow.alignment = { horizontal: 'center', vertical: 'middle' };

      rows.forEach(r => {
        const addedRow = sheet.addRow(r);
        addedRow.alignment = { horizontal: 'center', vertical: 'middle' };
      });

      sheet.columns.forEach(col => { col.width = 20; });

      const bgColor = this.isDarkMode ? '#0b1422' : '#ffffff';

      try {
        const base64String = chartInstance.getDataURL({
          type: 'png',
          pixelRatio: 2,
          backgroundColor: bgColor,
          excludeComponents: ['toolbox']
        });

        if (base64String && base64String.length > 100) {
          const cleanBase64 = base64String.split(',')[1];
          const buffer = this.base64ToBuffer(cleanBase64);
          const imageId = workbook.addImage({
            buffer: buffer,
            extension: 'png',
          });

          const startRow = 4 + rows.length + 2;
          sheet.addImage(imageId, {
            tl: { col: 0, row: startRow },
            ext: { width: 600, height: 350 }
          });
        }
      } catch (err) {
        console.error(`Error processing chart ${sheetName}:`, err);
      }
    };

    addSheetWithChart('Ladle Status', ['Status', 'Count'], [['Active', this.ladleData.active], ['Idle', this.ladleData.idle]], this.ladleChartInstance);
    addSheetWithChart('Trip Analytics', ['Status', 'Count'], [['Completed', this.tripData.completed], ['Pending', this.tripData.pending]], this.tripChartInstance);
    addSheetWithChart('Area Dist', ['Area', 'Count'], this.areaData.map(d => [d.category, d.value]), this.areaChartInstance);
    addSheetWithChart('Active Trend', ['Time', 'Count'], this.activeLadleData.map(d => [d.time, d.value]), this.activeLadleChartInstance);
    addSheetWithChart('Trip Shift', ['Hour', 'Actual', 'Target'], this.tripShiftData.map(d => [d.hour, d.actual, d.target]), this.tripShiftChartInstance);
    addSheetWithChart('Retention', ['Time', 'BF', 'SMS', 'LRS'], this.retentionData.map(d => [d.time, d.bf, d.sms, d.lrs]), this.retentionChartInstance);
    addSheetWithChart('Production', ['Time', 'BF1', 'BF2', 'BF3'], this.productionData.map((d: any) => [d.category, d.bf1, d.bf2, d.bf3]), this.productionChartInstance);
    addSheetWithChart('Consumption', ['Time', 'SMS', 'DIP', 'PCM'], this.consumptionData.map((d: any) => [d.category, d.sms, d.dip, d.pcm]), this.consumptionChartInstance);
    addSheetWithChart('TAT', ['Ladle', 'Minutes'], this.tatData.map(d => [d.ladle, d.value]), this.tatChartInstance);

    const buffer = await workbook.xlsx.writeBuffer();

    const now = new Date();

    const datePart = now.toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });

    let hours = now.getHours();
    const ampm = hours >= 12 ? 'PM' : 'AM';
    hours = hours % 12;
    hours = hours ? hours : 12;
    const minutes = String(now.getMinutes()).padStart(2, '0');
    const seconds = String(now.getSeconds()).padStart(2, '0');
    const timePart = `${hours}_${minutes}_${seconds} ${ampm}`;

    const fileName = `Dashboard ${datePart}, ${timePart}.xlsx`;

    const blob = new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    fs.saveAs(blob, fileName);
    console.log('Export Complete');
  }

  // timeRanges = ['1H', '4H', '8H', '24H'];
  // selectedTime = '1H';
  // shifts = ['All Shifts', 'Shift A', 'Shift B', 'Shift C'];
  // selectedShift = 'All Shifts';

  setTripView(view: 'bar' | 'line') { this.tripView = view; this.updateTripShiftChart(); }

  loadKPIs() {
    return this.dashboardService.getKPIs().pipe(
      tap((res: any) => {
        this.kpis = (res || []).map((k: any) => ({
          title: k.Title,
          value: k.Value,
          unit: k.Unit,
          color: k.Color,
          icon: k.Icon,
          trend: k.Trend
        }));

        this.updateLadleChart();
        this.updateTripChart();
        this.updateAreaChart();
      }),
      catchError(err => {
        console.error('Error fetching KPIs', err);
        return of([]);
      })
    );
  }

  loadAnalyticalDashboard() {
    return this.dashboardService.getAnalyticalDashboardData().pipe(
      tap((res: any) => {
        console.log('FULL RESPONSE:', res);

        if (res && res.status && res.data) {
          this.analyticalData = res.data;
          console.log('Dashboard Data:', this.analyticalData);
          this.locationsData = this.analyticalData.LocationData;
          console.log('Location Data:', this.locationsData);
          if (this.areaChartInstance) {
            this.updateAreaChart();
          }
        } else {
          console.error('API Error in Analytical Dashboard:', res?.message);
        }
      }),
      catchError(err => {
        console.error('HTTP Error in Analytical Dashboard:', err);
        return of(null);
      })
    );
  }

  loadChart() {
    return this.dashboardService.getLadleChart().pipe(
      tap(res => {
        this.activeLadleDataInUsed = res || [];
        if (this.activeLadleChartInstance) {
          this.updateActiveLadleChart();
        }
      }),
      catchError(err => {
        console.error('Error in loadChart:', err);
        return of([]);
      })
    );
  }

  hourlyData: any[] = [];

  loadHourlyData() {
    return this.dashboardService.getHourlyProductionConsumptionReport().pipe(
      tap((res: any) => {
        this.hourlyData = res || [];
        console.log('Hourly Data:', this.hourlyData);

        this.updateProductionChart();
        this.updateConsumptionChart();
      }),
      catchError(err => {
        console.error('Error in loadHourlyData:', err);
        return of([]);
      })
    );
  }

  formatHour(hourSlot?: string): string {
    if (!hourSlot) return '';
    if (hourSlot.length >= 16) {
      return hourSlot.substring(11, 16);
    }
    return hourSlot;
  }
}

export interface KPI {
  title: string;
  value: string;
  unit?: string;
  color: string;
  icon: string;
  trend: string;
}