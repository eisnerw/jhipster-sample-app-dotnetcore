import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';

@Component({
  selector: 'lib-message-dialog',
  standalone: false,
  template: `
    <h1 mat-dialog-title>{{data.title}}</h1>
    <div mat-dialog-content>{{data.message}}</div>
    <div mat-dialog-actions>
      @if (data.confirm) {
        <button mat-button [mat-dialog-close]="false">Cancel</button>
      }
      <button mat-button color="primary" [mat-dialog-close]="true">OK</button>
    </div>
    `
})
export class MessageDialogComponent {
  constructor(@Inject(MAT_DIALOG_DATA) public data: { title: string; message: string; confirm?: boolean }) {}
}
