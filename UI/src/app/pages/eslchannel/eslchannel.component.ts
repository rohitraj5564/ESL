import { Component, EventEmitter, Input, OnInit, Output, SimpleChanges, ViewChild, TemplateRef } from '@angular/core';
import { MatPaginator } from '@angular/material/paginator';
import { MatTableDataSource } from '@angular/material/table';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { Router } from '@angular/router';
import { MainserviceService } from '../service/mainservice.service';
import { DataService } from '../service/data.service';

export class LadleAssign {
  ladleNo: string = '';
  AssignedProductionUit: string = '';
  AssignedProductionUnitID: number = 0;
  AssignedDateTime: Date = new Date();
  SourceLocationID: number = 0;
  SourceLocationName: string = '';
  State: number = 0;
  LadleName: string = '';
  SourceInDateTime: Date = new Date();
  UID: number = 0;
}

@Component({
  selector: 'app-eslchannel',
  templateUrl: './eslchannel.component.html',
  styleUrls: ['./eslchannel.component.css', '../esldashboard/esldashboard.component.css'],
  standalone: false
})
export class EslchannelComponent implements OnInit {
  @Output() refreshDataEvent = new EventEmitter<string>();
  @Input() public eslLocationData: any = [];
  @Input() public divId: string = '';
  @Input() public ellipsId: string = '';
  @Input() public className: string = '';
  @Input() public channelName: string = '';
  @Input() public isLink: boolean = false;

  public isBfGroup: boolean = false;
  public iswgGroup: boolean = false;
  public issmsGroup: boolean = false;

  public LadleName: string = '';
  public casteNo: string = 'NA';
  public cp: string = 'NA';
  public sip: string = 'NA';
  public mnp: string = 'NA';
  public sp: string = 'NA';
  public pp: string = 'NA';
  public tip: string = 'NA';
  public crp: string = 'NA';
  public spp: string = 'NA';
  public Analyst: string = 'NA';
  public NewCastNumber: string = 'NA';
  public assignValue: LadleAssign[] = [];
  public dataSource = new MatTableDataSource<any>([]);
  public dataSourceLadleDetails = new MatTableDataSource<any>([]);
  public selectedValue: string = '';
  public isSuccessResult = false;
  public isFailResult = false;
  public RoleName: string = '';

  displayedColumns: string[] = ['LadleName', 'Unit'];
  displayedColumnsLadleDetails: string[] = ['LadleName', 'LastLocation', 'TimeSpent'];

  private currentDialogRef?: MatDialogRef<any>;

  constructor(
    private dataService: DataService,
    public mainService: MainserviceService,
    private dialog: MatDialog,
    private router: Router
  ) {}

  ngOnInit() {
    const role = sessionStorage.getItem('RoleName') || localStorage.getItem('RoleName') || '';
    this.RoleName = role;
  }

  ngOnChanges(changes: SimpleChanges) {}

  public readonly tatDefinition: string = 'Average Turnaround Time (TAT): The total cycle time taken for a ladle from dispatch to return.';
  public readonly holdingDefinition: string = 'Average Holding Time: The duration the ladle has been held at this station.';

  public getLadeleCount(): number {
    if (this.eslLocationData && this.eslLocationData.LadleList) {
      return this.eslLocationData.LadleList.length;
    }
    return 0;
  }

  public getAvgTat(): string {
    const val = this.eslLocationData?.AverageTATSTR;
    if (val && val !== '0' && val !== '00:00:00' && val !== '00:00' && val.trim() !== '') {
      const parts = val.split(':');
      if (parts.length >= 2) {
        return `${parts[0].padStart(2, '0')}:${parts[1].padStart(2, '0')}`;
      }
      return val;
    }
    return '00:00';
  }

  public HoldingSTR(): string {
    const val = this.eslLocationData?.AverageHoldTimeSTR;
    if (val && val !== '0' && val !== '00:00:00' && val !== '00:00' && val.trim() !== '') {
      const parts = val.split(':');
      if (parts.length >= 2) {
        return `${parts[0].padStart(2, '0')}:${parts[1].padStart(2, '0')}`;
      }
      return val;
    }
    return '00:00';
  }

  public getPreviousStation(element: any): string {
    if (element) {
      if (element.LastLocationName && element.LastLocationName !== 'Standard Loop' && element.LastLocationName.trim() !== '') {
        return element.LastLocationName;
      }
      if (element.PreviousLocation && element.PreviousLocation !== 'Standard Loop') {
        return element.PreviousLocation;
      }
      if (element.SenderLocation) {
        return element.SenderLocation;
      }
    }
    if (['SMS', 'DIP', 'PCM', 'LRS'].includes(this.channelName)) {
      return 'Weighbridge';
    } else if (this.channelName === 'Weighbridge' || this.channelName === 'In Transit') {
      return 'Blast Furnace';
    } else if (['BF 1', 'BF 2', 'BF 3'].includes(this.channelName)) {
      return 'Yard / Return';
    }
    return '-';
  }

