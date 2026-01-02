/* eslint-disable */
import { Component, OnInit, ViewChild, TemplateRef, AfterViewInit, inject, Input, HostListener, ChangeDetectorRef, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule, ActivatedRoute } from '@angular/router';
import { HttpClientModule } from '@angular/common/http';

import { NO_ERRORS_SCHEMA } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { MenuItem, MessageService, ConfirmationService } from 'primeng/api';
import { ContextMenuModule } from 'primeng/contextmenu';
import { Menu } from 'primeng/menu';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { CheckboxModule } from 'primeng/checkbox';
import { ButtonModule } from 'primeng/button';
import { MenuModule } from 'primeng/menu';
import { ChipModule } from 'primeng/chip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { forkJoin } from 'rxjs';

import SharedModule from 'app/shared/shared.module';
import { ViewService } from '../../view/service/view.service';
import { EntityGenericService } from '../service/entity-generic.service';
import { DataLoader, FetchFunction } from 'app/shared/data-loader';
import { QueryInputComponent,  bqlToRuleset} from 'popup-ngx-query-builder';
import { QueryLanguageSpec } from 'ngx-query-builder';
import { SuperTable, ColumnConfig, GroupData, GroupDescriptor } from 'app/shared/SuperTable/super-table.component';
import { GenericListActionResolver, GenericListActionContext, GenericListRow } from './generic-list-actions';

type LocalRuleSet = { condition: string; rules: Array<LocalRuleSet | LocalRule>; name?: string; not?: boolean; isChild?: boolean };
type LocalRule = { field: string; operator: string; value?: any };

type AnyRow = GenericListRow;
type MenuSpecItem = { action?: string; icon?: string; label?: string; items?: MenuSpecItem[] };

@Component({
  selector: 'jhi-generic-list',
  templateUrl: './generic-list.component.html',
  styleUrls: ['./generic-list.component.scss'],
  schemas: [NO_ERRORS_SCHEMA],
  providers: [MessageService, ConfirmationService],
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
    CheckboxModule,
    ContextMenuModule,
    ButtonModule,
    MenuModule,
    ChipModule,
    ConfirmDialogModule,
    FontAwesomeModule,
  ],
  standalone: true,
})
export class GenericListComponent implements OnInit, AfterViewInit, OnDestroy {
  @Input() entity!: string;
  @Input() pageTitle: string | null = null;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private sanitizer = inject(DomSanitizer);
  private viewService = inject(ViewService);
  private entityService = inject(EntityGenericService);
  private messageService = inject(MessageService);
  private confirmationService = inject(ConfirmationService);
  private cdr = inject(ChangeDetectorRef);
  private actionResolver = inject(GenericListActionResolver);

  @ViewChild('superTable') superTable!: SuperTable;
  @ViewChild('contextMenu') contextMenu: any;
  @ViewChild('chipMenu') chipMenu!: Menu;
  @ViewChild('expandedRow', { static: true }) expandedRowTemplate: TemplateRef<any> | undefined;
  @ViewChild(QueryInputComponent) queryInput!: QueryInputComponent;

  currentQuery = '';
  gridHighlightPattern = '';
  spec: QueryLanguageSpec | undefined;
  dataLoader!: DataLoader<AnyRow>;
  columns: ColumnConfig[] = [];
  initialWidths: (string | undefined)[] = [];
  specSignature: string | undefined;
  private baseSpecSignature: string | undefined;
  globalFilterFields: string[] = [];
  entitySpec: any = null;

  viewName: string | null = null;
  views: { label: string; value: string }[] = [];
  groups: GroupDescriptor[] = [];
  viewMode: 'grid' | 'group' = 'grid';

  // Loading state for group (view) category fetches
  groupLoading = false;
  groupLoadingMessage = 'Loading…';

  menuItems: MenuItem[] = [];
  contextSelectedRow: AnyRow | null = null;
  contextSelectedLabel: string | undefined;
  selectionMode: 'single' | 'multiple' | null | undefined = 'multiple';
  selection: AnyRow[] = [];
  checkboxSelectedRows: AnyRow[] = [];
  chipSelectedRows: AnyRow[] = [];
  chipMenuRow: AnyRow | null = null;
  chipMenuIsCount = false;
  chipMenuModel: MenuItem[] = [];
  showRowNumbers = false;

  showCategorizeDialog = false;
  allCategories: string[] = [];
  filteredCategories: string[] = [];
  categoryState: Record<string, boolean> = {};
  newCategoryText = '';
  newCategoryChecked = false;
  private tempNewCategory: string | null = null;
  rowsToCategorizeCount = 0;
  // Annotation helpers
  private annotationCache: Record<string, any[]> = {};
  private menuSpec: MenuSpecItem[] = [];
  private hasCustomMenuSpec = false;

  expandedRowKeys: { [key: string]: boolean } = {};
  iframeSafeSrcById: Record<string, any> = {};

  bDisplayDetail = false;
  detailDialogTitle = '';
  dialogSafeSrc: any = null;
  detailDialogId: string | null = null;

  itemsPerPage = 50;
  sort = '';
  private entitySession = 0;

  constructor() {
    const fetch: FetchFunction<AnyRow> = (queryParams: any) => {
      if (queryParams.bqlQuery) {
        const bql = queryParams.bqlQuery;
        delete queryParams.bqlQuery;
        return this.entityService.searchWithBql<AnyRow>(this.entity, bql, queryParams);
      }
      return this.entityService.query<AnyRow>(this.entity, queryParams);
    };
    this.dataLoader = new DataLoader<AnyRow>(fetch);
  }

  // legacy category pill support removed; handled via column annotations now

  ngOnInit(): void {
    this.route.paramMap.subscribe(pm => {
      const pEntity = pm.get('entity') || this.entity;
      this.initForEntity(pEntity);
    });

    const data = this.route.snapshot.data || {};
    if (!this.entity && data['entity']) {
      this.initForEntity(data['entity']);
    }
  }

  ngAfterViewInit(): void {
    //this.onQueryChange(this.currentQuery);
    try {
      document.addEventListener('mouseover', this.menuHoverListener, true);
    } catch {}
  }

  ngOnDestroy(): void {
    try {
      document.removeEventListener('mouseover', this.menuHoverListener, true);
    } catch {}
  }

  private menuLimitHandle: any = null;

  private menuHoverListener = (ev: Event) => {
    const target = ev.target as HTMLElement | null;
    if (!target) return;
    const menu = target.closest('.p-contextmenu-submenu, .p-menu-list, .p-submenu-list') as HTMLElement | null;
    if (menu) {
      this.scheduleMenuLimit(menu);
    }
  };

  private applyMenuHeightLimit(menuEl: HTMLElement): void {
    try {
      const rect = menuEl.getBoundingClientRect();
      const viewportHeight = window.innerHeight || document.documentElement.clientHeight || 0;
      const availableBelow = Math.max(0, viewportHeight - rect.top - 8);
      const maxHeight = Math.max(0, Math.min(menuEl.scrollHeight, availableBelow));
      menuEl.style.maxHeight = `${maxHeight}px`;
      if (availableBelow < menuEl.scrollHeight) {
        menuEl.style.height = `${maxHeight}px`;
      } else {
        menuEl.style.height = '';
      }
      menuEl.style.overflowY = 'auto';
      menuEl.style.overflowX = 'hidden';
    } catch {}
  }

