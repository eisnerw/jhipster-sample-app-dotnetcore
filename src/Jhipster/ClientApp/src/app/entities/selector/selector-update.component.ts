import { Component, OnInit } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
// eslint-disable-next-line @typescript-eslint/no-unused-vars
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

import { ISelector, Selector } from 'app/shared/model/selector.model';
import { SelectorService } from './selector.service';
import { ICountry } from 'app/shared/model/country.model';
import { CountryService } from 'app/entities/country/country.service';
import { RulesetService } from '../ruleset/ruleset.service';
import { IStoredRuleset } from 'app/shared/model/ruleset.model';

@Component({
  selector: 'jhi-selector-update',
  templateUrl: './selector-update.component.html',
})
export class SelectorUpdateComponent implements OnInit {
  isSaving = false;
  countries: ICountry[] = [];
  rulesets: IStoredRuleset[] = [];

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
    protected countryService: CountryService,
    protected activatedRoute: ActivatedRoute,
    protected rulesetService: RulesetService,
    private fb: FormBuilder
  ) {}

  ngOnInit(): void {
    this.activatedRoute.data.subscribe(({ selector }) => {
      this.updateForm(selector);
        this.rulesetService
        .query({ filter: 'selector-is-null' })
        .pipe(
          map((res: HttpResponse<IStoredRuleset[]>) => {
            return res.body || [];
          })
        )
        .subscribe((resBody: IStoredRuleset[]) => {
          this.rulesets = resBody;
        });

    });
  }

  updateForm(selector: ISelector): void {
    this.editForm.patchValue({
      id: selector.id,
      name: selector.name,
      rulesetName: selector.rulesetName,
      action: selector.action,
      actionParameter: selector.actionParameter,
      description: selector.description,
    });
  }

  previousState(): void {
    window.history.back();
  }

  save(): void {
    this.isSaving = true;
    const selector = this.createFromForm();
    if (selector.id !== undefined) {
      this.subscribeToSaveResponse(this.selectorService.update(selector));
    } else {
      this.subscribeToSaveResponse(this.selectorService.create(selector));
    }
  }

  private createFromForm(): ISelector {
    return {
      ...new Selector(),
      id: this.editForm.get(['id'])!.value,
      name: this.editForm.get(['name'])!.value,
      rulesetName: this.editForm.get(['rulesetName'])!.value,
      action: this.editForm.get(['action'])!.value,
      actionParameter: this.editForm.get(['actionParameter'])!.value,
      description: this.editForm.get(['description'])!.value,
    };
  }

  protected subscribeToSaveResponse(result: Observable<HttpResponse<ISelector>>): void {
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


}
