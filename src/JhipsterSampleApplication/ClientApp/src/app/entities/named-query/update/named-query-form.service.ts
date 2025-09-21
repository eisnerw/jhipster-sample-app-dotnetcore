import { Injectable } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';

import { INamedQuery, NewNamedQuery } from '../named-query.model';

type NamedQueryFormGroupContent = {
  id: FormControl<INamedQuery['id'] | null>;
  name: FormControl<INamedQuery['name'] | null>;
  text: FormControl<INamedQuery['text'] | null>;
  owner: FormControl<INamedQuery['owner'] | null>;
  entity: FormControl<INamedQuery['entity'] | null>;
};

export type NamedQueryFormGroup = FormGroup<NamedQueryFormGroupContent>;

@Injectable({ providedIn: 'root' })
export class NamedQueryFormService {
  createNamedQueryFormGroup(namedQuery: NamedQueryFormGroupInput = { id: null }): NamedQueryFormGroup {
    const namedQueryRawValue = {
      ...this.getFormDefaults(),
      ...namedQuery,
    };
    return new FormGroup<NamedQueryFormGroupContent>({
      id: new FormControl(
        { value: namedQueryRawValue.id, disabled: true },
        {
          nonNullable: true,
          validators: [Validators.required],
        },
      ),
      name: new FormControl(namedQueryRawValue.name ?? '', {
        nonNullable: true,
        validators: [Validators.required],
      }),
      text: new FormControl(namedQueryRawValue.text ?? '', {
        nonNullable: true,
        validators: [Validators.required],
      }),
      owner: new FormControl(namedQueryRawValue.owner ?? '', {
        nonNullable: true,
      }),
      entity: new FormControl(namedQueryRawValue.entity ?? '', {
        nonNullable: true,
        validators: [Validators.required],
      }),
    });
  }

  getNamedQuery(form: NamedQueryFormGroup): INamedQuery | NewNamedQuery {
    return form.getRawValue() as INamedQuery | NewNamedQuery;
  }

  resetForm(form: NamedQueryFormGroup, namedQuery: NamedQueryFormGroupInput): void {
    const namedQueryRawValue = { ...this.getFormDefaults(), ...namedQuery };
    form.reset(
      {
        ...namedQueryRawValue,
        id: { value: namedQueryRawValue.id, disabled: true },
      } as any /* cast to workaround https://github.com/angular/angular/issues/46458 */,
    );
  }

  private getFormDefaults(): NamedQueryFormDefaults {
    return {
      id: null,
    };
  }
}

type NamedQueryFormDefaults = Pick<NewNamedQuery, 'id'>;

type NamedQueryFormGroupInput = {
  id: INamedQuery['id'] | null;
  name?: INamedQuery['name'] | null;
  text?: INamedQuery['text'] | null;
  owner?: INamedQuery['owner'] | null;
  entity?: INamedQuery['entity'] | null;
};
