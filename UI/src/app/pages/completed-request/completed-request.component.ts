import { Component, OnDestroy, OnInit } from '@angular/core';
import { MainserviceService } from '../service/mainservice.service';

@Component({
  selector: 'app-completed-request',
  standalone: false,
  templateUrl: './completed-request.component.html',
  styleUrl: './completed-request.component.css'
})
export class CompletedRequestComponent implements OnInit, OnDestroy {

  /** One row per department: { locationName, ladleCount, completedDateTime, ladleNos } */
  completedRequests: any[] = [];

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
        const now = new Date();
        const last24Hours = new Date(now.getTime() - 24 * 60 * 60 * 1000);
        const allCompleted = data?.data?.completedRequest || [];

        const filtered = allCompleted.filter((item: any) => {
          if (!item.completedDateTime) return false;
          const completedDate = new Date(item.completedDateTime);
          return completedDate >= last24Hours;
        });

        this.completedRequests = this.groupByDepartment(filtered);
      },
      (error) => {
        console.error('Error fetching completed requests:', error);
      }
    );
  }

  private groupByDepartment(rows: any[]): any[] {
    const map = new Map<string, any>();

    rows.forEach((row: any) => {
      const dept = row.locationName || 'NA';
      const qty  = row.ladleNo ? 1 : (Number(row.nosOfLadle) || 0);
      const time = row.completedDateTime ? new Date(row.completedDateTime).getTime() : 0;

      const existing = map.get(dept);

      if (existing) {
        existing.ladleCount += qty;
        if (row.ladleNo) { existing.ladleNos.push(row.ladleNo); }

        // keep the newest completion time for this department
        if (time > existing.sortTime) {
          existing.sortTime = time;
          existing.completedDateTime = row.completedDateTime;
        }
      } else {
        map.set(dept, {
          locationName: dept,
          ladleCount: qty,
          completedDateTime: row.completedDateTime,
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
    return this.completedRequests.reduce(
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