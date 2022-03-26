import { Component, OnInit } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
// eslint-disable-next-line @typescript-eslint/no-unused-vars
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

import { IDepartment, Department } from 'app/shared/model/department.model';
import { DepartmentService } from './department.service';
import { ISelector } from 'app/shared/model/selector.model';
import { SelectorService } from 'app/entities/selector/selector.service';

@Component({
  selector: 'jhi-department-update',
  templateUrl: './department-update.component.html',
})
export class DepartmentUpdateComponent implements OnInit {
  isSaving = false;
  selectors: ISelector[] = [];

  editForm = this.fb.group({
    id: [],
    departmentName: [null, [Validators.required]],
    selectorId: [],
  });

  constructor(
    protected departmentService: DepartmentService,
    protected selectorService: SelectorService,
    protected activatedRoute: ActivatedRoute,
    private fb: FormBuilder
  ) {}

  ngOnInit(): void {
    this.activatedRoute.data.subscribe(({ department }) => {
      this.updateForm(department);

      this.selectorService
        .query({ filter: 'department-is-null' })
        .pipe(
          map((res: HttpResponse<ISelector[]>) => {
            return res.body || [];
          })
        )
        .subscribe((resBody: ISelector[]) => {
          if (!department.selectorId) {
            this.selectors = resBody;
          } else {
            this.selectorService
              .find(department.selectorId)
              .pipe(
                map((subRes: HttpResponse<ISelector>) => {
                  return subRes.body ? [subRes.body].concat(resBody) : resBody;
                })
              )
              .subscribe((concatRes: ISelector[]) => (this.selectors = concatRes));
          }
        });
    });
  }

  updateForm(department: IDepartment): void {
    this.editForm.patchValue({
      id: department.id,
      departmentName: department.departmentName,
      selectorId: department.selectorId,
    });
  }

  previousState(): void {
    window.history.back();
  }

  save(): void {
    this.isSaving = true;
    const department = this.createFromForm();
    if (department.id !== undefined) {
      this.subscribeToSaveResponse(this.departmentService.update(department));
    } else {
      this.subscribeToSaveResponse(this.departmentService.create(department));
    }
  }

  private createFromForm(): IDepartment {
    return {
      ...new Department(),
      id: this.editForm.get(['id'])!.value,
      departmentName: this.editForm.get(['departmentName'])!.value,
      selectorId: this.editForm.get(['selectorId'])!.value,
    };
  }

  protected subscribeToSaveResponse(result: Observable<HttpResponse<IDepartment>>): void {
    result.subscribe(
      () => this.onSaveSuccess(),
      () => this.onSaveError()
    );
  }

  protected onSaveSuccess(): void {
    this.isSaving = false;
    this.previousState();
  }

  protected onSaveError(): void {
    this.isSaving = false;
  }

  trackById(index: number, item: ISelector): any {
    return item.id;
  }
}