  private scheduleMenuLimit(targetMenu?: HTMLElement): void {
    if (this.menuLimitHandle) {
      cancelAnimationFrame(this.menuLimitHandle);
    }
    const applyAll = () => {
      try {
        if (targetMenu) {
          this.applyMenuHeightLimit(targetMenu);
        }
        document.querySelectorAll('.p-contextmenu-submenu, .p-menu-list, .p-submenu-list').forEach(el => {
          this.applyMenuHeightLimit(el as HTMLElement);
        });
      } catch {}
    };
    const run = () => {
      requestAnimationFrame(() => {
        applyAll();
        setTimeout(applyAll, 25);
        setTimeout(applyAll, 75);
      });
    };
    this.menuLimitHandle = requestAnimationFrame(run);
  }

  private constrainAllMenusSoon(): void {
    this.scheduleMenuLimit();
  }

  private resetEntityScopedState(): void {
    this.superTable?.resetLayoutState();
    this.spec = undefined;
    this.columns = [];
    this.initialWidths = [];
    this.baseSpecSignature = undefined;
    this.specSignature = undefined;
    this.globalFilterFields = [];
    this.entitySpec = null;
    this.menuSpec = [];
    this.views = [];
    this.viewName = null;
    this.groups = [];
    this.viewMode = 'grid';
    this.groupLoading = false;
    this.groupLoadingMessage = 'Loading…';
    this.currentQuery = '';
    this.gridHighlightPattern = '';
    this.selection = [];
    this.checkboxSelectedRows = [];
    this.chipSelectedRows = [];
    this.chipMenuRow = null;
    this.chipMenuIsCount = false;
    this.chipMenuModel = [];
    this.contextSelectedRow = null;
    this.menuItems = [];
    this.expandedRowKeys = {};
    this.iframeSafeSrcById = {};
    this.annotationCache = {};
    this.bDisplayDetail = false;
    this.detailDialogTitle = '';
    this.dialogSafeSrc = null;
    this.detailDialogId = null;
    this.showCategorizeDialog = false;
    this.allCategories = [];
    this.filteredCategories = [];
    this.categoryState = {};
    this.newCategoryText = '';
    this.newCategoryChecked = false;
    this.tempNewCategory = null;
    this.rowsToCategorizeCount = 0;
    this.hasCustomMenuSpec = false;
    this.sort = '';
  }

  private isActiveSession(sessionId: number): boolean {
    return sessionId === this.entitySession;
  }

  private capitalize(s: string): string { return !s ? s : s.charAt(0).toUpperCase() + s.slice(1); }

  private loadViews(sessionId: number, entityName: string): void {
    this.viewService.queryByEntity(entityName).subscribe(res => {
      if (!this.isActiveSession(sessionId) || entityName !== this.entity) {
        return;
      }
      const body = res.body ?? [];
      this.views = body.map(v => ({ label: v.name!, value: v.id! }));
    });
  }

  private loadColumnsFromSpec(spec: any): void {
    if (!this.pageTitle && spec?.title) this.pageTitle = String(spec.title);
    const columns = this.buildColumnsFromSpec(spec);
    if (typeof console !== 'undefined') {
      try {
        console.info('[GenericList] loadColumnsFromSpec', {
          entity: this.entity,
          columnCount: columns.length,
          fields: columns.map(c => c.field),
        });
      } catch {}
    }
    this.columns = columns;
    this.initialWidths = this.columns.map(c => c.width || undefined);
    // Base signature tied to column spec
    this.baseSpecSignature = this.columns.map(c => `${c.field}:${c.width || ''}`).join('|');
    const vw = this.getStableViewportWidth();
    this.specSignature = `${this.baseSpecSignature}|vw:${vw}`;
    this.globalFilterFields = columns
      .filter(c => (c.type === 'string' || !c.type) && !!c.field && c.field !== 'lineNumber' && c.field !== 'checkbox')
      .map(c => c.field);
    setTimeout(() => this.superTable?.forceWidthRecompute(), 0);
  }

  // (annotations now parsed per column; legacy category pill loader removed)

  private initForEntity(e: string | null): void {
    if (!e) return;
    const sessionId = ++this.entitySession;
    this.entity = e;
    this.resetEntityScopedState();
    this.pageTitle = `${this.capitalize(this.entity)}s...`;

    const fetch: FetchFunction<AnyRow> = (queryParams: any) => {
      if (queryParams.bqlQuery) {
        const bql = queryParams.bqlQuery;
        delete queryParams.bqlQuery;
        return this.entityService.searchWithBql<AnyRow>(this.entity, bql, queryParams);
      }
      return this.entityService.query<AnyRow>(this.entity, queryParams);
    };
    this.dataLoader = new DataLoader<AnyRow>(fetch);

    forkJoin({
      spec: this.entityService.getQueryBuilderSpec(this.entity),
      entitySpec: this.entityService.getEntitySpec(this.entity)
    }).subscribe({
      next: ({ spec, entitySpec }) => {
        if (!this.isActiveSession(sessionId)) {
          return;
        }
        this.spec = spec;
        this.entitySpec = entitySpec;
        const normalizedMenu = this.normalizeMenuSpec(entitySpec?.menu);
        this.hasCustomMenuSpec = normalizedMenu.length > 0;
        this.menuSpec = normalizedMenu;

        // Ensure sort is initialized using entitySpec
        if (entitySpec?.sort) {
          this.sort = entitySpec.sort;
        }

        // Proceed to load columns and views after both specs are available
        this.loadColumnsFromSpec(entitySpec);
        this.loadViews(sessionId, this.entity);
        this.loadPage();
      },
      error: () => {
        if (!this.isActiveSession(sessionId)) {
          return;
        }
        this.spec = undefined;
        this.entitySpec = null;
        const normalizedMenu = this.normalizeMenuSpec(undefined);
        this.hasCustomMenuSpec = normalizedMenu.length > 0;
        this.menuSpec = normalizedMenu;

        // As a fallback, load columns and views even if spec fetch fails
        this.loadColumnsFromSpec(undefined);
        this.loadViews(sessionId, this.entity);
        this.loadPage();
      }
    });
  }

