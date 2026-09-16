import { Component, OnDestroy, OnInit } from '@angular/core';
import { MainserviceService } from '../service/mainservice.service';

@Component({
  selector: 'app-pending-request',
  standalone: false,
  templateUrl: './pending-request.component.html',
  styleUrl: './pending-request.component.css'
})
export class PendingRequestComponent implements OnInit, OnDestroy {

  /** One row per department: { locationName, ladleCount, createdDateTime, ladleNos } */
  pendingRequests: any[] = [];

  private refreshInterval: any;

  constructor(private service: MainserviceService) {}

  ngOnInit(): void {
    this.getLadleRequest();

    this.refreshInterval = setInterval(() => {
      this.getLadleRequest();
    }, 3000);
  }

  ngOnDestroy(): void {
    if (this.refreshInterval) {
      clearInterval(this.refreshInterval);
    }
  }

  getLadleRequest(): void {
    this.service.getLadleRequest().subscribe(
      (data: any) => {
        const allPending = data?.data?.pendingRequest || [];

        const filtered = allPending.filter((item: any) => {
          if (item.requestNo) return true;
          if (!item.requestNo &&
            (item.locationName === 'BF1' ||
             item.locationName === 'BF2' ||
             item.locationName === 'BF3')) return true;
          if (!item.requestNo &&
            (item.locationName === 'SMS' ||
             item.locationName === 'PCM' ||
             item.locationName === 'DIP')) return true;
          return false;
        });

        this.pendingRequests = this.groupByDepartment(filtered);
      },
      (error) => {
        console.error('Error fetching pending requests:', error);
      }
    );
  }

  private groupByDepartment(rows: any[]): any[] {
    const map = new Map<string, any>();

    rows.forEach((row: any) => {
      const dept = row.locationName || 'NA';
      const qty  = row.ladleNo ? 1 : (Number(row.nosOfLadle) || 0);
      const time = row.createdDateTime ? new Date(row.createdDateTime).getTime() : 0;

      const existing = map.get(dept);

      if (existing) {
        existing.ladleCount += qty;
        if (row.ladleNo) { existing.ladleNos.push(row.ladleNo); }

        // keep the newest request time for this department
        if (time > existing.sortTime) {
          existing.sortTime = time;
          existing.createdDateTime = row.createdDateTime;
        }
      } else {
        map.set(dept, {
          locationName: dept,
          ladleCount: qty,
          createdDateTime: row.createdDateTime,
          sortTime: time,
          ladleNos: row.ladleNo ? [row.ladleNo] : []
        });
      }
    });

    return Array.from(map.values())
                .sort((a, b) => b.sortTime - a.sortTime);
  }

  /** Total ladles across all departments — shown in the header badge. */
  get totalLadles(): number {
    return this.pendingRequests.reduce(
      (sum, row) => sum + (row.ladleCount || 0), 0
    );
  }

  /** Tooltip on the count cell, e.g. "Ladle5, Ladle6" */
  ladleTooltip(row: any): string {
    return row.ladleNos && row.ladleNos.length
      ? row.ladleNos.join(', ')
      : '';
  }
}