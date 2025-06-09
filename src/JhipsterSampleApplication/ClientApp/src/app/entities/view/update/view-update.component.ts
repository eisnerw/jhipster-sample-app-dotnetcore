import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import SharedModule from 'app/shared/shared.module';
import { IView } from '../view.model';
import { ViewService } from '../service/view.service';
import { finalize } from 'rxjs/operators';
import { HttpResponse } from '@angular/common/http';

@Component({
  standalone: true,
  selector: 'jhi-view-update',
  templateUrl: './view-update.component.html',
  imports: [SharedModule, RouterModule, FormsModule],
})
export class ViewUpdateComponent implements OnInit {
  view: IView = {
    name: '',
    field: '',
    aggregation: '',
    query: '',
    categoryQuery: '',
    script: '',
    domain: 'birthdays',
  };
  isSaving = false;
  views: IView[] = [];

  constructor(
    protected viewService: ViewService,
    protected activatedRoute: ActivatedRoute,
    protected router: Router,
  ) {}

  ngOnInit(): void {
    this.activatedRoute.data.subscribe(({ view }) => {
      if (view) {
        this.view = view;
      }
      if (!this.view.domain) {
        this.view.domain = 'birthdays';
      }
    });
    this.viewService.query().subscribe((res: HttpResponse<IView[]>) => {
      this.views = res.body ?? [];
    });
  }

  previousState(): void {
    window.history.back();
  }

  save(): void {
    this.isSaving = true;
    if (this.view.id) {
      this.viewService.update(this.view).subscribe({
        next: () => this.onSaveSuccess(),
        error: () => this.onSaveError(),
      });
    } else {
      this.viewService.create(this.view).subscribe({
        next: () => this.onSaveSuccess(),
        error: () => this.onSaveError(),
      });
    }
  }

  protected onSaveSuccess(): void {
    this.isSaving = false;
    this.previousState();
  }

  protected onSaveError(): void {
    this.isSaving = false;
  }
}
