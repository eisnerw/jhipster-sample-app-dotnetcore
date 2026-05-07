import { Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

@Component({
  selector: 'lib-message-dialog',
  standalone: false,
  template: `
    <div class="q-message-dialog">
      <h2 class="q-message-title">{{ data.title }}</h2>
      <div class="q-message-content">{{ data.message }}</div>
      <div class="q-message-actions">
        @if (data.confirm) {
          <button class="q-message-button" type="button" (click)="dialogRef.close(false)">Cancel</button>
        }
        <button class="q-message-button q-message-button-primary" type="button" (click)="dialogRef.close(true)">OK</button>
      </div>
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .q-message-dialog {
        box-sizing: border-box;
        width: min(360px, calc(100vw - 48px));
        padding: 20px 24px 16px;
      }

      .q-message-title {
        margin: 0 0 12px;
        color: #1f2937;
        font: 500 20px/1.3 Arial, Helvetica, sans-serif;
      }

      .q-message-content {
        color: #4b5563;
        font: 14px/1.4 Arial, Helvetica, sans-serif;
      }

      .q-message-actions {
        display: flex;
        justify-content: flex-end;
        gap: 8px;
        margin-top: 20px;
      }

      .q-message-button {
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

      .q-message-button:hover {
        background: #f3f4f6;
      }

      .q-message-button-primary {
        border-color: #2563eb;
        background: #2563eb;
        color: #fff;
      }

      .q-message-button-primary:hover {
        background: #1d4ed8;
      }
    `,
  ],
})
export class MessageDialogComponent {
  dialogRef = inject<MatDialogRef<MessageDialogComponent, boolean>>(MatDialogRef);
  data = inject<{
    title: string;
    message: string;
    confirm?: boolean;
  }>(MAT_DIALOG_DATA);
}
