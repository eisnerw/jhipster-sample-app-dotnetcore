/* eslint-disable */

import { Component, OnInit, ViewChild, TemplateRef, AfterViewInit, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { HttpClientModule, HttpResponse } from '@angular/common/http';
import { combineLatest, Subscription } from 'rxjs';
import { map } from 'rxjs/operators';

import { QueryInputComponent, bqlToRuleset } from 'popup-ngx-query-builder';
// Local minimal types to avoid cross-project import issues
type LocalRuleSet = { condition: string; rules: Array<LocalRuleSet | LocalRule>; name?: string; not?: boolean; isChild?: boolean };
type LocalRule = { field: string; operator: string; value?: any };
import { QueryLanguageSpec } from 'ngx-query-builder';
import { MenuItem, MessageService } from 'primeng/api';
import { MenuModule } from 'primeng/menu';
import { ContextMenuModule } from 'primeng/contextmenu';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { ChipModule } from 'primeng/chip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { DomSanitizer } from '@angular/platform-browser';

import SharedModule from 'app/shared/shared.module';
import { ViewService } from '../../view/service/view.service';
import { DataLoader, FetchFunction } from 'app/shared/data-loader';
import { SuperTable, ColumnConfig, GroupDescriptor, GroupData } from '../../../shared/SuperTable/super-table.component';

import { MovieService, EntityArrayResponseType, ViewArrayResponseType } from '../service/movie.service';
import { IMovie } from '../movie.model';

@Component({
  selector: 'jhi-movie',
  templateUrl: './movie.component.html',
  styleUrls: ['./movie.component.scss'],
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
    ContextMenuModule,
    ChipModule,
    ConfirmDialogModule,
    FontAwesomeModule,
  ],
  standalone: true,
})
export class MovieComponent implements OnInit, AfterViewInit {
  protected movieService = inject(MovieService);
  protected viewService = inject(ViewService);
  protected activatedRoute = inject(ActivatedRoute);
  protected router = inject(Router);
  protected messageService = inject(MessageService);
  sanitizer = inject(DomSanitizer);

  @ViewChild('superTable') superTable!: SuperTable;
  @ViewChild('expandedRow', { static: true }) expandedRowTemplate: TemplateRef<any> | undefined;
  @ViewChild(QueryInputComponent) queryInput!: QueryInputComponent;

  dataLoader: DataLoader<IMovie>;
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
  globalFilterFields: string[] = ['title', 'genres', 'country', 'languages'];
  showRowNumbers = false;

  expandedRowKeys: { [key: string]: boolean } = {};
  iframeSafeSrcById: Record<string, any> = {};

  columns: ColumnConfig[] = [
    { field: 'lineNumber', header: '#', type: 'lineNumber', width: '4rem' },
    { field: 'checkbox', header: '', type: 'checkbox', width: '2rem' },
    { field: 'title', header: 'Title', filterType: 'text', type: 'string', width: '220px' },
    { field: 'release_year', header: 'Year', filterType: 'numeric', type: 'string', width: '6rem' },
    { field: 'genres', header: 'Genres', filterType: 'text', type: 'string', width: '180px' },
    { field: 'runtimeMinutes', header: 'Runtime', filterType: 'numeric', type: 'string', width: '6rem' },
    { field: 'country', header: 'Country', filterType: 'text', type: 'string', width: '8rem' },
    { field: 'languages', header: 'Languages', filterType: 'text', type: 'string', width: '8rem' },
    { field: 'budget_usd', header: 'Budget', filterType: 'numeric', type: 'string', width: '8rem' },
    { field: 'gross_usd', header: 'Gross', filterType: 'numeric', type: 'string', width: '8rem' },
    { field: 'rotten_tomatoes_score', header: 'Rotten Tomatoes', filterType: 'numeric', type: 'string', width: '8rem' },
    { field: 'expander', header: '', type: 'expander', width: '25px', style: 'font-weight: 700;' },
  ];

  private lastSortEvent: any = null;
  private lastTableState: any;