  private buildColumnsFromSpec(spec: any): ColumnConfig[] {
    const cols: ColumnConfig[] = [
      { field: 'lineNumber', header: '#', type: 'lineNumber', width: '4rem' },
      { field: 'checkbox', header: '', type: 'checkbox', width: '2rem' },
    ];
    const qbFieldsAll: Record<string, any> = spec?.fields || {};
    // Do not show these as visible columns by default
    const EXCLUDE = new Set<string>(['document', 'category', 'categories']);
    const df: any = spec?.detailFields;
    if (Array.isArray(df)) {
      for (const f of df) {
        if (f !== undefined && f !== null) EXCLUDE.add(String(f).toLowerCase());
      }
    }

    const columnsPref: Array<string | { field: string; header?: string; type?: string; dateFormat?: string }> = Array.isArray(spec?.columns) ? spec.columns : [];
    const listFields: Array<string | { field: string; header?: string; type?: string; dateFormat?: string }> = (columnsPref.length > 0 ? columnsPref : (spec?.listFields || []));
    if (Array.isArray(listFields) && listFields.length > 0) {
      for (const lf of listFields) {
        if (typeof lf === 'string') {
          if (EXCLUDE.has(lf.toLowerCase())) continue;
          const meta = qbFieldsAll[lf] || {};
          const header = (meta.column || meta.name || this.prettyHeader(lf));
          const width = this.normalizeWidth(meta.width);
          const t = String(meta.type || 'string').toLowerCase();
          const col: ColumnConfig = { field: lf, header, width } as any;
          if (t === 'date' || t === 'datetime') { col.type = 'date'; col.filterType = 'date'; col.dateFormat = meta.dateFormat || 'MM/dd/yyyy'; }
          else if (t === 'number' || t === 'numeric') { col.type = 'string'; col.filterType = 'numeric'; }
          else if (t === 'computed') {
            col.type = 'string'; col.filterType = 'text';
            const expr = String(meta.computation || '').trim();
            col.computeFields = this.parseCompute(expr);
            if (!col.header) col.header = this.prettyHeader(lf);
          }
          else if (t === 'boolean') { col.type = 'boolean'; col.filterType = 'boolean'; }
          else { col.type = 'string'; col.filterType = 'text'; }
          // Attach annotations, if any
          const anns = this.parseAnnotationsForField(spec, lf, meta);
          if (anns && anns.length) (col as any).annotations = anns;
          if (meta && Object.prototype.hasOwnProperty.call(meta, 'tooltip')) {
            (col as any).tooltipSpec = meta.tooltip;
          }
          cols.push(col);
        } else if (lf && typeof lf === 'object') {
          if (EXCLUDE.has(String(lf.field || '').toLowerCase())) continue;
          const meta = qbFieldsAll[lf.field] || {};
          const header = lf.header || meta.column || meta.name || this.prettyHeader(lf.field);
          const col: ColumnConfig = { field: lf.field, header } as any;
          const t = (lf.type || 'string').toLowerCase();
          if (t === 'date' || t === 'datetime') { col.type = 'date'; col.filterType = 'date'; col.dateFormat = lf.dateFormat || 'MM/dd/yyyy'; }
          else if (t === 'number' || t === 'numeric') { col.type = 'string'; col.filterType = 'numeric'; }
          else if (t === 'computed') {
            col.type = 'string'; col.filterType = 'text';
            const expr = String((lf as any).computation || meta.computation || '').trim();
            col.computeFields = this.parseCompute(expr);
          }
          else if (t === 'boolean') { col.type = 'boolean'; col.filterType = 'boolean'; }
          else { col.type = 'string'; col.filterType = 'text'; }
          if (!col.width && (lf as any).width) col.width = this.normalizeWidth((lf as any).width);
          if (!col.width && meta.width) col.width = this.normalizeWidth(meta.width);
          // Attach annotations, if any (prefer explicit lf.annotations over meta)
          const anns = this.parseAnnotationsForField(spec, lf.field, (lf as any).annotations ? { annotations: (lf as any).annotations } : meta);
          if (anns && anns.length) (col as any).annotations = anns;
          const tt = (lf as any).tooltip ?? meta.tooltip;
          if (tt !== undefined) (col as any).tooltipSpec = tt;
          cols.push(col);
        }
      }
    } else {
      const qbFields = qbFieldsAll;
      type F = { name?: string; column?: string; type?: string; options?: any[]; width?: string };
      const entries: Array<[string, F]> = (Object.entries(qbFields) as Array<[string, any]>)
        .filter(([k]) => !EXCLUDE.has(String(k).toLowerCase()))
        .map(([k, v]) => [k, v as F]);
      const score = (k: string, f: F): number => {
        const t = (f.type || '').toLowerCase();
        let s = 0;
        if (t === 'string' || t === 'category') s += 5;
        if (t === 'date') s += 4;
        if (t === 'number') s += 3;
        if (t === 'boolean') s += 2;
        const key = k.toLowerCase();
        if (/(title|name|lname|fname)/.test(key)) s += 5;
        if (/(release|year|dob|date)/.test(key)) s += 3;
        return s;
      };
      entries.sort((a, b) => score(b[0], b[1]) - score(a[0], a[1]));
      const pick = entries.slice(0, Math.min(6, entries.length));
      for (const [k, f] of pick) {
        const t = (f.type || '').toLowerCase();
        let col: ColumnConfig;
        const header = (f.column || f.name || this.prettyHeader(k));
        if (t === 'date' || t === 'datetime') col = { field: k, header, type: 'date', filterType: 'date', dateFormat: 'MM/dd/yyyy' };
        else if (t === 'boolean') col = { field: k, header, type: 'boolean', filterType: 'boolean' };
        else if (t === 'number') col = { field: k, header, type: 'string', filterType: 'numeric' };
        else if (t === 'computed') { col = { field: k, header, type: 'string', filterType: 'text', computeFields: this.parseCompute(String((f as any).computation || '')) } as any; }
        else if (t === 'category' && Array.isArray(f.options)) {
          col = { field: k, header, type: 'list', listOptions: (f.options || []).map((o: any) => ({ label: o.name || String(o.value), value: String(o.value) })) };
        } else {
          col = { field: k, header, type: 'string', filterType: 'text' };
        }
        if (f.width) col.width = this.normalizeWidth(f.width);
        const anns = this.parseAnnotationsForField(spec, k, f);
        if (anns && anns.length) (col as any).annotations = anns;
        if (Object.prototype.hasOwnProperty.call(f || {}, 'tooltip')) {
          (col as any).tooltipSpec = (f as any).tooltip;
        }
        cols.push(col);
      }
    }

    cols.push({ field: 'expander', header: '', type: 'expander', width: '25px', style: 'font-weight: 700;' });
    return cols;
  }

  private prettyHeader(k: string): string { return (k || '').replace(/_/g, ' ').replace(/\b\w/g, (m) => m.toUpperCase()); }

  private normalizeWidth(w: any): string | undefined {
    if (w === null || w === undefined) return undefined;
    const s = String(w).trim();
    if (!s) return undefined;
    if (/^(\d+\.?\d*)(px|rem|%)$/.test(s)) return s;
    const n = parseFloat(s);
    return isFinite(n) ? `${Math.max(1, Math.round(n))}px` : undefined;
  }

  private parseCompute(expr: string): string[] {
    if (!expr) return [];
    // Support syntax like "a | b | c" or "a||b"
    const parts = expr.split(/\|\|?|,/).map(s => s.trim()).filter(Boolean);
    return Array.from(new Set(parts));
  }

  onQueryChange(query: string, restoreState = false): void {
    this.currentQuery = query || '';
    this.gridHighlightPattern = this.buildHighlightPattern(this.currentQuery);
    if (this.viewName) {
      this.loadRootGroups(restoreState);
    } else {
      this.loadPage();
    }
  }

