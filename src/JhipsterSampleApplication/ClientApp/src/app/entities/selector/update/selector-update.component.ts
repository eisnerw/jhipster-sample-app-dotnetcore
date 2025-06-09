import { Component, OnInit } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
import { UntypedFormBuilder } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Observable } from 'rxjs';
import { finalize, map } from 'rxjs/operators';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { ReactiveFormsModule } from '@angular/forms';
import SharedModule from 'app/shared/shared.module';

import { ISelector, Selector } from '../selector.model';
import { SelectorService } from '../service/selector.service';
import { NamedQueryService } from '../../named-query/service/named-query.service';
import { INamedQuery } from '../../named-query/named-query.model';

@Component({
  standalone: true,
  selector: 'jhi-selector-update',
  templateUrl: './selector-update.component.html',
  imports: [
    CommonModule,
    RouterModule,
    FontAwesomeModule,
    ReactiveFormsModule,
    SharedModule,
  ],
})
export class SelectorUpdateComponent implements OnInit {
  isSaving = false;
  namedQueries: INamedQuery[] = [];

  editForm = this.fb.group({
    id: [],
    name: [],
    rulesetName: [],
    action: [],
    actionParameter: [],
    description: [],
  });

  constructor(
    protected selectorService: SelectorService,
    protected activatedRoute: ActivatedRoute,
    protected namedQueryService: NamedQueryService,
    protected fb: UntypedFormBuilder,
  ) {}

  ngOnInit(): void {
    this.activatedRoute.data.subscribe({
      next: ({ selector }) => {
        if (selector) {
          this.updateForm(selector);
        }
        this.namedQueryService
          .query({ name: '', owner: 'SYSTEM' })
          .pipe(map((res: HttpResponse<INamedQuery[]>) => res.body ?? []))
          .subscribe({
            next: (resBody: INamedQuery[]) => {
              this.namedQueries = resBody;
            },
          });
      },
    });
  }

  previousState(): void {
    window.history.back();
  }

  save(): void {
    this.isSaving = true;
    const selector = this.createFromForm();
    if (selector.id) {
      this.subscribeToSaveResponse(this.selectorService.update(selector));
    } else {
      this.subscribeToSaveResponse(this.selectorService.create(selector));
    }
  }

  updateForm(selector: ISelector): void {
    this.editForm.patchValue({
      id: selector.id as any,
      name: selector.name as any,
      rulesetName: selector.rulesetName as any,
      action: selector.action as any,
      actionParameter: selector.actionParameter as any,
      description: selector.description as any,
    });
  }

  protected subscribeToSaveResponse(
    result: Observable<HttpResponse<ISelector>>,
  ): void {
    result.pipe(finalize(() => this.onSaveFinalize())).subscribe({
      next: () => this.onSaveSuccess(),
      error: () => this.onSaveError(),
    });
  }

  protected onSaveSuccess(): void {
    this.previousState();
  }

  protected onSaveError(): void {
    // Api for inheritance.
  }

  protected onSaveFinalize(): void {
    this.isSaving = false;
  }
  protected createFromForm(): ISelector {
    const selector = new Selector();
    selector.id = this.editForm.get(['id'])!.value;
    selector.name = this.editForm.get(['name'])!.value;
    selector.rulesetName = this.editForm.get(['rulesetName'])!.value;
    selector.action = this.editForm.get(['action'])!.value;
    selector.actionParameter = this.editForm.get(['actionParameter'])!.value;
    selector.description = this.editForm.get(['description'])!.value;
    return selector;
  }
}
