import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class LayoutService {
  private sidebarOpenSubject = new BehaviorSubject<boolean>(true); 
  sidebarOpen$ = this.sidebarOpenSubject.asObservable();

  toggle(): void {
    this.sidebarOpenSubject.next(!this.sidebarOpenSubject.value);
  }

  close(): void {
    this.sidebarOpenSubject.next(false);
  }

  get isOpen(): boolean {
    return this.sidebarOpenSubject.value;
  }
}