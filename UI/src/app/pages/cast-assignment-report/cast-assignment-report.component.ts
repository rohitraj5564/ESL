import {
  CommonModule
} from '@angular/common';

import {
  ChangeDetectorRef,
  Component,
  OnInit
} from '@angular/core';

import { MatTableModule } from '@angular/material/table';

import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';

@Component({
  selector: 'app-cast-assignment-report',
  standalone: true,
  imports: [
    CommonModule,
    MatTableModule
  ],
  templateUrl: './cast-assignment-report.component.html',
  styleUrl: './cast-assignment-report.component.css'
})
export class CastAssignmentReportComponent implements OnInit {

  displayedColumns: string[] = [
    'Tran_ID',
    'GrossDate',
    'GrossTime',
    'LaddleNumber',
    'SenderPlant',
    'NetWT',
    'GrossWT',
    'TareWT',
    'receiverPlant',
    'CastNumber'
  ];

  dataSource: any[] = [];

  constructor(
    private cdr: ChangeDetectorRef
  ) { }

  ngOnInit(): void {
    this.loadReportData();
  }

  loadReportData(): void {
    const loginUser = JSON.parse(
      sessionStorage.getItem('loginUser') || '{}'
    );

    const userPlant =
      loginUser?.UserName
        ?.trim()
        ?.toUpperCase() || '';

    // API integration will be added next.
    // The existing Project 2 filtering logic is preserved.
    this.dataSource = [];

    this.cdr.detectChanges();

    console.log('Report loaded for:', userPlant);
  }

  exportPDF(): void {

    const pdf = new jsPDF('landscape');

    pdf.setFontSize(18);

    pdf.text(
      'Blast Furnace Production Report',
      14,
      15
    );

    autoTable(pdf, {
      startY: 25,

      head: [[
        'Transaction No',
        'Date',
        'Time',
        'Laddle No',
        'Sender Plant',
        'Net WT',
        'Gross WT',
        'Tare WT',
        'Cast No'
      ]],

      body: this.dataSource.map(
        (row: any) => [
          row.Tran_ID,

          new Date(
            row.GrossDateTime
          ).toLocaleDateString('en-GB'),

          new Date(
            row.GrossDateTime
          ).toLocaleTimeString(
            'en-IN',
            {
              hour: '2-digit',
              minute: '2-digit'
            }
          ),

          row.LaddleNumber,
          row.SenderPlant,
          row.NetWT,
          row.GrossWT,
          row.TareWT,
          row.CastNumber
        ]
      ),

      styles: {
        fontSize: 10
      },

      headStyles: {
        fillColor: [49, 46, 129]
      }
    });

    pdf.save(
      'BlastFurnaceReport.pdf'
    );
  }
}