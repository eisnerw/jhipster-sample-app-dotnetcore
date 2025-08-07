import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, filter, map, switchMap, of } from 'rxjs';
import { HttpResponse } from '@angular/common/http';
import { finalize } from 'rxjs/operators';

import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import SharedModule from 'app/shared/shared.module';

import { INamedQuery } from '../named-query.model';
import { NamedQueryService } from '../service/named-query.service';
import {
  NamedQueryFormService,
  NamedQueryFormGroup,
} from './named-query-form.service';
import { AccountService } from 'app/core/auth/account.service';

@Component({
  standalone: true,
  selector: 'jhi-named-query-update',
  templateUrl: './named-query-update.component.html',
  imports: [
    FormsModule,
    ReactiveFormsModule,
    FontAwesomeModule,
    SharedModule
],
})
export class NamedQueryUpdateComponent implements OnInit {
  isSaving = false;
  namedQuery: INamedQuery | null = null;
  editForm: NamedQueryFormGroup;
  isAdmin = false;

  protected readonly namedQueryService = inject(NamedQueryService);
  protected readonly namedQueryFormService = inject(NamedQueryFormService);
  protected readonly activatedRoute = inject(ActivatedRoute);
  protected readonly router = inject(Router);
  protected readonly accountService = inject(AccountService);

  constructor() {
    this.editForm = this.namedQueryFormService.createNamedQueryFormGroup();
  }

  ngOnInit(): void {
    this.accountService.identity().subscribe((account) => {
      if (account) {
        this.isAdmin = this.accountService.hasAnyAuthority('ROLE_ADMIN');
      }
    });

    this.activatedRoute.data
      .pipe(
        map(({ namedQuery }) => namedQuery as INamedQuery),
        switchMap((namedQuery) => {
          this.namedQuery = namedQuery;
          this.editForm =
            this.namedQueryFormService.createNamedQueryFormGroup(namedQuery);
          const formNamedQuery = this.namedQueryFormService.getNamedQuery(
            this.editForm,
          );
          return of(formNamedQuery as INamedQuery);
        }),
      )
      .subscribe((namedQuery) => {
        this.updateForm(namedQuery);
      });
  }

  previousState(): void {
    window.history.back();
  }

  save(): void {
    this.isSaving = true;
    const namedQuery = this.namedQueryFormService.getNamedQuery(this.editForm);
    if (namedQuery.id !== null) {
      this.subscribeToSaveResponse(this.namedQueryService.update(namedQuery));
    } else {
      this.subscribeToSaveResponse(this.namedQueryService.create(namedQuery));
    }
  }

  protected subscribeToSaveResponse(
    result: Observable<HttpResponse<INamedQuery>>,
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

  protected updateForm(namedQuery: INamedQuery): void {
    this.namedQuery = namedQuery;
    this.namedQueryFormService.resetForm(this.editForm, namedQuery);
  }
}
