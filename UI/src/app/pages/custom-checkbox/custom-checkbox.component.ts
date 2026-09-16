import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'app-custom-checkbox',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './custom-checkbox.component.html',
  styleUrl: './custom-checkbox.component.css'
})
export class CustomCheckboxComponent {
  @Input() checked: boolean = false;
  @Input() indeterminate: boolean = false;
  @Output() checkedChange = new EventEmitter<boolean>();

  toggleCheck() {
    this.checked = !this.checked;
    this.indeterminate = false;
    this.checkedChange.emit(this.checked);
  }
}