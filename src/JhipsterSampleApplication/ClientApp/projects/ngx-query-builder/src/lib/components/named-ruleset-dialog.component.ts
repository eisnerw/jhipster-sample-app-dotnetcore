import { Component, inject } from '@angular/core';
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
    <div class="q-named-dialog">
      <h2 class="q-named-title">Update {{ data.rulesetName }}</h2>
      <div class="q-named-content">
        <label class="q-named-label" for="q-named-ruleset-name"> {{ data.rulesetName }} Name </label>
        <div class="q-named-input-row">
          <input id="q-named-ruleset-name" class="q-named-input" [value]="name" (input)="onInput($event)" type="text" />
          @if (name) {
            <button class="q-named-clear" type="button" aria-label="Clear ruleset name" (click)="name = ''">&times;</button>
          }
        </div>
      </div>
      @if (name.trim() === '') {
        <div class="q-modified-warning">The name will be removed from the {{ data.rulesetName }}</div>
      }
      @if (name.trim() !== '' && name.trim() !== data.name) {
        <div class="q-modified-warning">All instances of {{ data.name }} will be renamed to {{ name.trim() }}</div>
      }
      @if (data.modified) {
        <div class="q-modified-warning">Existing definition will be updated.</div>
      }
      <div class="q-named-actions">
        <button class="q-named-button" type="button" (click)="dialogRef.close({ action: 'cancel' })">Cancel</button>
        @if (data.modified) {
          <button class="q-named-button" type="button" (click)="dialogRef.close({ action: 'undo' })">Undo</button>
        }
        @if (data.allowEdit) {
          <button class="q-named-button" type="button" (click)="dialogRef.close({ action: 'edit' })">Edit</button>
        }
        <button
          class="q-named-button q-named-button-primary"
          type="button"
          [disabled]="isUpdateDisabled()"
          (click)="dialogRef.close({ action: 'save', name })"
        >
          Update
        </button>
      </div>
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .q-named-dialog {
        box-sizing: border-box;
        width: min(420px, calc(100vw - 48px));
        padding: 20px 24px 16px;
      }

      .q-named-title {
        margin: 0 0 18px;
        color: #1f2937;
        font:
          500 20px/1.3 Arial,
          Helvetica,
          sans-serif;
      }

      .q-named-content {
        display: flex;
        flex-direction: column;
        gap: 6px;
        margin-bottom: 12px;
      }

      .q-named-label {
        color: #4b5563;
        font:
          500 13px/1.4 Arial,
          Helvetica,
          sans-serif;
      }

      .q-named-input-row {
        position: relative;
        display: flex;
        align-items: center;
      }

      .q-named-input {
        box-sizing: border-box;
        width: 100%;
        min-height: 36px;
        padding: 6px 34px 6px 10px;
        border: 1px solid #9ca3af;
        border-radius: 4px;
        background-color: #fff;
        color: #111827;
        font:
          14px/1.4 Arial,
          Helvetica,
          sans-serif;
      }

      .q-named-input:focus {
        border-color: #2563eb;
        outline: 2px solid rgba(37, 99, 235, 0.2);
        outline-offset: 1px;
      }

      .q-named-clear {
        position: absolute;
        right: 5px;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: 26px;
        height: 26px;
        border: 0;
        border-radius: 4px;
        background: transparent;
        color: #6b7280;
        cursor: pointer;
        font:
          18px/1 Arial,
          Helvetica,
          sans-serif;
      }

      .q-named-clear:hover {
        background: #f3f4f6;
        color: #111827;
      }

      .q-modified-warning {
        margin: 8px 0 0;
        color: #8a5a00;
        font:
          13px/1.4 Arial,
          Helvetica,
          sans-serif;
      }

      .q-named-actions {
        display: flex;
        justify-content: flex-end;
        gap: 8px;
        margin-top: 20px;
      }

      .q-named-button {
        min-width: 72px;
        min-height: 34px;
        padding: 6px 12px;
        border: 1px solid #9ca3af;
        border-radius: 4px;
        background: #fff;
        color: #111827;
        cursor: pointer;
        font:
          500 14px/1.3 Arial,
          Helvetica,
          sans-serif;
      }

      .q-named-button:hover:not(:disabled) {
        background: #f3f4f6;
      }

      .q-named-button-primary {
        border-color: #2563eb;
        background: #2563eb;
        color: #fff;
      }

      .q-named-button-primary:hover:not(:disabled) {
        background: #1d4ed8;
      }

      .q-named-button:disabled {
        border-color: #d1d5db;
        background: #f3f4f6;
        color: #9ca3af;
        cursor: default;
      }
    `,
  ],
})
export class NamedRulesetDialogComponent {
  dialogRef = inject<MatDialogRef<NamedRulesetDialogComponent, NamedRulesetDialogResult>>(MatDialogRef);
  data = inject<NamedRulesetDialogData>(MAT_DIALOG_DATA);

  name: string;
  constructor() {
    const data = this.data;

    this.name = data.name;
  }

  onInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const sanitizer =
      this.data.rulesetNameSanitizer ||
      ((v: string) =>
        v
          .toUpperCase()
          .replace(/ /g, '_')
          .replace(/[^A-Z0-9_]/g, ''));
    this.name = sanitizer(input.value);
  }

  isUpdateDisabled(): boolean {
    if (!this.data.valid) {
      return true;
    }
    return !this.data.modified && this.name.trim() === this.data.name;
  }
}
