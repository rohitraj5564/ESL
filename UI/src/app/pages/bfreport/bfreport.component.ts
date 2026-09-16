import { Component, Input, OnInit, ViewChild, AfterViewInit } from '@angular/core';
import { MatPaginator } from '@angular/material/paginator';
import { MatSort } from '@angular/material/sort';
import { MatTableDataSource } from '@angular/material/table';

@Component({
  selector: 'app-bfreport',
  templateUrl: './bfreport.component.html',
  styleUrls: ['./bfreport.component.css'],
  standalone: false
})
export class BfreportComponent implements OnInit, AfterViewInit {
  @Input() public inputdata: any;
  displayedColumns: string[] = ['Name', 'LastLocationDateTimeSTR', 'LastLocationName', 'TimeSpentSTR'];
  public dataSource = new MatTableDataSource<any>([]);

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  constructor() {}

  ngOnInit(): void {
    if (this.inputdata) {
      this.dataSource.data = this.inputdata;
    }
  }

  ngAfterViewInit() {
    this.dataSource.paginator = this.paginator;
    this.dataSource.sort = this.sort;
  }
}