  constructor() {
    const fetchFunction: FetchFunction<IMovie> = (queryParams: any) => {
      // Exclude large descriptive fields by default for grid
      if (queryParams.includeDetails === undefined) {
        queryParams.includeDetails = false;
      }
      if (queryParams.bqlQuery) {
        const bql = queryParams.bqlQuery;
        delete queryParams.bqlQuery;
        return this.movieService
          .searchWithBql(bql, queryParams)
          .pipe(map(res => this.useQuestionIfPresent(res)));
      }
      return this.movieService.query(queryParams).pipe(map(res => this.useQuestionIfPresent(res)));
    };
    this.dataLoader = new DataLoader<IMovie>(fetchFunction);
  }

  private useQuestionIfPresent(res: HttpResponse<any>): HttpResponse<any> {
    const hits = res.body?.hits;
    if (Array.isArray(hits)) {
      res.body.hits = hits.map((h: any) => ({
        ...h,
        description: h.question ?? h.description,
      }));
    }
    return res;
  }

  ngOnInit(): void {
    this.movieService.getQueryBuilderSpec().subscribe({
      next: spec => (this.spec = spec),
      error: () => (this.spec = undefined),
    });
    this.loadViews();
    this.handleNavigation();
  }

  // Parity helpers (chips, selection, context menu)
  selectionMode: 'single' | 'multiple' | null | undefined = 'multiple';
  selection: IMovie[] = [];
  checkboxSelectedRows: IMovie[] = [];
  chipSelectedRows: IMovie[] = [];
  chipMenuModel: MenuItem[] = [];
  menuItems: MenuItem[] = [];
  contextSelectedRow: IMovie | null = null;

  refreshData(): void {
    if (this.superTable) {
      this.superTable.filterGlobal('');
    }
    this.onQueryChange(this.currentQuery, true);
  }

  onViewChange(view: string | null): void {
    this.viewName = view;
    if (this.viewName) {
      try { this.superTable?.filterGlobal(''); } catch {}
      this.loadRootGroups();
    } else {
      this.groups = [];
      this.viewMode = 'grid';
      this.loadPage();
      setTimeout(() => this.superTable.applyCapturedHeaderState(), 500);
    }
  }

  trackById(index: number, row: IMovie): any {
    return (row as any)?.id ?? index;
  }

  onCheckboxChange(): void {
    this.checkboxSelectedRows = this.selection || [];
    this.chipSelectedRows = this.checkboxSelectedRows.slice(0, 2);
  }

  onChipMouseEnter(event: MouseEvent, row: IMovie): void {
    this.chipMenuModel = [
      { label: 'View', icon: 'pi pi-search', command: () => this.viewFromContext() },
      { label: 'Delete', icon: 'pi pi-trash', command: () => this.deleteFromChipMenu() },
    ];
    (this as any).chipMenu?.show(event);
  }

  onCountChipMouseEnter(event: MouseEvent): void {
    this.chipMenuModel = [
      { label: 'Delete', icon: 'pi pi-trash', command: () => this.deleteFromChipMenu() },
    ];
    (this as any).chipMenu?.show(event);
  }

  onRemoveChip(row: IMovie): void {
    const id = (row as any)?.id;
    this.selection = (this.selection || []).filter((r: any) => ((r?.id ?? r) !== id));
    this.onCheckboxChange();
  }

  onRemoveCountChip(): void {
    this.selection = [];
    this.onCheckboxChange();
  }

  onRowExpand(event: { originalEvent: Event; data: any }): void {
    const row = event.data as IMovie;
    const key = (row as any)?.id || JSON.stringify(row);
    this.expandedRowKeys[key] = true;
    setTimeout(() => {
      const url = '/api/entity/movie/html/' + (row as any).id;
      this.iframeSafeSrcById[(row as any).id!] = this.sanitizer.bypassSecurityTrustResourceUrl(url);
    }, 50);
  }

  onRowCollapse(event: any): void {
    const row = event.data as IMovie;
    const key = (row as any)?.id || JSON.stringify(row);
    delete this.expandedRowKeys[key];
    if ((row as any)?.id) {
      delete this.iframeSafeSrcById[(row as any).id];
    }
  }

