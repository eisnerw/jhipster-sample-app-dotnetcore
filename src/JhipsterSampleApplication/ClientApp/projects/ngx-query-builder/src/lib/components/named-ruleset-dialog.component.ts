import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

export interface NamedRulesetDialogData {
  name: string;
  rulesetName: string;
  allowDelete: boolean;
  modified: boolean;
  valid: boolean;
  rulesetNameSanitizer?: (value: string) => string;
  allowEdit?: boolean;
}

export interface NamedRulesetDialogResult {
  action: 'save' | 'delete' | 'cancel' | 'removeName' | 'undo' | 'edit';
  name?: string;
}

@Component({
  selector: 'lib-named-ruleset-dialog',
  standalone: false,
  template: `
    <h1 mat-dialog-title>Update {{data.rulesetName}}</h1>
    <div mat-dialog-content>
      <mat-form-field appearance="fill">
        <mat-label>{{data.rulesetName}} Name</mat-label>
        <input matInput [(ngModel)]="name" (input)="onInput($event)" />
        @if (name) {
          <button mat-icon-button matSuffix (click)="name = ''" type="button">✕</button>
        }
      </mat-form-field>
      @if (name.trim() === '') {
        <div class="q-modified-warning">
          The name will be removed from the {{data.rulesetName}}
        </div>
      }
      @if (name.trim() !== '' && name.trim() !== data.name) {
        <div class="q-modified-warning">
          All instances of {{data.name}} will be renamed to {{name.trim()}}
        </div>
      }
      @if (data.modified) {
        <div class="q-modified-warning">
          Existing definition will be updated.
        </div>
      }
    </div>
    <div mat-dialog-actions>
      <button mat-button (click)="dialogRef.close({action: 'cancel'})">Cancel</button>
      @if (data.modified) {
        <button mat-button (click)="dialogRef.close({action: 'undo'})">Undo</button>
      }
      @if (data.allowEdit) {
        <button mat-button (click)="dialogRef.close({action: 'edit'})">Edit</button>
      }
      <button mat-raised-button color="primary" [disabled]="isUpdateDisabled()" (click)="dialogRef.close({action: 'save', name})">Update</button>
    </div>
    `
})
export class NamedRulesetDialogComponent {
  name: string;
  constructor(
    public dialogRef: MatDialogRef<NamedRulesetDialogComponent, NamedRulesetDialogResult>,
    @Inject(MAT_DIALOG_DATA) public data: NamedRulesetDialogData
  ) {
    this.name = data.name;
  }

  onInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const sanitizer = this.data.rulesetNameSanitizer ||
      ((v: string) => v.toUpperCase().replace(/ /g, '_').replace(/[^A-Z0-9_]/g, ''));
    this.name = sanitizer(input.value);
  }

  isUpdateDisabled(): boolean {
    if (!this.data.valid) {
      return true;
    }
    return !this.data.modified && this.name.trim() === this.data.name;
  }
}
