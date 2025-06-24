import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Observable, of } from 'rxjs';

import {
  SuperTable,
  ColumnConfig,
} from 'app/shared/SuperTable/super-table.component';
import { IBirthday } from '../birthday.model';
import { DataLoader, FetchFunction } from 'app/shared/data-loader';

@Component({
  selector: 'jhi-birthday-group-detail',
  standalone: true,
  imports: [CommonModule, SuperTable],
  templateUrl: './birthday-group-detail.component.html',
})
export class BirthdayGroupDetailComponent implements OnInit {
  @Input() group!: IBirthday[];
  @Input() columns!: ColumnConfig[];
  @Input() parent!: any;

  dataLoader: DataLoader<IBirthday>;

  constructor() {
    const fetchFunction: FetchFunction<IBirthday> = () => of();
    this.dataLoader = new DataLoader<IBirthday>(fetchFunction);
  }

  ngOnInit(): void {
    this.dataLoader.data$.next(this.group);
  }
}
