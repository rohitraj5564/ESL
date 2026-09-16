import { Component, OnInit, OnDestroy } from '@angular/core';
import { interval, Subscription } from 'rxjs';
import { MainserviceService } from '../service/mainservice.service';

@Component({
  selector: 'app-readerstatus',
  templateUrl: './readerstatus.component.html',
  styleUrls: ['./readerstatus.component.css'],
  standalone: false
})
export class ReaderstatusComponent implements OnInit, OnDestroy {
  public RData: any[] = [];
  public isLoading = false;
  private updateSubscription?: Subscription;

  constructor(public mainService: MainserviceService) {}

  ngOnInit(): void {
    this.isLoading = true;
    this.loadData();

    this.updateSubscription = interval(5000).subscribe(() => {
      this.loadDataSilent();
    });
  }

  ngOnDestroy(): void {
    if (this.updateSubscription) {
      this.updateSubscription.unsubscribe();
    }
  }

  loadData(): void {
    this.mainService.getStatusReaderReport().subscribe({
      next: (res: any) => {
        this.isLoading = false;
        this.RData = res?.data ? res.data : (Array.isArray(res) ? res : []);
      },
      error: (err) => {
        console.error('Error fetching reader status:', err);
        this.isLoading = false;
      }
    });
  }

  loadDataSilent(): void {
    this.mainService.getStatusReaderReport().subscribe({
      next: (res: any) => {
        this.RData = res?.data ? res.data : (Array.isArray(res) ? res : []);
        console.log(`[Reader Status] Internal auto-refresh completed at ${new Date().toLocaleTimeString()}`);
      },
      error: (err) => {
        console.warn('Silent refresh error:', err);
      }
    });
  }
}
