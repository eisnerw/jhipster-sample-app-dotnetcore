import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';

import SharedModule from 'app/shared/shared.module';
import { IView } from '../view.model';

@Component({
  standalone: true,
  selector: 'jhi-view-detail',
  templateUrl: './view-detail.component.html',
  imports: [SharedModule, RouterModule],
})
export class ViewDetailComponent implements OnInit {
  view: IView = {
    name: '',
    field: '',
    aggregation: '',
    query: '',
    categoryQuery: '',
    script: '',
    domain: 'birthdays',
  };

  constructor(protected activatedRoute: ActivatedRoute) {}

  ngOnInit(): void {
    this.activatedRoute.data.subscribe(({ view }) => {
      if (view) {
        this.view = view;
      }
      if (!this.view.domain) {
        this.view.domain = 'birthdays';
      }
    });
  }

  previousState(): void {
    window.history.back();
  }
}