  private getHighlightTermsFromBql(bql: string): string[] {
    const terms: string[] = [];
    if (!bql || !bql.trim()) return terms;
    try {
      const rs = bqlToRuleset(bql, this.queryInput.queryBuilderConfig) as LocalRuleSet;
      const visit = (node: LocalRuleSet | LocalRule) => {
        const asRule = node as LocalRule;
        if ((asRule as any).field !== undefined) {
          const field = (asRule.field || '').toLowerCase();
          const operator = (asRule.operator || '').toLowerCase();
          const value: any = (asRule as any).value;
          const pushVal = (v: any) => {
            if (v === null || v === undefined) return;
            const s = String(v).trim();
            if (s) terms.push(s);
          };
          if (operator && (operator.includes('contains') || operator.includes('like') || operator === '=' || operator === '==' || operator === 'in' || operator === '!in')) {
            if (Array.isArray(value)) value.forEach(pushVal);
            else pushVal(value);
          } else if (field === 'document') {
            if (Array.isArray(value)) value.forEach(pushVal);
            else pushVal(value);
          }
        } else {
          const asSet = node as LocalRuleSet;
          if (asSet && Array.isArray(asSet.rules)) asSet.rules.forEach(visit);
        }
      };
      visit(rs);
    } catch {}
    const dedup = Array.from(new Set(terms.map(t => t))).filter(t => t.length <= 256).slice(0, 50);
    return dedup;
  }

  onExpandedIframeLoad(id: string, ev: Event): void {
    try {
      const iframe = ev.target as HTMLIFrameElement;
      if (!iframe || !iframe.contentDocument) return;
      const doc = iframe.contentDocument;
      const terms = this.getHighlightTermsFromBql(this.currentQuery);
      if (!terms.length) return;
      const style = doc.createElement('style');
      style.textContent = '.__bql-hl{background:yellow; color:#111;}';
      doc.head?.appendChild(style);
      const esc = (s: string) => s.replace(/[.*+?^${}()|[\\]\\]/g, '\\$&');
      const regexes: { re: RegExp }[] = [];
      const words: string[] = [];
      for (const t of terms) {
        const m = t.match(/^\/(.*)\/([a-z]*)$/);
        if (m) {
          const body = m[1];
          const flags = (m[2] || '').includes('i') ? 'gi' : 'g';
          try { regexes.push({ re: new RegExp(body, flags) }); } catch {}
        } else {
          words.push(esc(t));
        }
      }
      if (words.length) {
        try { regexes.push({ re: new RegExp('(' + words.join('|') + ')', 'gi') }); } catch {}
      }
      if (regexes.length === 0) return;
      const walker = doc.createTreeWalker(doc.body, NodeFilter.SHOW_TEXT, null as any);
      const textNodes: Text[] = [];
      let n: any;
      while ((n = walker.nextNode())) {
        if (n && n.nodeValue && String(n.nodeValue).trim().length > 0) {
          textNodes.push(n as Text);
        }
      }
      textNodes.forEach(tn => {
        const parent = tn.parentNode as HTMLElement | null;
        if (!parent) return;
        const text = tn.nodeValue || '';
        type Range = { s: number; e: number };
        const ranges: Range[] = [];
        for (const { re } of regexes) {
          re.lastIndex = 0;
          let m: RegExpExecArray | null;
          while ((m = re.exec(text)) !== null) {
            const s = m.index;
            const e = s + (m[0]?.length || 0);
            if (e > s) ranges.push({ s, e });
            if (m[0].length === 0) re.lastIndex++;
          }
        }
        if (ranges.length === 0) return;
        ranges.sort((a, b) => a.s - b.s || a.e - b.e);
        const merged: Range[] = [];
        for (const r of ranges) {
          if (!merged.length || r.s > merged[merged.length - 1].e) {
            merged.push({ ...r });
          } else {
            merged[merged.length - 1].e = Math.max(merged[merged.length - 1].e, r.e);
          }
        }
        const frag = doc.createDocumentFragment();
        let last = 0;
        for (const r of merged) {
          if (r.s > last) frag.appendChild(doc.createTextNode(text.slice(last, r.s)));
          const mark = doc.createElement('mark');
          mark.className = '__bql-hl';
          mark.textContent = text.slice(r.s, r.e);
          frag.appendChild(mark);
          last = r.e;
        }
        if (last < text.length) frag.appendChild(doc.createTextNode(text.slice(last)));
        parent.replaceChild(frag, tn);
      });
    } catch {}
  }

