import {
  Component,
  Input,
  OnInit,
  TemplateRef,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { SuperTable, ColumnConfig } from './super-table.component';
import { DataLoader } from '../data-loader';

@Component({
  selector: 'jhi-super-table-group-detail',
  standalone: true,
  imports: [CommonModule, SuperTable],
  templateUrl: './group-detail.component.html',
})
export class GroupDetailComponent implements OnInit {
  @Input() groupName!: string;
  @Input() columns!: ColumnConfig[];
  @Input() parent!: any;
  @Input() groupQuery!: (group: string) => DataLoader<any>;
  @Input() expandedRowTemplate: TemplateRef<any> | undefined;

  dataLoader!: DataLoader<any>;
  @ViewChild(SuperTable) superTableComponent!: SuperTable;

  ngOnInit(): void {
    this.dataLoader = this.groupQuery(this.groupName);
  }

  applySort(event: any): void {
    this.superTableComponent.applySort(event);
  }

  applyFilter(event: any): void {
    this.superTableComponent.applyFilter(event);
  }
}
