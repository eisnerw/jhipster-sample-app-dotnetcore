import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

import { ISelector } from '../selector.model';

@Component({
  selector: 'jhi-selector-detail',
  templateUrl: './selector-detail.component.html',
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
