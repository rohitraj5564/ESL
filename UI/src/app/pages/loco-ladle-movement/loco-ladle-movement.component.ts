import { CommonModule } from '@angular/common';
import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';

import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

import { FooterComponent } from '../footer/footer.component';
import { ThemeService } from '../service/theme.service';
import { MainserviceService } from '../service/mainservice.service';
import { SessionTimeoutService } from '../service/session-timeout.service';

@Component({
  selector: 'app-loco-ladle-movement',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatIconModule,
    MatTooltipModule,
    FooterComponent
  ],
  templateUrl: './loco-ladle-movement.component.html',
  styleUrl: './loco-ladle-movement.component.css'
})
export class LocoLadleMovementComponent implements OnInit, OnDestroy {

  isDarkTheme = false;
  isLoading = false;
  private themeSubscription!: Subscription;
  private refreshInterval: any;

  locos: LocoCard[] = [];

  private readonly LOCO_COLORS: Record<string, string> = {
    'Loco1': '#22c55e',
    'Loco2': '#3b82f6',
    'Loco3': '#f97316',
    'Loco4': '#8b5cf6',
    'Loco5': '#06b6d4'
  };

  private readonly DEFAULT_COLORS = ['#22c55e', '#3b82f6', '#f97316', '#8b5cf6', '#06b6d4', '#ec4899', '#eab308'];

  constructor(
    private themeService: ThemeService,
    private service: MainserviceService,
    private sessionTimeoutService: SessionTimeoutService
  ) {}

  ngOnInit(): void {
    this.sessionTimeoutService.initSessionTimeout();

    this.themeSubscription = this.themeService.isDarkMode$.subscribe(
      (isDark) => {
        this.isDarkTheme = isDark;
      }
    );

    // Initial load — shows loading spinner on UI
    this.loadLocoLadleMapping();

    // Auto-refresh every 5s — silent, no loading spinner, only console log
    this.refreshInterval = setInterval(() => this.silentRefresh(), 5000);
  }

  ngOnDestroy(): void {
    this.themeSubscription?.unsubscribe();
    if (this.refreshInterval) {
      clearInterval(this.refreshInterval);
    }
  }

  /** Initial load & manual Refresh button — shows loading spinner on UI */
  loadLocoLadleMapping(): void {
    this.isLoading = true;

    this.service.getLatestLocoLadleMapping().subscribe({
      next: (response: any) => {
        console.log('GetLatestLocoLadleMapping Response:', response);

        if (response?.status && response?.data) {
          this.locos = this.buildLocoCards(response.data);
        } else {
          this.locos = this.buildLocoCards([]);
        }

        this.isLoading = false;
      },
      error: (err: any) => {
        console.error('GetLatestLocoLadleMapping Error:', err);
        this.locos = this.buildLocoCards([]);
        this.isLoading = false;
      }
    });
  }

  /** Auto-refresh — silent, no loading spinner, only console log */
  private silentRefresh(): void {
    this.service.getLatestLocoLadleMapping().subscribe({
      next: (response: any) => {
        console.log('[Auto-Refresh] LocoLadleMapping refreshed at', new Date().toLocaleTimeString());

        if (response?.status && response?.data) {
          this.locos = this.buildLocoCards(response.data);
        }
      },
      error: (err: any) => {
        console.error('[Auto-Refresh] LocoLadleMapping Error:', err);
      }
    });
  }

  private readonly STATIC_LOCOS = ['Loco1', 'Loco2', 'Loco3', 'Loco4', 'Loco5'];

  private resolveLocoKey(item: any): string {
    // 1. Try by Serial Number first (LocoAssetSerialNo from LocoLadleMappingLog)
    const serial = String(item.LocoSerialNo || item.LocoAssetSerialNo || '').trim();
    if (serial) {
      const serialMap: Record<string, string> = {
        '45': 'Loco1',
        '46': 'Loco2',
        '47': 'Loco3',
        '52': 'Loco3',
        '48': 'Loco4',
        '49': 'Loco5',
        '1': 'Loco1',
        '2': 'Loco2',
        '3': 'Loco3',
        '4': 'Loco4',
        '5': 'Loco5'
      };
      if (serialMap[serial]) return serialMap[serial];
    }

    // 2. Try by LocoName
    const rawName = (item.LocoName || '').trim();
    if (rawName) {
      const normalized = rawName.replace(/\s+/g, '');
      const matched = this.STATIC_LOCOS.find(l => l.toLowerCase() === normalized.toLowerCase());
      if (matched) return matched;
    }

    // 3. Try by Tag ID
    const tagId = (item.LocoTagId || '').trim();
    if (tagId) {
      const tagMap: Record<string, string> = {
        '1C030000002750534C202020': 'Loco1',
        '1C0300000012A4B7C202020': 'Loco2',
        '1C0300000058D91EC202020': 'Loco3',
        '1C03000000F3A762C202020': 'Loco4',
        '1C03000000B84E35C202020': 'Loco5'
      };
      if (tagMap[tagId]) return tagMap[tagId];
    }

    return '';
  }

