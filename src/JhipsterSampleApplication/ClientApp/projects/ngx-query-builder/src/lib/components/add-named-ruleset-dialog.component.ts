import { Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

export interface AddNamedRulesetDialogData {
  names: string[];
  rulesetName: string;
}

@Component({
  selector: 'lib-add-named-ruleset-dialog',
  standalone: false,
  template: `
    <h1 mat-dialog-title>Select a Named {{data.rulesetName}}</h1>
    <div mat-dialog-content>
      <mat-form-field appearance="fill">
        <mat-label>{{data.rulesetName}} Name</mat-label>
        <mat-select [(value)]="selected">
          @for (n of data.names; track n) {
            <mat-option [value]="n">{{n}}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </div>
    <div mat-dialog-actions>
      <button mat-button (click)="dialogRef.close()">Cancel</button>
      <button mat-raised-button color="primary" [disabled]="!selected" (click)="dialogRef.close(selected)">Add</button>
    </div>
    `
})
export class AddNamedRulesetDialogComponent {
  dialogRef = inject<MatDialogRef<AddNamedRulesetDialogComponent>>(MatDialogRef);
  data = inject<AddNamedRulesetDialogData>(MAT_DIALOG_DATA);

  selected: string | null = null;
}