  public compareTimeSpan(value: string): boolean {
    if (!value) return false;
    const splitArr = value.split(':');
    if (splitArr.length > 0) {
      const minutes = parseInt(splitArr[0], 10);
      return minutes > 30;
    }
    return false;
  }

  public mousehoverEvent(data: any) {
    if (data.LimsData) {
      this.casteNo = data.LimsData.CastNo ? data.LimsData.CastNo : 'NA';
      this.cp = data.LimsData.C ? data.LimsData.C : 'NA';
      this.sip = data.LimsData.Si ? data.LimsData.Si : 'NA';
      this.mnp = data.LimsData.Mn ? data.LimsData.Mn : 'NA';
      this.sp = data.LimsData.S ? data.LimsData.S : 'NA';
      this.pp = data.LimsData.P ? data.LimsData.P : 'NA';
      this.tip = data.LimsData.Ti ? data.LimsData.Ti : 'NA';
      this.crp = data.LimsData.Cr ? data.LimsData.Cr : 'NA';
      this.spp = data.LimsData.S_P ? data.LimsData.S_P : 'NA';
      this.Analyst = data.LimsData.Analyst ? data.LimsData.Analyst : 'NA';
      this.NewCastNumber = data.LimsData.NewCastNumber ? data.LimsData.NewCastNumber : 'NA';
    } else {
      this.casteNo = 'NA';
      this.cp = 'NA';
      this.sip = 'NA';
      this.mnp = 'NA';
      this.sp = 'NA';
      this.pp = 'NA';
      this.tip = 'NA';
      this.crp = 'NA';
      this.spp = 'NA';
      this.Analyst = 'NA';
      this.NewCastNumber = 'NA';
    }
  }

  public AssignClick() {
    this.mainService.setAssignLadle(this.assignValue).subscribe({
      next: (res) => {
        if (res) {
          this.isSuccessResult = true;
          this.isFailResult = false;
          this.refreshDataEvent.emit();
          setTimeout(() => {
            if (this.currentDialogRef) this.currentDialogRef.close();
          }, 1500);
        } else {
          this.isFailResult = true;
          this.isSuccessResult = false;
        }
      },
      error: () => {
        this.isFailResult = true;
        this.isSuccessResult = false;
      }
    });
  }

  open(content: TemplateRef<any>, channelName: string) {
    this.isBfGroup = ['BF 1', 'BF 2', 'BF 3'].includes(channelName);
    this.iswgGroup = channelName === 'Weighbridge';
    this.issmsGroup = ['SMS', 'DIP', 'PCM', 'LRS'].includes(channelName);

    if (this.eslLocationData && this.eslLocationData.LadleList) {
      this.dataSource.data = this.eslLocationData.LadleList;
    }
    this.currentDialogRef = this.dialog.open(content, { width: '800px', maxWidth: '95vw' });
  }

  openLadleDetails(ladleDetail: TemplateRef<any>, channelName: string) {
    if (this.eslLocationData && this.eslLocationData.LadleList) {
      this.dataSourceLadleDetails.data = this.eslLocationData.LadleList;
    }
    this.currentDialogRef = this.dialog.open(ladleDetail, { width: '800px', maxWidth: '95vw' });
  }

  openLadleAssign(ladleAssign: TemplateRef<any>, eslLocationData: any, channelName: string) {
    this.isSuccessResult = false;
    this.isFailResult = false;
    this.assignValue = [];

    if (eslLocationData && eslLocationData.LadleList) {
      eslLocationData.LadleList.forEach((element: any) => {
        const item = new LadleAssign();
        item.LadleName = element.Name;
        item.ladleNo = element.Name;
        item.AssignedProductionUit = element.AcceptedLocationName || 'Unassign';
        item.AssignedDateTime = new Date();
        item.SourceLocationID = eslLocationData.LocationId;
        item.SourceLocationName = channelName;
        item.State = 4;
        item.UID = element.UID;
        this.assignValue.push(item);
      });
    }

    this.dataSource.data = this.assignValue;
    this.currentDialogRef = this.dialog.open(ladleAssign, { width: '700px', maxWidth: '95vw' });
  }

  closeDialog() {
    if (this.currentDialogRef) {
      this.currentDialogRef.close();
    }
  }

  changeActiveMenu(loc: string) {
    this.dataService.setData(loc);
    this.router.navigate(['locationsummary']);
  }
}
