import { Component, Input, OnInit } from '@angular/core';

@Component({
  selector: 'app-wgreport',
  template: `<app-bfreport [inputdata]="inputdata"></app-bfreport>`,
  standalone: false
})
export class WgreportComponent implements OnInit {
  @Input() public inputdata: any;
  ngOnInit(): void {}
}
