import { Component, Input, OnChanges } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  selector: 'app-wait',
  templateUrl: './wait.component.html',
  styleUrls: ['./wait.component.css'],
  standalone: false
})
export class WaitComponent implements OnChanges {
  @Input() msg: string = 'for us';
  @Input() err: any;

  constructor(private router: Router) {}

  ngOnChanges(): void {
    if (this.err && this.err.length > 0 && this.err[0].ERRORCODE === '101') {
      alert('Your session has expired due to inactivity. Please login again.');
      this.router.navigate(['login']);
    }
  }
}
