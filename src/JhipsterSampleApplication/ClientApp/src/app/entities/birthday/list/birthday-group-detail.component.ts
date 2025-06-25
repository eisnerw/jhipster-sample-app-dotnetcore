import {
  Component,
  Input,
  OnInit,
  TemplateRef,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Observable } from 'rxjs';

import {
  SuperTable,
  ColumnConfig,
} from 'app/shared/SuperTable/super-table.component';
import { IBirthday } from '../birthday.model';
import { DataLoader, FetchFunction } from 'app/shared/data-loader';
import { BirthdayService } from '../service/birthday.service';
import { TableColResizeEvent } from 'primeng/table';

@Component({
  selector: 'jhi-birthday-group-detail',
  standalone: true,
  imports: [CommonModule, SuperTable],
  templateUrl: './birthday-group-detail.component.html',
})
export class BirthdayGroupDetailComponent implements OnInit {
  @Input() groupName!: string;
  @Input() columns!: ColumnConfig[];
  @Input() parent!: any;
  @Input() expandedRowTemplate: TemplateRef<any> | undefined;

  dataLoader: DataLoader<IBirthday>;
  @ViewChild(SuperTable) superTableComponent!: SuperTable;

  constructor(private birthdayService: BirthdayService) {
    const fetchFunction: FetchFunction<IBirthday> = (queryParams: any) => {
      return this.birthdayService.query(queryParams);
    };
    this.dataLoader = new DataLoader<IBirthday>(fetchFunction);
  }

  ngOnInit(): void {
    const filter = { query: `fname:"${this.groupName}"` };
    this.dataLoader.load(50, 'lname', true, filter);
  }

  applySort(event: any): void {
    this.superTableComponent.applySort(event);
  }

  applyFilter(event: any): void {
    this.superTableComponent.applyFilter(event);
  }

  applyColResize(event: TableColResizeEvent): void {
    this.superTableComponent.applyColResize(event);
  }
}
