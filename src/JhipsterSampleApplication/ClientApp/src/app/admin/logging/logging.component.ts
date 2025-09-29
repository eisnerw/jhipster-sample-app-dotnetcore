import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'jhi-admin-logging',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
  <div class="container py-3">
    <h2 class="mb-3">Logging Control</h2>
    <div class="card mb-3">
      <div class="card-body d-flex align-items-end gap-2 flex-wrap">
        <div>
          <label class="form-label">Global level</label>
          <select class="form-select" [(ngModel)]="globalLevel" (ngModelChange)="applyGlobal()">
            <option *ngFor="let l of levels" [ngValue]="l">{{ l }}</option>
          </select>
        </div>
        <div>
          <label class="form-label">Duration (minutes, optional)</label>
          <input type="number" class="form-control" [(ngModel)]="minutes" min="0" placeholder="0 = until changed" (change)="applyGlobal()" />
        </div>
      </div>
    </div>

    <div class="card">
      <div class="card-body d-flex align-items-end gap-2 flex-wrap">
        <div>
          <label class="form-label">On-demand file level</label>
          <select class="form-select" [(ngModel)]="fileLevel" (ngModelChange)="applyFile()">
            <option *ngFor="let l of levels" [ngValue]="l">{{ l }}</option>
          </select>
        </div>
        <div>
          <label class="form-label">Duration (minutes)</label>
          <input type="number" class="form-control" [(ngModel)]="fileMinutes" min="0" placeholder="0 = until changed" (change)="applyFile()" />
        </div>
        <div class="ms-auto d-flex gap-2 align-items-end">
          <button type="button" class="btn btn-outline-danger" [disabled]="busy" (click)="truncate()">
            {{ busy ? 'Truncating…' : 'Truncate' }}
          </button>
        </div>
      </div>
    </div>
    <p class="text-muted mt-2">
      File location: <code>{{ info?.filePath || 'logs/on-demand/log-YYYYMMDD.log' }}</code>
      <span *ngIf="info?.fileExists" class="ms-2">(exists<span *ngIf="info?.fileSize != null">, {{ info!.fileSize }} bytes</span>)</span>
      <span *ngIf="info?.latestFilePath && info?.latestFilePath !== info?.filePath" class="ms-3">
        Latest: <code>{{ info!.latestFilePath }}</code>
      </span>
    </p>
  </div>
  `,
})
export default class LoggingComponent {
  private readonly http = inject(HttpClient);
  levels = ['Verbose','Debug','Information','Warning','Error','Fatal'];
  globalLevel = 'Information';
  fileLevel = 'Fatal';
  minutes: number | null = null;
  fileMinutes: number | null = null;
  info: { filePath?: string; fileExists?: boolean; fileSize?: number | null; latestFilePath?: string } | null = null;
  busy = false;

  async refresh() {
    this.http.get<{ globalLevel: string; fileLevel: string; filePath?: string; fileExists?: boolean; fileSize?: number | null; latestFilePath?: string }>(`/api/admin/logging/level`).subscribe(js => {
      this.globalLevel = js.globalLevel;
      this.fileLevel = js.fileLevel;
      this.info = { filePath: js.filePath, fileExists: js.fileExists, fileSize: js.fileSize ?? null, latestFilePath: js.latestFilePath };
    });
  }

  async applyGlobal() {
    this.http
      .post(`/api/admin/logging/level`, { level: this.globalLevel, minutes: this.minutes ?? undefined })
      .subscribe(() => this.refresh());
  }

  async applyFile() {
    this.http
      .post(`/api/admin/logging/file`, { level: this.fileLevel, minutes: this.fileMinutes ?? undefined })
      .subscribe(() => this.refresh());
  }

  ngOnInit() { this.refresh(); }

  truncate() {
    if (this.busy) return;
    this.busy = true;
    this.http.post(`/api/admin/logging/truncate`, {}, { responseType: 'json' })
      .subscribe({
        next: () => { this.refresh(); this.busy = false; },
        error: () => { this.busy = false; }
      });
  }
}