  loadRootGroups(restoreState: boolean = false, sessionId: number = this.entitySession): void {
    const activeView = this.viewName;
    if (!activeView) { this.groups = []; this.groupLoading = false; this.viewMode = 'grid'; this.loadPage(sessionId); setTimeout(() => this.superTable?.applyCapturedHeaderState(), 300); return; }
    const viewParams: any = { from: 0, pageSize: 1000, view: activeView };
    const hasQuery = this.currentQuery.trim().length > 0;
    // Signal that the category list is loading in group mode
    this.groupLoading = true;
    this.groupLoadingMessage = 'Loading categories…';
    if (hasQuery) {
      this.entityService.searchWithBql<any>(this.entity, this.currentQuery.trim(), viewParams).subscribe({
        next: (res: any) => {
          if (!this.isActiveSession(sessionId) || activeView !== this.viewName) {
            return;
          }
          const hits = res.body?.hits ?? [];
          if (hits.length > 0 && (hits[0] as any).categoryName !== undefined) {
            this.groups = hits.map((h: any) => ({ name: h.categoryName, count: h.count, categories: null }));
            this.viewMode = 'group';
            this.groupLoading = false;
            setTimeout(() => {
              if (!this.superTable) return;
              if (restoreState) {
                this.superTable.restoreState((this.superTable as any).captureState());
              } else {
                this.superTable.applyCapturedHeaderState();
              }
            }, 300);
          } else {
            this.groups = [];
            this.groupLoading = false; // switching to grid mode; detail loader will indicate
            const filter: any = { view: activeView };
            filter.bqlQuery = this.currentQuery.trim();
            this.dataLoader.load(this.itemsPerPage, this.sort, filter);
            this.viewMode = 'grid';
            setTimeout(() => this.superTable?.applyCapturedHeaderState(), 300);
          }
        },
        error: () => {
          if (!this.isActiveSession(sessionId) || activeView !== this.viewName) {
            return;
          }
          this.groupLoading = false;
          this.groups = [];
          this.viewMode = 'grid';
          this.loadPage(sessionId);
          setTimeout(() => this.superTable?.applyCapturedHeaderState(), 300);
        }
      });
    } else {
      this.entityService.searchView(this.entity, { ...viewParams, query: '*' }).subscribe({
        next: (res: any) => {
          if (!this.isActiveSession(sessionId) || activeView !== this.viewName) {
            return;
          }
          const hits = res.body?.hits ?? [];
          if (hits.length > 0 && (hits[0] as any).categoryName !== undefined) {
            this.groups = hits.map((h: any) => ({ name: h.categoryName, count: h.count, categories: null }));
            this.viewMode = 'group';
            this.groupLoading = false;
            setTimeout(() => {
              if (!this.superTable) return;
              if (restoreState) {
                this.superTable.restoreState((this.superTable as any).captureState());
              } else {
                this.superTable.applyCapturedHeaderState();
              }
            }, 300);
          } else {
            this.groups = [];
            this.groupLoading = false; // switching to grid mode; top-level loader takes over
            const filter: any = { view: activeView, query: '*' };
            this.dataLoader.load(this.itemsPerPage, this.sort, filter);
            this.viewMode = 'grid';
            setTimeout(() => this.superTable?.applyCapturedHeaderState(), 300);
          }
        },
        error: () => {
          if (!this.isActiveSession(sessionId) || activeView !== this.viewName) {
            return;
          }
          this.groupLoading = false;
          this.groups = [];
          this.viewMode = 'grid';
          this.loadPage(sessionId);
          setTimeout(() => this.superTable?.applyCapturedHeaderState(), 300);
        }
      });
    }
  }

  onViewChange(view: string | null): void {
    this.viewName = view;
    if (this.viewName) { try { this.superTable?.filterGlobal(''); } catch {} this.loadRootGroups(); }
    else { this.groups = []; this.viewMode = 'grid'; this.loadPage(); setTimeout(() => this.superTable?.applyCapturedHeaderState(), 500); }
  }

  loadPage(sessionId: number = this.entitySession): void {
    if (!this.isActiveSession(sessionId)) {
      return;
    }
    const filter: any = {};
    if (this.currentQuery && this.currentQuery.trim().length > 0) filter.bqlQuery = this.currentQuery.trim(); else filter.luceneQuery = '*';
    if (this.viewName) filter.view = this.viewName;
    this.dataLoader.load(this.itemsPerPage, this.sort, filter);
    setTimeout(() => this.superTable?.applyCapturedHeaderState(), 300);
  }

  refreshData(): void { try { this.superTable.captureHeaderState(); this.onQueryChange(this.currentQuery, true); } catch {} }

  private buildHighlightPattern(bql: string): string {
    const terms = this.getHighlightTermsFromBql(bql);
    if (!terms.length) {
      return '';
    }
    return terms.join(' ');
  }

  // Nudge SuperTable to recompute on resize and scope persistence to viewport width
  private resizeDebounce: any;
  @HostListener('window:resize')
  onWindowResize(): void {
    if (this.resizeDebounce) clearTimeout(this.resizeDebounce);
    this.resizeDebounce = setTimeout(() => {
      const vw = this.getStableViewportWidth();
      const base = this.baseSpecSignature || this.specSignature || '';
      this.specSignature = base ? `${base}|vw:${vw}` : `vw:${vw}`;
      try { (this.superTable as any)['lastColumnWidths'] = undefined; } catch {}
    }, 150);
  }

  // Prefer documentElement.clientWidth which excludes vertical scrollbar width,
  // making signatures stable across content-height changes.
  private getStableViewportWidth(): number {
    try {
      const docW = (typeof document !== 'undefined' && (document.documentElement?.clientWidth || 0)) || 0;
      const winW = (typeof window !== 'undefined' && (window.innerWidth || 0)) || 0;
      return docW || winW || 0;
    } catch { return 0; }
  }

  private regexMatch(pattern: string, value: string): boolean {
    try {
      // Interpret pattern directly as a regular expression (already JSON-escaped in spec).
      // Case-insensitive by default to preserve previous behavior.
      const re = new RegExp(String(pattern), 'i');
      return re.test(value || '');
    } catch {
      return false;
    }
  }

