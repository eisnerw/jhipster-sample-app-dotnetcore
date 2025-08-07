import { Component, OnInit, inject } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
import { FormBuilder, Validators, ReactiveFormsModule, FormGroup } from '@angular/forms';
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

  editForm!: FormGroup;

  protected birthdayService = inject(BirthdayService);
  protected activatedRoute = inject(ActivatedRoute);
  protected fb = inject(FormBuilder);

  ngOnInit(): void {
    this.editForm = this.fb.group({
      id: [undefined as string | null | undefined],
      lname: [undefined as string | null | undefined, [Validators.required]],
      fname: [undefined as string | null | undefined, [Validators.required]],
      sign: [undefined as string | null | undefined],
      dob: [undefined as Date | null | undefined, [Validators.required]],
      isAlive: [undefined as boolean | null | undefined, [Validators.required]],
    });
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
      lname: birthday.lname as string | null | undefined,
      fname: birthday.fname as string | null | undefined,
      sign: birthday.sign as string | null | undefined,
      dob: birthday.dob as Date | null | undefined,
      isAlive: birthday.isAlive as boolean | null | undefined,
    });
  }

  protected createFromForm(): IBirthday {
    const birthday = new Birthday();
    const id = this.editForm.get(['id'])!.value;
    const lname = this.editForm.get(['lname'])!.value;
    const fname = this.editForm.get(['fname'])!.value;
    const sign = this.editForm.get(['sign'])!.value;
    const dob = this.editForm.get(['dob'])!.value;
    const isAlive = this.editForm.get(['isAlive'])!.value;

    birthday.id = id ?? undefined;
    birthday.lname = lname ?? undefined;
    birthday.fname = fname ?? undefined;
    birthday.sign = sign ?? undefined;
    birthday.dob = dob ?? undefined;
    birthday.isAlive = isAlive ?? undefined;

    return birthday;
  }
}
