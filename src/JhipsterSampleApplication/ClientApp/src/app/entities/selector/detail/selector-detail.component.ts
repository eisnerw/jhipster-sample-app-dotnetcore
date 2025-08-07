import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

import { RouterModule } from '@angular/router';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import SharedModule from 'app/shared/shared.module';

import { ISelector } from '../selector.model';

@Component({
  standalone: true,
  selector: 'jhi-selector-detail',
  templateUrl: './selector-detail.component.html',
  imports: [RouterModule, FontAwesomeModule, SharedModule],
})
export class SelectorDetailComponent implements OnInit {
  selector: ISelector | null = null;

  constructor(protected activatedRoute: ActivatedRoute) {}

  ngOnInit(): void {
    this.activatedRoute.data.subscribe(({ selector }) => {
      this.selector = selector;
    });
  }

  previousState(): void {
    window.history.back();
  }
}
