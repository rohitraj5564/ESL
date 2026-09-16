import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class DataService {
  private dataSubject = new BehaviorSubject<string>('');
  data$: Observable<string> = this.dataSubject.asObservable();

  setData(data: string): void {
    this.dataSubject.next(data);
    localStorage.setItem('selectedLocation', data);
  }

  getData(): string {
    return this.dataSubject.value || localStorage.getItem('selectedLocation') || '';
  }
}
