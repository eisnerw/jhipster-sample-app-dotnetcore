import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';

import SharedModule from 'app/shared/shared.module';
import { EntityGenericService } from '../service/entity-generic.service';

type AnyRow = { id?: string; [k: string]: any };

@Component({
  selector: 'jhi-generic-edit',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, SharedModule],
  templateUrl: './generic-edit.component.html',
})
export class GenericEditComponent implements OnInit {
  entity!: string;
  id: string | null = null;
  title = 'Edit';
  isSaving = false;

  form = new FormGroup({});
  fields: { key: string; type: string; label: string; options?: { label: string; value: string }[] }[] = [];

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private fb = inject(FormBuilder);
  private service = inject(EntityGenericService);
  private cdr = inject(ChangeDetectorRef);

  ngOnInit(): void {
    this.entity = this.route.snapshot.paramMap.get('entity')!;
    this.id = this.route.snapshot.paramMap.get('id');

    this.service.getEntitySpec(this.entity).subscribe(spec => {
      const specObj = this.normalizeSpec(spec);
      const fieldsObj = (specObj?.fields as Record<string, any>) || {};
      if (typeof console !== 'undefined') {
        try {
          console.warn('[GenericEdit] spec loaded', {
            entity: this.entity,
            fieldsCount: Object.keys(fieldsObj).length,
            fieldKeys: Object.keys(fieldsObj),
          });
        } catch {
          /* ignore */
        }
      }
      const EXCLUDE = new Set(['document', 'categories', 'category']);
      this.fields = Object.keys(fieldsObj)
        .filter(k => !EXCLUDE.has(k.toLowerCase()))
        .map(k => {
          const f = fieldsObj[k] || {};
          const t = String(f.type || 'string').toLowerCase();
          // For form labels, prefer the friendly field 'name' over 'column'.
          const label = String(f.name || f.column || k);
          const opts = Array.isArray(f.options)
            ? (f.options as any[]).map(o => ({ label: o.name || String(o.value), value: String(o.value) }))
            : undefined;
          return { key: k, type: t, label, options: opts };
        });
      this.buildForm();
      try {
        this.cdr.detectChanges();
      } catch {
        /* ignore */
      }
      if (this.id) {
        this.title = 'Edit';
        this.loadRow(this.id);
      } else {
        this.title = 'Create';
      }
    });
  }

  private normalizeSpec(raw: any): any {
    if (!raw) return {};
    if (typeof raw === 'string') {
      try {
        return JSON.parse(raw);
      } catch {
        return {};
      }
    }
    return raw;
  }

  private buildForm(): void {
    const controls: any = {};
    for (const f of this.fields) {
      if (f.key === 'id') continue;
      switch (f.type) {
        case 'number':
          controls[f.key] = [null];
          break;
        case 'boolean':
          controls[f.key] = [false];
          break;
        case 'date':
          controls[f.key] = [null];
          break;
        default:
          controls[f.key] = [null];
      }
    }
    this.form = this.fb.group(controls);
    if (typeof console !== 'undefined') {
      try {
        console.warn('[GenericEdit] form built', {
          entity: this.entity,
          fieldsCount: this.fields.length,
          controlKeys: Object.keys(controls),
        });
      } catch {
        /* ignore */
      }
    }
    try {
      this.cdr.detectChanges();
    } catch {
      /* ignore */
    }
  }

  private loadRow(id: string): void {
    this.service.find<AnyRow>(this.entity, id).subscribe((res: HttpResponse<AnyRow>) => {
      const row = res.body || {};
      const patch: any = {};
      for (const f of this.fields) {
        if (f.key === 'id') continue;
        patch[f.key] = row[f.key] ?? null;
      }
      this.form.patchValue(patch);
    });
  }

  save(): void {
    const payload: AnyRow = {};
    for (const f of this.fields) {
      if (f.key === 'id') continue;
      payload[f.key] = this.form.get(f.key)?.value ?? null;
    }
    this.isSaving = true;
    let req$: Observable<HttpResponse<AnyRow>>;
    if (this.id) req$ = this.service.update<AnyRow>(this.entity, this.id, payload);
    else req$ = this.service.create<AnyRow>(this.entity, payload);
    req$.subscribe({
      next: () => this.onSaveSuccess(),
      error: () => (this.isSaving = false),
    });
  }

  delete(): void {
    if (!this.id) return;
    if (!confirm('Delete this item?')) return;
    this.service.delete(this.entity, this.id).subscribe(() => this.onSaveSuccess());
  }

  cancel(): void {
    this.router.navigate(['/entity', this.entity]);
  }

  private onSaveSuccess(): void {
    this.isSaving = false;
    this.router.navigate(['/entity', this.entity]);
  }
}