  // === Annotation parsing and renderers ===
  private parseAnnotationsForField(spec: any, fieldName: string, meta: any): any[] {
    try {
      const anns = (meta && Array.isArray(meta.annotations)) ? meta.annotations : [];
      if (!anns.length) return [];
      const key = `${this.entity}:${fieldName}`;
      const out: any[] = [];
      for (const ann of anns) {
        if (!ann || typeof ann !== 'object') continue;
        const type = String(ann.type || '').toLowerCase();
        const srcField = String(ann.field || fieldName);
        const ruleType = String(ann.ruleType || '').toLowerCase();
        const rules = Array.isArray(ann.rules) ? ann.rules : [];
        // Allow tooltip to be either a string expression or a mapping object { label: tooltip }
        // Keep reference without coercing to string so maps are preserved.
        const tooltipSpec: any = (ann && Object.prototype.hasOwnProperty.call(ann, 'tooltip')) ? (ann as any).tooltip : null;
        if (type === 'pill') {
          // Custom count-of-array pill: show count when the source field is a non-empty array
          if ((ann as any).countOf) {
            const pos = String((ann as any).position || 'before').toLowerCase() === 'after' ? 'after' : 'before';
            const render = (row: AnyRow) => {
              const v = row?.[srcField];
              if (!Array.isArray(v) || v.length === 0) return null;
              const text = String(v.length);
              const tooltip = this.resolveTooltip(row, tooltipSpec, text, text, srcField);
              return { text, tooltip };
            };
            out.push({ type: 'pill', render, pos });
          }
          if (ruleType === 'regex') {
            const compiled = this.compileRegexRules(rules);
            const pos = String((ann as any).position || 'before').toLowerCase() === 'after' ? 'after' : 'before';
            const render = (row: AnyRow) => {
              const v = row?.[srcField];
              if (v === null || v === undefined) return null;
              const arr = Array.isArray(v) ? v : [v];
              let label: string | null = null;
              for (const item of arr) {
                const s = String(item);
                for (const r of compiled) {
                  if (r.re.test(s)) { label = r.label; break; }
                }
                if (label) break;
              }
              if (label === null) {
                const def = String((ann as any).default || '').toLowerCase();
                if (def === 'countof' && Array.isArray(v) && v.length > 0) {
                  const text = String(v.length);
                  const tooltip = this.resolveTooltip(row, tooltipSpec, text, text, srcField);
                  return { text, tooltip };
                }
                return null;
              } else {
                const text = label + (Array.isArray(v) && v.length > 1 ? '+' : '');
                const tooltip = this.resolveTooltip(row, tooltipSpec, label, text, srcField);
                return { text, tooltip };
              }
            };
            out.push({ type: 'pill', render, pos });
          } else if (ruleType === 'compare') {
            const compiled = this.compileCompareRules(rules);
            const pos = String((ann as any).position || 'before').toLowerCase() === 'after' ? 'after' : 'before';
            const render = (row: AnyRow) => {
              const raw = row?.[srcField];
              const num = typeof raw === 'number' ? raw : parseFloat(String(raw));
              if (!isFinite(num)) return null;
              for (const r of compiled) {
                if (this.compare(num, r.op, r.value)) {
                  const text = r.label;
                  const tooltip = this.resolveTooltip(row, tooltipSpec, r.label, text, srcField);
                  return { text, tooltip };
                }
              }
              return null;
            };
            out.push({ type: 'pill', render, pos });
          }
        } else if (type === 'linkpill') {
          if (ruleType === 'compare') {
            const compiled = this.compileCompareRules(rules, true);
            const linkTmpl = String((ann as any).link || '');
            const pos = String((ann as any).position || 'before').toLowerCase() === 'after' ? 'after' : 'before';
            const render = (row: AnyRow) => {
              const raw = row?.[srcField];
              // Evaluate compare rules. Support 'exists' for linkPill.
              const exists = !(raw === null || raw === undefined || String(raw).trim() === '');
              for (const r of compiled) {
                if (r.op === 'exists') {
                  if (exists) {
                    const text = r.label;
                    const tooltip = this.resolveTooltip(row, tooltipSpec, r.label, text, srcField);
                    const link = this.resolveTemplateString(linkTmpl, row) || String(raw || '');
                    if (!link) return null;
                    return { text, tooltip, link };
                  }
                } else {
                  // Fallback numeric compare for linkPill if provided
                  const num = typeof raw === 'number' ? raw : parseFloat(String(raw));
                  if (!isFinite(num)) continue;
                  if (this.compare(num, r.op, r.value as any)) {
                    const text = r.label;
                    const tooltip = this.resolveTooltip(row, tooltipSpec, r.label, text, srcField);
                    const link = this.resolveTemplateString(linkTmpl, row) || String(raw || '');
                    if (!link) return null;
                    return { text, tooltip, link };
                  }
                }
              }
              return null;
            };
            out.push({ type: 'linkPill', render, pos });
          } else if (ruleType === 'regex') {
            const compiled = this.compileRegexRules(rules);
            const linkTmpl = String((ann as any).link || '');
            const pos = String((ann as any).position || 'before').toLowerCase() === 'after' ? 'after' : 'before';
            const render = (row: AnyRow) => {
              const v = row?.[srcField];
              if (v === null || v === undefined) return null;
              const arr = Array.isArray(v) ? v : [v];
              let label: string | null = null;
              for (const item of arr) {
                const s = String(item);
                for (const r of compiled) {
                  if (r.re.test(s)) { label = r.label; break; }
                }
                if (label) break;
              }
              if (label === null) return null;
              const text = label + (Array.isArray(v) && v.length > 1 ? '+' : '');
              const tooltip = this.resolveTooltip(row, tooltipSpec, label, text, srcField);
              const link = this.resolveTemplateString(linkTmpl, row) || '';
              if (!link) return null;
              return { text, tooltip, link };
            };
            out.push({ type: 'linkPill', render, pos });
          }
        } else if (type === 'link') {
          const linkTmpl = String((ann as any).link || '');
          const render = (row: AnyRow) => {
            const raw = row?.[srcField];
            const has = !(raw === null || raw === undefined || String(raw).trim() === '');
            if (!has) return null;
            const link = this.resolveTemplateString(linkTmpl, row) || String(raw || '');
            if (!link) return null;
            return { link };
          };
          out.push({ type: 'link', render });
        }
      }
      return out;
    } catch { return []; }
  }

  private compileRegexRules(rules: any[]): Array<{ re: RegExp; label: string }> {
    const out: Array<{ re: RegExp; label: string }> = [];
    for (const r of rules) {
      const [pat, label] = Object.entries(r || {})[0] || [null, null];
      if (!pat) continue;
      try { out.push({ re: new RegExp(String(pat), 'i'), label: String(label ?? '') }); } catch {}
    }
    return out;
  }

  private compileCompareRules(rules: any[], allowExists: boolean = false): Array<{ op: string; value?: number | null; label: string }> {
    const out: Array<{ op: string; value?: number | null; label: string }> = [];
    for (const r of rules) {
      const [k, label] = Object.entries(r || {})[0] || [null, null];
      if (!k) continue;
      const key = String(k).trim();
      if (allowExists && key.toLowerCase() === 'exists') {
        out.push({ op: 'exists', value: null, label: String(label ?? '') });
        continue;
      }
      const parts = key.split(',');
      if (parts.length !== 2) continue;
      const op = parts[0].trim();
      const val = parseFloat(parts[1]);
      if (!isFinite(val)) continue;
      out.push({ op, value: val, label: String(label ?? '') });
    }
    return out;
  }

  private compare(num: number, op: string, val: number | null | undefined): boolean {
    if (op === 'exists') {
      return !(num === null || num === undefined || !isFinite(num));
    }
    if (val === null || val === undefined || !isFinite(val as number)) {
      return false;
    }
    const v = val as number;
    switch (op) {
      case '>': return num > v;
      case '>=': return num >= v;
      case '<': return num < v;
      case '<=': return num <= v;
      case '=':
      case '==': return num === v;
      case '!=': return num !== v;
      default: return false;
    }
  }

  // Replace {field} tokens in a template string with corresponding row values
  private resolveTemplateString(tmpl: string, row: AnyRow): string {
    try {
      if (!tmpl) return '';
      return String(tmpl).replace(/\{([^{}:\s]+)(?::[^{}]+)?\}/g, (_m: string, g1: string) => {
        const v = row?.[g1];
        return v === null || v === undefined ? '' : String(v);
      });
    } catch { return ''; }
  }

