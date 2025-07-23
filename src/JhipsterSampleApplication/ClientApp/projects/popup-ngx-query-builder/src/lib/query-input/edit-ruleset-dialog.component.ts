import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { RuleSet, QueryBuilderConfig } from 'ngx-query-builder';
import { bqlToRuleset, rulesetToBql } from '../bql';

export interface EditRulesetDialogData {
  ruleset: RuleSet;
  rulesetName: string;
  validate: (rs: any) => boolean;
  config: QueryBuilderConfig;
}

@Component({
  selector: 'lib-edit-ruleset-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule],
  template: `
    <h1 mat-dialog-title>Edit {{data.rulesetName}}</h1>
    <div mat-dialog-content>
      <textarea class="dialog-output" [(ngModel)]="text" (ngModelChange)="onChange($event)"></textarea>
    </div>
    <div mat-dialog-actions>
      <button mat-button (click)="dialogRef.close()">Cancel</button>
      <button mat-raised-button color="primary" [disabled]="state !== 'valid'" (click)="save()">Save</button>
    </div>
  `,
  styles: [
    `.dialog-output { width: 100%; height: 100%; resize: none; box-sizing: border-box; }`
  ]
})
export class EditRulesetDialogComponent {
  text: string;
  state: 'valid' | 'invalid-bql' | 'invalid-query' = 'valid';

  constructor(
    public dialogRef: MatDialogRef<EditRulesetDialogComponent, RuleSet | null>,
    @Inject(MAT_DIALOG_DATA) public data: EditRulesetDialogData
  ) {
    this.text = rulesetToBql(data.ruleset, data.config);
    this.validate();
  }

  onChange(value: string): void {
    this.text = value;
    this.validate();
  }

  private validate(): void {
    try {
      const val = bqlToRuleset(this.text.trim(), this.data.config);
      this.state = this.data.validate(val) ? 'valid' : 'invalid-query';
    } catch {
      this.state = 'invalid-bql';
    }
  }

  save(): void {
    try {
      const val = bqlToRuleset(this.text.trim(), this.data.config);
      if (this.data.validate(val)) {
        this.dialogRef.close(val);
      }
    } catch {
      // ignore
    }
  }
}
