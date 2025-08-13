/* eslint-disable */

import { Component, OnInit, ViewChild, TemplateRef, AfterViewInit, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { HttpClientModule } from '@angular/common/http';
import { combineLatest, Subscription } from 'rxjs';
import { map } from 'rxjs/operators';

import { QueryInputComponent, bqlToRuleset } from 'popup-ngx-query-builder';
import { QueryLanguageSpec } from 'ngx-query-builder';
import { MenuItem, MessageService } from 'primeng/api';
import { MenuModule } from 'primeng/menu';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';

import SharedModule from 'app/shared/shared.module';
import { ViewService } from '../../view/service/view.service';
import { DataLoader, FetchFunction } from 'app/shared/data-loader';
import { SuperTable, ColumnConfig, GroupDescriptor, GroupData } from '../../../shared/SuperTable/super-table.component';

import { SupremeService, EntityArrayResponseType, ViewArrayResponseType } from '../service/supreme.service';
import { ISupreme } from '../supreme.model';

@Component({
  selector: 'jhi-supreme',
  templateUrl: './supreme.component.html',
  styleUrls: ['./supreme.component.scss'],
  schemas: [NO_ERRORS_SCHEMA],
  providers: [MessageService],
  imports: [
    CommonModule,
    FormsModule,
    HttpClientModule,
    RouterModule,
    SharedModule,
    SuperTable,
    TableModule,
    QueryInputComponent,
    InputTextModule,
    DialogModule,
    ButtonModule,
    MenuModule,
  ],
  standalone: true,
})
export class SupremeComponent implements OnInit, AfterViewInit {
  protected supremeService = inject(SupremeService);
  protected viewService = inject(ViewService);
  protected activatedRoute = inject(ActivatedRoute);
  protected router = inject(Router);
  protected messageService = inject(MessageService);

  @ViewChild('superTable') superTable!: SuperTable;
  @ViewChild('expandedRow', { static: true }) expandedRowTemplate: TemplateRef<any> | undefined;
  @ViewChild(QueryInputComponent) queryInput!: QueryInputComponent;

  dataLoader: DataLoader<ISupreme>;
  spec: QueryLanguageSpec | undefined;
  currentQuery = '';
  rulesetJson = '';
  itemsPerPage = 50;
  page = 1;
  predicate!: string;
  ascending!: boolean;
  ngbPaginationPage = 1;
  viewName: string | null = null;
  views: { label: string; value: string }[] = [];
  viewMode: 'grid' | 'group' = 'grid';
  groups: GroupDescriptor[] = [];
  globalFilterFields: string[] = ['name', 'term', 'heard_by', 'docket_number'];
  showRowNumbers = false;

  columns: ColumnConfig[] = [
    { field: 'lineNumber', header: '#', type: 'lineNumber', width: '4rem' },
    { field: 'name', header: 'Case', filterType: 'text', type: 'string', width: '360px' },
    { field: 'term', header: 'Term', filterType: 'text', type: 'string', width: '120px' },
    { field: 'docket_number', header: 'Docket #', filterType: 'text', type: 'string', width: '140px' },
    { field: 'heard_by', header: 'Heard By', filterType: 'text', type: 'string', width: '240px' },
  ];

  private lastSortEvent: any = null;

  constructor() {
    const fetchFunction: FetchFunction<ISupreme> = (queryParams: any) => {
      if (queryParams.bqlQuery) {
        const bql = queryParams.bqlQuery;
        delete queryParams.bqlQuery;
        return this.supremeService.searchWithBql(bql, queryParams);
      }
      return this.supremeService.query(queryParams);
    };
    this.dataLoader = new DataLoader<ISupreme>(fetchFunction);
  }

  ngOnInit(): void {
    this.supremeService.getQueryBuilderSpec().subscribe({
      next: spec => (this.spec = spec),
      error: () => (this.spec = undefined),
    });
    this.loadViews();
    this.handleNavigation();
  }

  ngAfterViewInit(): void {
    this.onQueryChange(this.currentQuery);
  }

  loadViews(): void {
    this.viewService.queryByDomain('supreme').subscribe((res) => {
      const body = res.body ?? [];
      this.views = body.map((v: any) => ({ label: v.name, value: v.id! }));
    });
  }

  onQueryChange(query: string, restoreState = false) {
    this.currentQuery = query;
    try {
      const rs = bqlToRuleset(query, this.queryInput.queryBuilderConfig);
      this.rulesetJson = JSON.stringify(rs, null, 2);
    } catch {
      this.rulesetJson = '';
    }
    if (this.viewName) {
      this.loadRootGroups(restoreState);
    } else {
      this.loadPage();
    }
  }

  loadPage(): void {
    const filter: any = {};
    if (this.currentQuery && this.currentQuery.trim().length > 0) {
      filter.bqlQuery = this.currentQuery.trim();
    } else {
      filter.luceneQuery = '*';
    }
    this.dataLoader.load(this.itemsPerPage, this.predicate, this.ascending, filter);
    setTimeout(() => this.superTable.applyCapturedHeaderState(), 500);
  }

  loadRootGroups(restoreState: boolean = false): void {
    if (!this.viewName) {
      this.groups = [];
      this.loadPage();
      this.viewMode = 'grid';
      if (restoreState) setTimeout(() => this.superTable.applyCapturedHeaderState(), 500);
      return;
    }
    const viewParams: any = { from: 0, pageSize: 1000, view: this.viewName! };
    const hasQuery = this.currentQuery && this.currentQuery.trim().length > 0;
    if (hasQuery) {
      this.supremeService
        .searchWithBql(this.currentQuery.trim(), viewParams)
        .pipe(map((res) => res.body?.hits ?? []))
        .subscribe((hits: any[]) => {
          if (hits.length > 0 && (hits[0] as any).categoryName !== undefined) {
            this.groups = hits.map((h) => ({ name: h.categoryName, count: h.count, categories: null }));
            this.viewMode = 'group';
            if (restoreState) setTimeout(() => this.superTable.applyCapturedHeaderState(), 500);
          } else {
            this.viewMode = 'grid';
            setTimeout(() => this.superTable.applyCapturedHeaderState(), 500);
          }
        });
    } else {
      this.supremeService
        .searchView({ ...viewParams, query: '*' })
        .pipe(map((res) => res.body?.hits ?? []))
        .subscribe((hits: any[]) => {
          if (hits.length > 0 && (hits[0] as any).categoryName !== undefined) {
            this.groups = hits.map((h) => ({ name: h.categoryName, count: h.count, categories: null }));
            this.viewMode = 'group';
            if (restoreState) setTimeout(() => this.superTable.applyCapturedHeaderState(), 500);
          } else {
            this.viewMode = 'grid';
            setTimeout(() => this.superTable.applyCapturedHeaderState(), 500);
          }
        });
    }
  }

  protected handleNavigation(): void {
    combineLatest([this.activatedRoute.data, this.activatedRoute.queryParamMap]).subscribe(([data, params]) => {
      const page = params.get('page');
      const pageSize = params.get('size');
      this.page = page !== null ? +page : 1;
      this.itemsPerPage = pageSize !== null ? +pageSize : this.itemsPerPage;
      this.predicate = 'name';
      this.ascending = true;
      const filter: any = {};
      if (this.currentQuery && this.currentQuery.trim().length > 0) {
        filter.bqlQuery = this.currentQuery.trim();
      } else {
        filter.luceneQuery = '*';
      }
      if (this.viewName) filter.view = this.viewName;
      this.dataLoader.load(this.itemsPerPage, this.predicate, this.ascending, filter);
    });
  }

  groupQuery(group: GroupDescriptor): GroupData {
    const path = group.categories ? [...group.categories, group.name] : [group.name];
    const params: any = { from: 0, pageSize: 1000, view: this.viewName! };
    if (path.length >= 1) params.category = path[0];
    if (path.length >= 2) params.secondaryCategory = path[1];

    const groupData: GroupData = { mode: 'group', groups: [] };
    const hasQuery = this.currentQuery && this.currentQuery.trim().length > 0;
    if (hasQuery) {
      this.supremeService
        .searchWithBql(this.currentQuery.trim(), params)
        .pipe(map((res) => res.body?.hits ?? []))
        .subscribe((hits: any[]) => {
          if (hits.length > 0 && (hits[0] as any).categoryName !== undefined) {
            groupData.groups = hits.map((h) => ({ name: h.categoryName, count: h.count, categories: path }));
            groupData.mode = 'group';
          } else {
            const fetch: FetchFunction<ISupreme> = (queryParams: any) => {
              if (queryParams.bqlQuery) {
                const bql = queryParams.bqlQuery;
                delete queryParams.bqlQuery;
                return this.supremeService.searchWithBql(bql, queryParams);
              }
              return this.supremeService.query(queryParams);
            };
            const loader = new DataLoader<ISupreme>(fetch);
            const filter: any = { view: this.viewName! };
            filter.bqlQuery = this.currentQuery.trim();
            if (path.length >= 1) filter.category = path[0];
            if (path.length >= 2) filter.secondaryCategory = path[1];
            loader.load(this.itemsPerPage, this.predicate, this.ascending, filter);
            groupData.mode = 'grid';
            groupData.loader = loader;
          }
        });
    } else {
      this.supremeService
        .searchView({ ...params, query: '*' })
        .pipe(map((res) => res.body?.hits ?? []))
        .subscribe((hits: any[]) => {
          if (hits.length > 0 && (hits[0] as any).categoryName !== undefined) {
            groupData.groups = hits.map((h) => ({ name: h.categoryName, count: h.count, categories: path }));
            groupData.mode = 'group';
          } else {
            const fetch: FetchFunction<ISupreme> = (queryParams: any) => {
              if (queryParams.bqlQuery) {
                const bql = queryParams.bqlQuery;
                delete queryParams.bqlQuery;
                return this.supremeService.searchWithBql(bql, queryParams);
              }
              return this.supremeService.query(queryParams);
            };
            const loader = new DataLoader<ISupreme>(fetch);
            const filter: any = { view: this.viewName!, query: '*' };
            if (path.length >= 1) filter.category = path[0];
            if (path.length >= 2) filter.secondaryCategory = path[1];
            loader.load(this.itemsPerPage, this.predicate, this.ascending, filter);
            groupData.mode = 'grid';
            groupData.loader = loader;
          }
        });
    }
    return groupData;
  }
}