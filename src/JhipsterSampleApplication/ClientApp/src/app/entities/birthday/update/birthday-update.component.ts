import { Component, OnInit } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
import { FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { Observable } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { CommonModule } from '@angular/common';

import { IBirthday, Birthday } from '../birthday.model';
import { BirthdayService } from '../service/birthday.service';
import SharedModule from 'app/shared/shared.module';

@Component({
  selector: 'jhi-birthday-update',
  templateUrl: './birthday-update.component.html',
  standalone: true,
  imports: [SharedModule, RouterModule, ReactiveFormsModule, CommonModule],
})
export class BirthdayUpdateComponent implements OnInit {
  isSaving = false;

  editForm = this.fb.group({
    id: [undefined as string | null | undefined],
    name: [undefined as string | null | undefined, [Validators.required]],
    date: [undefined as Date | null | undefined, [Validators.required]],
    description: [undefined as string | null | undefined],
  });

  constructor(
    protected birthdayService: BirthdayService,
    protected activatedRoute: ActivatedRoute,
    protected fb: FormBuilder,
  ) {}

  ngOnInit(): void {
    this.activatedRoute.data.subscribe(({ birthday }) => {
      this.updateForm(birthday);
    });
  }

  previousState(): void {
    window.history.back();
  }

  save(): void {
    this.isSaving = true;
    const birthday = this.createFromForm();
    if (birthday.id !== undefined) {
      this.subscribeToSaveResponse(this.birthdayService.update(birthday));
    } else {
      this.subscribeToSaveResponse(this.birthdayService.create(birthday));
    }
  }

  protected subscribeToSaveResponse(result: Observable<HttpResponse<IBirthday>>): void {
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

  protected updateForm(birthday: IBirthday): void {
    this.editForm.patchValue({
      id: birthday.id as string | null | undefined,
      name: birthday.name as string | null | undefined,
      date: birthday.date as Date | null | undefined,
      description: birthday.description as string | null | undefined,
    });
  }

  protected createFromForm(): IBirthday {
    const birthday = new Birthday();
    const id = this.editForm.get(['id'])!.value;
    const name = this.editForm.get(['name'])!.value;
    const date = this.editForm.get(['date'])!.value;
    const description = this.editForm.get(['description'])!.value;

    birthday.id = id ?? undefined;
    birthday.name = name ?? undefined;
    birthday.date = date ?? undefined;
    birthday.description = description ?? undefined;

    return birthday;
  }
}