  private buildLocoCards(data: any[]): LocoCard[] {
    // Always create 5 static cards
    const cards: LocoCard[] = this.STATIC_LOCOS.map((locoName, index) => ({
      id: index + 1,
      name: locoName,
      sourceLocation: '',
      color: this.LOCO_COLORS[locoName] || this.DEFAULT_COLORS[index],
      ladles: []
    }));

    // Group API items by loco name
    const locoGroups = new Map<string, any[]>();
    (data || []).forEach((item: any) => {
      const locoKey = this.resolveLocoKey(item);
      if (locoKey) {
        if (!locoGroups.has(locoKey)) {
          locoGroups.set(locoKey, []);
        }
        locoGroups.get(locoKey)!.push(item);
      }
    });

    // Process each loco group
    locoGroups.forEach((items, locoName) => {
      const card = cards.find(c => c.name === locoName);
      if (!card) return;

      // Separate NULL-ladle entries (loco moved without ladles) from real ladle entries
      const nullEntries = items.filter((i: any) => !i.LadleRfidTag && !i.LadleNumber);
      const ladleEntries = items.filter((i: any) => !!i.LadleRfidTag || !!i.LadleNumber);

      // Find the latest NULL entry timestamp
      const latestNullTime = nullEntries.length > 0
        ? Math.max(...nullEntries.map((i: any) => new Date(i.CreatedOn || i.LocoEventTime || 0).getTime()))
        : 0;

      // Find the latest ladle entry timestamp
      const latestLadleTime = ladleEntries.length > 0
        ? Math.max(...ladleEntries.map((i: any) => new Date(i.CreatedOn || i.LocoEventTime || 0).getTime()))
        : 0;

      if (latestNullTime > latestLadleTime && nullEntries.length > 0) {
        // The latest event for this loco is a NULL ladle entry —
        // loco has dropped all ladles. Show 0 ladles, only latest location.
        const latestNull = nullEntries.reduce((a: any, b: any) =>
          new Date(a.CreatedOn || a.LocoEventTime || 0).getTime() > new Date(b.CreatedOn || b.LocoEventTime || 0).getTime() ? a : b
        );
        card.sourceLocation = latestNull.TouchPointType || '';
        card.ladles = [];
      } else {
        // Normal case — show ladles from API
        ladleEntries.forEach((item: any) => {
          if (item.TouchPointType) {
            card.sourceLocation = item.TouchPointType;
          }

          const lNum = item.LadleNumber != null ? item.LadleNumber.toString() : '';
          const lName = item.LadleName || (lNum ? `Ladle${lNum}` : 'Ladle');

          // Avoid duplicate ladle entries for same ladle
          if (!card.ladles.some(l => l.ladleNumber === lNum && l.ladleName === lName)) {
            card.ladles.push({
              ladleNumber: lNum,
              ladleName: lName,
              confidence: item.ConfidenceScore || 0,
              status: item.MappingStatus || 'UNKNOWN',
              mappingTime: item.MappingStartTime || item.CreatedOn || ''
            });
          }
        });
      }
    });

    return cards;
  }

  /** Inserts a space between letters and digits: "Loco1" → "Loco 1", "Ladle5" → "Ladle 5" */
  formatName(name: any): string {
    if (!name) return '—';
    const str = String(name);
    return str.replace(/([a-zA-Z])(\d)/g, '$1 $2');
  }
}

export interface LadleInfo {
  ladleNumber: string;
  ladleName: string;
  confidence: number;
  status: string;
  mappingTime: string;
}

export interface LocoCard {
  id: number;
  name: string;
  sourceLocation: string;
  ladles: LadleInfo[];
  color: string;
}
