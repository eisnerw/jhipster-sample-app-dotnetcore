import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HistoryService, EntityArrayResponseType } from '../service/history.service';
import { IHistory } from '../history.model';
import { HttpResponse } from '@angular/common/http';

@Component({
  selector: 'jhi-history',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './history.component.html',
})
export class HistoryComponent implements OnInit {
  histories: IHistory[] = [];
  private historyService = inject(HistoryService);

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.historyService.query().subscribe((res: EntityArrayResponseType) => {
      this.histories = res.body ?? [];
    });
  }
}