  private evalTooltip(row: AnyRow, expr: string | null, contextField?: string, fallbackValue?: any): string | null {
    if (!expr) return null;
    try {
      const s = expr.trim();
      // Support: fieldName
      const simple = s.match(/^([a-zA-Z_][a-zA-Z0-9_]*)$/);
      if (simple) {
        const v = row?.[simple[1]];
        if (Array.isArray(v)) return v.join(', ');
        return v === undefined || v === null ? null : String(v);
      }
      // Support: fieldName.join(', ')
      const join = s.match(/^([a-zA-Z_][a-zA-Z0-9_]*)\.join\((.*)\)$/);
      if (join) {
        const v = row?.[join[1]];
        if (!Array.isArray(v)) return null;
        let sep = join[2].trim();
        const m = sep.match(/^['\"](.*)['\"]$/);
        sep = m ? m[1] : ', ';
        return v.join(sep);
      }
      // Fallback: evaluate a constrained expression by injecting row fields as arguments
      // Only exposes provided fields and Math. No window/document.
      const keys = Object.keys(row || {});
      const values = keys.map(k => (row as any)[k]);
      // Provide a 'value' symbol mapped to the current field's value when available.
      let valueSym: any = undefined;
      if (contextField && Object.prototype.hasOwnProperty.call(row || {}, contextField)) {
        valueSym = (row as any)[contextField];
      } else if (fallbackValue !== undefined) {
        valueSym = fallbackValue;
      }
      const fn = new Function(...[...keys, 'value', 'Math'], 'return ( ' + s + ' );');
      const val = fn(...values, valueSym, Math);
      if (val === undefined || val === null) return null;
      if (Array.isArray(val)) return val.join(', ');
      return String(val);
    } catch { return null; }
  }

  // Resolve tooltip from either a string expression or a mapping object keyed by label/text.
  // If a string expression fails to evaluate, fall back to treating it as a literal string.
  private resolveTooltip(row: AnyRow, spec: any, rawLabel: string, finalText: string, contextField?: string): string | null {
    try {
      if (spec === null || spec === undefined) return null;
      // String expression path (backward compatible)
      if (typeof spec === 'string') {
        const v = this.evalTooltip(row, spec, contextField, finalText);
        return v === null ? String(spec) : v;
      }
      // Object map path: prefer raw rule label, then rendered text
      if (typeof spec === 'object' && !Array.isArray(spec)) {
        const map = spec as Record<string, any>;
        if (Object.prototype.hasOwnProperty.call(map, rawLabel)) {
          const v = map[rawLabel];
          return v === null || v === undefined ? null : String(v);
        }
        if (Object.prototype.hasOwnProperty.call(map, finalText)) {
          const v = map[finalText];
          return v === null || v === undefined ? null : String(v);
        }
        return null;
      }
      // Unknown type
      return null;
    } catch {
      return null;
    }
  }

  onCheckboxChange(): void { this.checkboxSelectedRows = this.selection || []; this.chipSelectedRows = this.checkboxSelectedRows.slice(0, 2); }
  getChipLabel(row: AnyRow): string { const titleFields = ['title','name','lname','fname']; let text = ''; for (const f of titleFields) { if (row[f]) { text = text ? `${text} ${row[f]}` : `${row[f]}`; } } return text || (row.id || ''); }
  onChipMouseEnter(event: MouseEvent, row: AnyRow): void { this.chipMenuIsCount = false; this.chipMenuRow = row; this.setMenu(row, true); this.chipMenu?.show(event); }
  onCountChipMouseEnter(event: MouseEvent): void { this.chipMenuIsCount = true; this.chipMenuRow = null; this.setMenu(null, true); this.chipMenu?.show(event); }
  onChipMouseLeave(): void { this.chipMenu?.hide(); }
  onRemoveChip(row: AnyRow): void { const id = row?.id; if (!id) return; this.selection = (this.selection || []).filter(r => (r?.id ?? r) !== id); this.onCheckboxChange(); }
  onRemoveCountChip(): void { this.selection = []; this.onCheckboxChange(); }
  onContextMenuSelect(dataOrEvent: any): void { const row: AnyRow | undefined = dataOrEvent && dataOrEvent.data ? dataOrEvent.data : dataOrEvent; if (!row) return; this.contextSelectedRow = row; this.setMenu(row, false); this.constrainAllMenusSoon(); }
  onMenuShow(): void { this.setMenu(this.contextSelectedRow, false); this.constrainAllMenusSoon(); }

  private setMenu(row: AnyRow | null, isChipMenu: boolean = false): void {
    this.contextSelectedLabel = undefined;
    const items = this.buildMenuItems(row, isChipMenu);
    if (isChipMenu) {
      this.chipMenuModel = items;
    } else {
      this.menuItems = items;
    }
  }

  private buildMenuItems(row: AnyRow | null, isChipMenu: boolean): MenuItem[] {
    const spec = this.hasCustomMenuSpec ? this.menuSpec : this.defaultMenuSpec();
    return spec
      .map(entry => this.createMenuItem(entry, row, isChipMenu))
      .filter((i): i is MenuItem => !!i);
  }

  private createMenuItem(entry: MenuSpecItem | null | undefined, row: AnyRow | null, isChipMenu: boolean, parentActionKey?: string): MenuItem | null {
    if (!entry) return null;
    const actionKey = entry.action ? String(entry.action).toLowerCase() : undefined;
    const effectiveActionKey = actionKey || parentActionKey;
    if (!effectiveActionKey && (!entry.items || entry.items.length === 0)) return null;

    const resolvedAction = effectiveActionKey ? this.actionResolver.resolve(this.entity, effectiveActionKey) : null;
    if (actionKey && !resolvedAction) return null;

    if (actionKey) {
      const ctx: GenericListActionContext = this.buildActionContext(row, isChipMenu, effectiveActionKey!);
      if (resolvedAction?.isEnabled && !resolvedAction.isEnabled(ctx)) return null;
    }

    const label = entry.label || (entry.action ? this.prettyHeader(entry.action) : (parentActionKey ? this.prettyHeader(parentActionKey) : ''));
    const childItems = (entry.items || [])
      .map(child => this.createMenuItem(child, row, isChipMenu, effectiveActionKey))
      .filter((c): c is MenuItem => !!c);

    const command = (!childItems.length && resolvedAction?.run)
      ? () => {
          if (parentActionKey) {
            this.contextSelectedLabel = label;
          } else {
            this.contextSelectedLabel = undefined;
          }
          const ctx: GenericListActionContext = this.buildActionContext(row, isChipMenu, effectiveActionKey!);
          resolvedAction.run(ctx);
        }
      : undefined;

    const item: MenuItem = {
      label,
      icon: entry.icon,
      items: childItems.length ? childItems : undefined,
      command,
    };
    return item;
  }

  private buildActionContext(row: AnyRow | null, isChipMenu: boolean, actionKey: string): GenericListActionContext {
    const resolvedRow = this.resolveMenuRow(row);
    const ctx: GenericListActionContext = {
      entity: this.entity,
      actionKey,
      rawRow: row,
      resolvedRow,
      isChipMenu,
      selection: this.selection || [],
      chipMenuRow: this.chipMenuRow,
      contextSelectedRow: this.contextSelectedRow,
      contextSelectedLabel: this.contextSelectedLabel,
      getContextLabel: () => this.contextSelectedLabel,
      setContextLabel: label => {
        this.contextSelectedLabel = label;
        ctx.contextSelectedLabel = label;
      },
      helpers: {
        deleteFromContext: () => this.deleteFromContext(),
        openCategorizeDialog: () => this.openCategorizeDialog(),
        viewIframeFromContext: () => this.viewIframeFromContext(),
        editFromContext: () => this.editFromContext(),
        hasAnyMenuSelection: r => this.hasAnyMenuSelection(r),
      },
    };
    return ctx;
  }

  private defaultMenuSpec(): MenuSpecItem[] {
    return [
      { action: 'Categorize', icon: 'pi pi-tags', label: 'Categorize' },
      { action: 'View', icon: 'pi pi-search', label: 'View' },
      { action: 'Edit', icon: 'pi pi-pencil', label: 'Edit' },
      { action: 'Delete', icon: 'pi pi-trash', label: 'Delete' },
    ];
  }

  private normalizeMenuSpec(raw: any): MenuSpecItem[] {
    if (!Array.isArray(raw)) return [];
    const normalizeEntry = (item: any): MenuSpecItem | null => {
      if (!item || typeof item !== 'object') return null;
      const action = (item as any).action ?? (item as any).Action;
      const icon = (item as any).icon ?? (item as any).Icon;
      const label = (item as any).label ?? (item as any).Label;
      const childrenRaw = (item as any).items ?? (item as any).Items;

      const children = Array.isArray(childrenRaw)
        ? childrenRaw
            .map(child => normalizeEntry(child))
            .filter((c): c is MenuSpecItem => !!c)
        : [];

      const hasAction = !(action === null || action === undefined || action === '');
      if (!hasAction && children.length === 0 && label === undefined && icon === undefined) return null;

      const entry: MenuSpecItem = {};
      if (hasAction) entry.action = String(action);
      if (icon !== undefined) entry.icon = String(icon);
      if (label !== undefined) entry.label = String(label);
      if (children.length) entry.items = children;
      return entry;
    };
    const normalized = raw
      .map(item => normalizeEntry(item))
      .filter((i): i is MenuSpecItem => !!i);
    return normalized;
  }

  private resolveMenuRow(row: AnyRow | null): AnyRow | null {
    return row || this.contextSelectedRow || this.chipMenuRow || (this.selection?.[0] ?? null);
  }

  private hasAnyMenuSelection(row: AnyRow | null): boolean {
    if (row) return true;
    if (this.contextSelectedRow) return true;
    if (this.chipMenuRow) return true;
    return (this.selection?.length ?? 0) > 0;
  }

  private setDetailDialogSrc(id: string | null): void {
    if (!id) {
      this.dialogSafeSrc = null;
      return;
    }
    const url = `/api/entity/${encodeURIComponent(this.entity)}/html/${encodeURIComponent(String(id))}`;
    this.dialogSafeSrc = this.sanitizer.bypassSecurityTrustResourceUrl(url);
    try { this.cdr.detectChanges(); } catch {}
  }

  private setExpandedIframeSrc(id: string | undefined | null): void {
    if (!id) return;
    const url = `/api/entity/${encodeURIComponent(this.entity)}/html/${encodeURIComponent(String(id))}`;
    this.iframeSafeSrcById[id] = this.sanitizer.bypassSecurityTrustResourceUrl(url);
    try { this.cdr.detectChanges(); } catch {}
  }

  openCategorizeDialog(): void {
    const rows = this.chipMenuIsCount ? (this.selection || []) : (this.chipMenuRow ? [this.chipMenuRow] : (this.selection?.length ? this.selection : (this.contextSelectedRow ? [this.contextSelectedRow] : [])));
    this.chipMenuRow = null; this.chipMenuIsCount = false;
    if (!rows.length) { this.messageService.add({ severity: 'warn', summary: 'No rows selected' }); return; }
    this.rowsToCategorizeCount = rows.length;
    // Fetch all categories for this entity (union across index)
    this.entityService.getUniqueFieldValues(this.entity, 'categories').subscribe({
      next: (list: string[]) => {
        const all = Array.from(new Set(list || [])).filter(Boolean);
        // Also include any categories present on selected rows even if not in the index-wide list
        const unionSel = new Set<string>();
        rows.forEach(r => (r.categories || []).forEach((c: string) => unionSel.add(c)));
        const allCombined = Array.from(new Set([...all, ...Array.from(unionSel)])).sort((a,b)=>a.localeCompare(b));
        // Determine which categories are common across all selected rows
        const isCommon = (cat: string) => rows.every(r => Array.isArray(r.categories) && r.categories.includes(cat));
        // Build state: checked if common, otherwise unchecked. Checked ones first in ordering
        const checkedCats: string[] = [];
        const uncheckedCats: string[] = [];
        this.categoryState = {};
        for (const c of allCombined) {
          if (isCommon(c)) { this.categoryState[c] = true; checkedCats.push(c); } else { this.categoryState[c] = false; uncheckedCats.push(c); }
        }
        this.allCategories = [...checkedCats.sort((a,b)=>a.localeCompare(b)), ...uncheckedCats.sort((a,b)=>a.localeCompare(b))];
        this.filteredCategories = [...this.allCategories];
        this.newCategoryText = ''; this.newCategoryChecked = false; this.showCategorizeDialog = true;
      },
      error: () => {
        // Fallback: derive from selected rows only
        const union = new Set<string>(); rows.forEach(r => (r.categories || []).forEach((c: string) => union.add(c)));
        const allCombined = Array.from(union.values()).sort((a,b)=>a.localeCompare(b));
        const isCommon = (cat: string) => rows.every(r => Array.isArray(r.categories) && r.categories.includes(cat));
        this.categoryState = {};
        const checked: string[] = []; const unchecked: string[] = [];
        for (const c of allCombined) { if (isCommon(c)) { this.categoryState[c] = true; checked.push(c); } else { this.categoryState[c] = false; unchecked.push(c);} }
        this.allCategories = [...checked, ...unchecked];
        this.filteredCategories = [...this.allCategories];
        this.newCategoryText = ''; this.newCategoryChecked = false; this.showCategorizeDialog = true;
      }
    });
  }
  hasCategorizeChanges(): boolean { return true; }
  cancelCategorize(): void { this.showCategorizeDialog = false; }
  filterCategoriesList(): void {
    const raw = (this.newCategoryText || '').trim();
    const q = raw.toLowerCase();
    const src = this.allCategories;
    let arr = !q ? [...src] : src.filter(c => c.toLowerCase().includes(q));
    // Dynamically add a new entry when there are no matches and the filter has content
    if (q && arr.length === 0) {
      // carry forward check state from previous temp if any
      const prev = this.tempNewCategory;
      this.tempNewCategory = raw;
      if (prev && prev !== this.tempNewCategory) {
        const prevChecked = !!this.categoryState[prev];
        // remove old temp entry state to avoid clutter
        delete this.categoryState[prev];
        // initialize new temp with previous checked state
        this.categoryState[this.tempNewCategory] = prevChecked;
      } else if (!(this.tempNewCategory in this.categoryState)) {
        // default new category to checked when first shown
        this.categoryState[this.tempNewCategory] = true;
      }
      arr = [this.tempNewCategory];
    } else {
      // If matches exist, clear temp new entry so it doesn't linger
      if (this.tempNewCategory && !arr.includes(this.tempNewCategory)) {
        // keep state only if it exists in the full list
        delete this.categoryState[this.tempNewCategory];
      }
      this.tempNewCategory = null;
    }
    // Always include currently-checked categories regardless of filter text
    if (Object.keys(this.categoryState).length) {
      const ensured = new Set(arr);
      for (const c of Object.keys(this.categoryState)) { if (this.categoryState[c]) ensured.add(c); }
      arr = Array.from(ensured);
    }

    // Sort with checked first, then alphabetical
    arr.sort((a,b) => {
      const sa = this.categoryState[a] ? 0 : 1;
      const sb = this.categoryState[b] ? 0 : 1;
      if (sa !== sb) return sa - sb;
      return a.localeCompare(b);
    });
    this.filteredCategories = arr;
  }
  toggleCategory(cat: string): void { this.categoryState[cat] = !this.categoryState[cat]; this.filterCategoriesList(); }
  applyCategorize(): void {
    const rows = this.selection && this.selection.length > 0 ? this.selection : (this.contextSelectedRow ? [this.contextSelectedRow] : []);
    if (!rows.length) { this.showCategorizeDialog = false; return; }
    const add: string[] = []; const remove: string[] = [];
    for (const c of Object.keys(this.categoryState)) {
      if (this.categoryState[c]) add.push(c); else remove.push(c);
    }
    let newCat = (this.newCategoryText || '').trim();
    if (newCat && this.newCategoryChecked && !add.some(a => a.toLowerCase() === newCat.toLowerCase())) add.push(newCat);
    const rowIds = rows.map(r => r.id).filter(Boolean) as string[];
    const payload = { rows: rowIds, add, remove };
    this.entityService.categorize(this.entity, payload).subscribe({ next: () => { this.showCategorizeDialog = false; this.refreshData(); }, error: () => { this.showCategorizeDialog = false; this.refreshData(); } });
  }

  viewIframeFromContext(): void {
    const row = (this.contextSelectedRow || (this.selection && this.selection[0])) as AnyRow | undefined;
    const id = row?.id;
    if (!id) return;
    this.detailDialogId = id;
    this.detailDialogTitle = this.buildTitle(row);
    this.setDetailDialogSrc(id);
    this.bDisplayDetail = true;
  }
  onDetailDialogShow(): void {
    this.setDetailDialogSrc(this.detailDialogId);
  }
  onDetailDialogHide(): void { this.dialogSafeSrc = null; }
  editFromContext(): void { const row = this.contextSelectedRow || (this.selection && this.selection[0]); const id = row?.id; if (!id) return; this.router.navigate(['/entity', this.entity, id, 'edit']); }

  deleteFromContext(): void {
    if (this.chipMenuRow || this.chipMenuIsCount) {
      this.deleteFromChipMenu();
      return;
    }
    const row = this.contextSelectedRow || (this.selection && this.selection[0]);
    const id = row?.id;
    if (!id) return;
    this.entityService.delete(this.entity, id).subscribe({ next: () => this.refreshData(), error: () => this.refreshData() });
  }
  deleteFromChipMenu(): void { const rows: AnyRow[] = this.chipMenuIsCount ? (this.selection || []) : (this.chipMenuRow ? [this.chipMenuRow] : []); this.chipMenuRow = null; this.chipMenuIsCount = false; const ids = rows.map(r => r.id).filter(Boolean) as string[]; if (!ids.length) return; const confirmAndRun = () => { const next = (remaining: string[]) => { if (!remaining.length) { this.refreshData(); return; } const id = remaining.shift()!; this.entityService.delete(this.entity, id).subscribe({ next: () => next(remaining), error: () => next(remaining) }); }; next([...ids]); }; if (ids.length > 1) { this.confirmationService.confirm({ header: 'Confirm Delete', icon: 'pi pi-exclamation-triangle', message: `Delete ${ids.length} item(s)?`, acceptLabel: 'Yes', rejectLabel: 'No', accept: () => confirmAndRun() }); } else { confirmAndRun(); } }

  onRowExpand(event: { originalEvent: Event; data: AnyRow }): void {
    const row = event.data as AnyRow;
    const key = row?.id || JSON.stringify(row);
    this.expandedRowKeys[key] = true;
    this.setExpandedIframeSrc(row?.id);
  }
  onRowCollapse(event: any): void {
    const row = event.data as AnyRow;
    const key = row?.id || JSON.stringify(row);
    delete this.expandedRowKeys[key];
    if (row?.id) delete this.iframeSafeSrcById[row.id];
  }

  groupQuery(group: GroupDescriptor): GroupData {
    const path = group.categories ? [...group.categories, group.name] : [group.name];
    const params: any = { from: 0, pageSize: 1000, view: this.viewName! };
    if (path.length >= 1) params.category = path[0];
    if (path.length >= 2) params.secondaryCategory = path[1];
    const groupData: GroupData = { mode: 'group', groups: [] };
    const hasQuery = this.currentQuery && this.currentQuery.trim().length > 0;
    if (hasQuery) {
      this.entityService.searchWithBql<any>(this.entity, this.currentQuery.trim(), params).subscribe((res: any) => {
        const hits = res.body?.hits ?? [];
        if (hits.length > 0 && (hits[0] as any).categoryName !== undefined) {
          groupData.groups = hits.map((h: any) => ({ name: h.categoryName, count: h.count, categories: path }));
          groupData.mode = 'group';
        } else {
          const fetch: FetchFunction<AnyRow> = (q: any) => { if (q.bqlQuery) { const bql = q.bqlQuery; delete q.bqlQuery; return this.entityService.searchWithBql<AnyRow>(this.entity, bql, q); } return this.entityService.query<AnyRow>(this.entity, q); };
          const loader = new DataLoader<AnyRow>(fetch);
          const filter: any = { view: this.viewName! };
          filter.bqlQuery = this.currentQuery.trim();
          if (path.length >= 1) filter.category = path[0];
          if (path.length >= 2) filter.secondaryCategory = path[1];
          loader.load(this.itemsPerPage, this.sort, filter);
          groupData.mode = 'grid';
          groupData.loader = loader;
        }
      });
    } else {
      this.entityService.searchView(this.entity, { ...params, query: '*' }).subscribe((res: any) => {
        const hits = res.body?.hits ?? [];
        if (hits.length > 0 && (hits[0] as any).categoryName !== undefined) {
          groupData.groups = hits.map((h: any) => ({ name: h.categoryName, count: h.count, categories: path }));
          groupData.mode = 'group';
        } else {
          const fetch: FetchFunction<AnyRow> = (q: any) => { if (q.bqlQuery) { const bql = q.bqlQuery; delete q.bqlQuery; return this.entityService.searchWithBql<AnyRow>(this.entity, bql, q); } return this.entityService.query<AnyRow>(this.entity, q); };
          const loader = new DataLoader<AnyRow>(fetch);
          const filter: any = { view: this.viewName!, query: '*' };
          if (path.length >= 1) filter.category = path[0];
          if (path.length >= 2) filter.secondaryCategory = path[1];
          loader.load(this.itemsPerPage, this.sort, filter);
          groupData.mode = 'grid';
          groupData.loader = loader;
        }
      });
    }
    return groupData;
  }

  private buildTitle(row: AnyRow): string { const name = row['title'] || row['name'] || [row['fname'], row['lname']].filter(Boolean).join(' '); return name || 'Details'; }
  trackById(index: number, row: AnyRow): any { return row?.id ?? index; }
  onSort(event: any): void { /* capture if needed */ }

  // Collect highlight terms for list grid and detail view
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
          const pushList = (val: any) => {
            if (Array.isArray(val)) {
              val.forEach(pushVal);
              return;
            }
            if (typeof val === 'string') {
              const trimmed = val.trim();
              if (!trimmed) return;
              const stripWrapper = (s: string): string => {
                if (
                  (s.startsWith('(') && s.endsWith(')')) ||
                  (s.startsWith('[') && s.endsWith(']'))
                ) {
                  return s.substring(1, s.length - 1);
                }
                return s;
              };
              const splitRespectingQuotes = (s: string): string[] => {
                const result: string[] = [];
                let current = '';
                let quoteChar: '"' | "'" | null = null;
                for (let i = 0; i < s.length; i++) {
                  const ch = s[i];
                  if ((ch === '"' || ch === "'") && s[i - 1] !== '\\') {
                    if (quoteChar === ch) {
                      quoteChar = null;
                    } else if (quoteChar === null) {
                      quoteChar = ch as '"' | "'";
                    }
                    current += ch;
                    continue;
                  }
                  if (ch === ',' && quoteChar === null) {
                    const piece = current.trim();
                    if (piece) result.push(piece);
                    current = '';
                    continue;
                  }
                  current += ch;
                }
                const finalPiece = current.trim();
                if (finalPiece) result.push(finalPiece);
                return result;
              };
              const tryJson = (s: string): string[] | null => {
                try {
                  const parsed = JSON.parse(s);
                  return Array.isArray(parsed) ? parsed.map((item) => String(item)) : null;
                } catch {
                  return null;
                }
              };
              const normalized = stripWrapper(trimmed);
              const jsonValues = tryJson(trimmed);
              if (jsonValues) {
                jsonValues.forEach(pushVal);
                return;
              }
              if (normalized.includes(',')) {
                splitRespectingQuotes(normalized)
                  .map((part) => part.replace(/^["'](.*)["']$/, '$1'))
                  .forEach(pushVal);
                return;
              }
              pushVal(trimmed);
              return;
            }
            pushVal(val);
          };
          // document contains "x" or generic value searches should highlight value
          if (operator && (operator.includes('contains') || operator.includes('like') || operator === '=' || operator === '==' || operator === 'in' || operator === '!in')) {
            pushList(value);
          } else if (field === 'document') {
            pushList(value);
          }
        } else {
          const asSet = node as LocalRuleSet;
          if (asSet && Array.isArray(asSet.rules)) asSet.rules.forEach(visit);
        }
      };
      visit(rs);
    } catch {}
    // Deduplicate and limit to reasonable length
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
      // Inject simple CSS for highlight
      const style = doc.createElement('style');
      style.textContent = '.__bql-hl{background:yellow; color:#111;}';
      doc.head?.appendChild(style);
      // Build matchers from terms; support regex literals (/.../flags) with case sensitivity per 'i' flag
      const esc = (s: string) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
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
      // Tree-walk and collect text nodes to process
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
        // Collect match ranges across all regexes
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
        // Merge overlapping
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

}
