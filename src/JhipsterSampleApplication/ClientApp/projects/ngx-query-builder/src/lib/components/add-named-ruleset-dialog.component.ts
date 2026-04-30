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
    <div class="q-add-named-dialog">
      <h2 class="q-add-named-title">Select a Named {{ data.rulesetName }}</h2>
      <div class="q-add-named-content">
        <label class="q-add-named-label" for="q-add-named-ruleset">
          {{ data.rulesetName }} Name
        </label>
        <select
          id="q-add-named-ruleset"
          class="q-add-named-select"
          [value]="selected || ''"
          (change)="onSelectionChange($event)"
        >
          <option value="" disabled>Select {{ data.rulesetName }}</option>
          @for (n of data.names; track n) {
            <option [value]="n">{{ n }}</option>
          }
        </select>
      </div>
      <div class="q-add-named-actions">
        <button type="button" class="q-add-named-button" (click)="dialogRef.close()">Cancel</button>
        <button
          type="button"
          class="q-add-named-button q-add-named-button-primary"
          [disabled]="!selected"
          (click)="dialogRef.close(selected)"
        >
          Add
        </button>
      </div>
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .q-add-named-dialog {
        box-sizing: border-box;
        width: min(380px, calc(100vw - 48px));
        padding: 20px 24px 16px;
      }

      .q-add-named-title {
        margin: 0 0 18px;
        color: #1f2937;
        font: 500 20px/1.3 Arial, Helvetica, sans-serif;
      }

      .q-add-named-content {
        display: flex;
        flex-direction: column;
        gap: 6px;
        margin-bottom: 20px;
      }

      .q-add-named-label {
        color: #4b5563;
        font: 500 13px/1.4 Arial, Helvetica, sans-serif;
      }

      .q-add-named-select {
        box-sizing: border-box;
        width: 100%;
        min-height: 36px;
        padding: 6px 32px 6px 10px;
        border: 1px solid #9ca3af;
        border-radius: 4px;
        background-color: #fff;
        color: #111827;
        font: 14px/1.4 Arial, Helvetica, sans-serif;
      }

      .q-add-named-select:focus {
        border-color: #2563eb;
        outline: 2px solid rgba(37, 99, 235, 0.2);
        outline-offset: 1px;
      }

      .q-add-named-actions {
        display: flex;
        justify-content: flex-end;
        gap: 8px;
      }

      .q-add-named-button {
        min-width: 72px;
        min-height: 34px;
        padding: 6px 12px;
        border: 1px solid #9ca3af;
        border-radius: 4px;
        background: #fff;
        color: #111827;
        cursor: pointer;
        font: 500 14px/1.3 Arial, Helvetica, sans-serif;
      }

      .q-add-named-button:hover:not(:disabled) {
        background: #f3f4f6;
      }

      .q-add-named-button-primary {
        border-color: #2563eb;
        background: #2563eb;
        color: #fff;
      }

      .q-add-named-button-primary:hover:not(:disabled) {
        background: #1d4ed8;
      }

      .q-add-named-button:disabled {
        border-color: #d1d5db;
        background: #f3f4f6;
        color: #9ca3af;
        cursor: default;
      }
    `,
  ],
})
export class AddNamedRulesetDialogComponent {
  dialogRef = inject<MatDialogRef<AddNamedRulesetDialogComponent>>(MatDialogRef);
  data = inject<AddNamedRulesetDialogData>(MAT_DIALOG_DATA);

  selected: string | null = null;

  onSelectionChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.selected = value || null;
  }
}
