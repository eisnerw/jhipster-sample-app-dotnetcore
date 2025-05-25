import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import SharedModule from 'app/shared/shared.module';

import { INamedQuery } from '../named-query.model';

@Component({
  standalone: true,
  selector: 'jhi-named-query-detail',
  templateUrl: './named-query-detail.component.html',
  imports: [CommonModule, RouterModule, FontAwesomeModule, SharedModule],
})
export class NamedQueryDetailComponent implements OnInit {
  namedQuery: INamedQuery | null = null;

  protected readonly activatedRoute = inject(ActivatedRoute);

  ngOnInit(): void {
    this.activatedRoute.data.subscribe(({ namedQuery }) => {
      this.namedQuery = namedQuery;
    });
  }

  previousState(): void {
    window.history.back();
  }
}