  onContextMenuSelect(dataOrEvent: any): void {
    const row: IMovie | undefined = dataOrEvent && dataOrEvent.data ? dataOrEvent.data : dataOrEvent;
    if (!row) return;
    this.contextSelectedRow = row;
    this.setMenu(row);
  }

  onMenuShow(): void {
    this.setMenu(this.contextSelectedRow);
  }

  setMenu(row: IMovie | null): void {
    this.menuItems = [
      { label: 'View', icon: 'pi pi-search', command: () => this.viewFromContext() },
      { label: 'Delete', icon: 'pi pi-trash', command: () => this.deleteFromChipMenu() },
    ];
  }

  viewFromContext(): void {
    const row: any = this.contextSelectedRow || (this.selection && this.selection[0]);
    const id = row?.id;
    if (!id) return;
    this.router.navigate(['/movie', id, 'view']);
  }

  deleteFromChipMenu(): void {
    // hook up when delete implemented for Movie
  }

  ngAfterViewInit(): void {
    // On first render, seed equal widths by skipping one restore, then load
    try { (this.superTable as any).resetWidthState?.(); } catch {}
    this.onQueryChange(this.currentQuery);
  }

  loadViews(): void {
    this.viewService.queryByEntity('movie').subscribe((res) => {
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
    // Mirror Birthday: capture header state before loading, then restore after
    try {
      if (this.superTable) {
        this.superTable.captureHeaderState();
      }
    } catch {}
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
      this.movieService
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
      this.movieService
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
      this.predicate = 'title';
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
      this.movieService
        .searchWithBql(this.currentQuery.trim(), params)
        .pipe(map((res) => res.body?.hits ?? []))
        .subscribe((hits: any[]) => {
          if (hits.length > 0 && (hits[0] as any).categoryName !== undefined) {
            groupData.groups = hits.map((h) => ({ name: h.categoryName, count: h.count, categories: path }));
            groupData.mode = 'group';
            } else {
              const fetch: FetchFunction<IMovie> = (queryParams: any) => {
                if (queryParams.includeDetails === undefined) {
                  queryParams.includeDetails = false;
                }
                if (queryParams.bqlQuery) {
                  const bql = queryParams.bqlQuery;
                  delete queryParams.bqlQuery;
                  return this.movieService
                    .searchWithBql(bql, queryParams)
                    .pipe(map(res => this.useQuestionIfPresent(res)));
                }
                return this.movieService
                  .query(queryParams)
                  .pipe(map(res => this.useQuestionIfPresent(res)));
              };
              const loader = new DataLoader<IMovie>(fetch);
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
      this.movieService
        .searchView({ ...params, query: '*' })
        .pipe(map((res) => res.body?.hits ?? []))
        .subscribe((hits: any[]) => {
          if (hits.length > 0 && (hits[0] as any).categoryName !== undefined) {
            groupData.groups = hits.map((h) => ({ name: h.categoryName, count: h.count, categories: path }));
            groupData.mode = 'group';
            } else {
              const fetch: FetchFunction<IMovie> = (queryParams: any) => {
                if (queryParams.includeDetails === undefined) {
                  queryParams.includeDetails = false;
                }
                if (queryParams.bqlQuery) {
                  const bql = queryParams.bqlQuery;
                  delete queryParams.bqlQuery;
                  return this.movieService
                    .searchWithBql(bql, queryParams)
                    .pipe(map(res => this.useQuestionIfPresent(res)));
                }
                return this.movieService
                  .query(queryParams)
                  .pipe(map(res => this.useQuestionIfPresent(res)));
              };
              const loader = new DataLoader<IMovie>(fetch);
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