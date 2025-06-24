import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Observable } from 'rxjs';

import {
  SuperTable,
  ColumnConfig,
} from 'app/shared/SuperTable/super-table.component';
import { IBirthday } from '../birthday.model';
import { BirthdayDataLoader } from './birthday-data-loader';
import { BirthdayDataLoaderFactory } from './birthday-data-loader.factory';

@Component({
  selector: 'jhi-birthday-group-detail',
  standalone: true,
  imports: [CommonModule, SuperTable],
  templateUrl: './birthday-group-detail.component.html',
})
export class BirthdayGroupDetailComponent implements OnInit {
  @Input() groupName!: string;
  @Input() columns!: ColumnConfig[];

  dataLoader: BirthdayDataLoader;
  birthdays$: Observable<IBirthday[]>;
  isLoading$: Observable<boolean>;
  loadingMessage$: Observable<string>;

  constructor(private dataLoaderFactory: BirthdayDataLoaderFactory) {
    this.dataLoader = this.dataLoaderFactory.create();
    this.birthdays$ = this.dataLoader.data$;
    this.isLoading$ = this.dataLoader.loading$;
    this.loadingMessage$ = this.dataLoader.loadingMessage$;
  }

  ngOnInit(): void {
    const filter = { query: `fname:"${this.groupName}"` };
    this.dataLoader.load(50, 'id', true, filter);
  }
}
