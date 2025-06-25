import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Observable } from 'rxjs';

import {
  SuperTable,
  ColumnConfig,
} from 'app/shared/SuperTable/super-table.component';
import { IBirthday } from '../birthday.model';
import { DataLoader, FetchFunction } from 'app/shared/data-loader';
import { BirthdayService } from '../service/birthday.service';

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

  dataLoader: DataLoader<IBirthday>;

  constructor(private birthdayService: BirthdayService) {
    const fetchFunction: FetchFunction<IBirthday> = (queryParams: any) => {
      return this.birthdayService.query(queryParams);
    };
    this.dataLoader = new DataLoader<IBirthday>(fetchFunction);
  }

  ngOnInit(): void {
    const filter = { luceneQuery: `fname:"${this.groupName}"` };
    this.dataLoader.load(50, 'lname', true, filter);
  }
}
