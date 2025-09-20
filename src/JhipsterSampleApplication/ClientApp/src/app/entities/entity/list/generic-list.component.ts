/* eslint-disable */
import { Component, OnInit, ViewChild, TemplateRef, AfterViewInit, inject, Input } from '@angular/core';
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

import SharedModule from 'app/shared/shared.module';
import { ViewService } from '../../view/service/view.service';
import { EntityGenericService } from '../service/entity-generic.service';
import { DataLoader, FetchFunction } from 'app/shared/data-loader';
import { QueryInputComponent } from 'popup-ngx-query-builder';
import { QueryLanguageSpec } from 'ngx-query-builder';
import { SuperTable, ColumnConfig, GroupData, GroupDescriptor } from 'app/shared/SuperTable/super-table.component';

type AnyRow = { id?: string; [k: string]: any };

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
export class GenericListComponent implements OnInit, AfterViewInit {
  @Input() entity!: string;
  @Input() pageTitle: string | null = null;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private sanitizer = inject(DomSanitizer);
  private viewService = inject(ViewService);
  private entityService = inject(EntityGenericService);
  private messageService = inject(MessageService);
  private confirmationService = inject(ConfirmationService);

  @ViewChild('superTable') superTable!: SuperTable;
  @ViewChild('contextMenu') contextMenu: any;
  @ViewChild('chipMenu') chipMenu!: Menu;
  @ViewChild('expandedRow', { static: true }) expandedRowTemplate: TemplateRef<any> | undefined;
  @ViewChild(QueryInputComponent) queryInput!: QueryInputComponent;

  currentQuery = '';
  spec: QueryLanguageSpec | undefined;
  dataLoader!: DataLoader<AnyRow>;
  columns: ColumnConfig[] = [];
  initialWidths: (string | undefined)[] = [];
  specSignature: string | undefined;
  globalFilterFields: string[] = [];

  viewName: string | null = null;
  views: { label: string; value: string }[] = [];
  groups: GroupDescriptor[] = [];
  viewMode: 'grid' | 'group' = 'grid';

  menuItems: MenuItem[] = [];
  contextSelectedRow: AnyRow | null = null;
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
  categoryState: Record<string, 'checked' | 'indeterminate' | 'unchecked'> = {};
  newCategoryText = '';
  newCategoryChecked = false;
  rowsToCategorizeCount = 0;

  expandedRowKeys: { [key: string]: boolean } = {};
  iframeSafeSrcById: Record<string, any> = {};

  bDisplayDetail = false;
  detailDialogTitle = '';
  dialogSafeSrc: any = null;
  detailDialogId: string | null = null;

  itemsPerPage = 50;
  predicate = 'id';
  ascending = true;

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

  ngAfterViewInit(): void { this.onQueryChange(this.currentQuery); }

  private capitalize(s: string): string { return !s ? s : s.charAt(0).toUpperCase() + s.slice(1); }

  private loadViews(): void {
    this.viewService.queryByEntity(this.entity).subscribe(res => {
      const body = res.body ?? [];
      this.views = body.map(v => ({ label: v.name!, value: v.id! }));
    });
  }

  private loadColumnsFromSpec(): void {
    this.entityService.getEntitySpec(this.entity).subscribe({
      next: (spec: any) => {
        if (!this.pageTitle && spec?.title) this.pageTitle = String(spec.title);
        const columns = this.buildColumnsFromSpec(spec);
        this.columns = columns;
        this.initialWidths = this.columns.map(c => c.width || undefined);
        this.specSignature = this.columns.map(c => `${c.field}:${c.width || ''}`).join('|');
        this.globalFilterFields = columns
          .filter(c => (c.type === 'string' || !c.type) && !!c.field && c.field !== 'lineNumber' && c.field !== 'checkbox')
          .map(c => c.field);
      },
      error: () => {
        // Fallback: try to build from query-builder spec if entity spec fetch fails
        this.entityService.getQueryBuilderSpec(this.entity).subscribe({
          next: (qb: any) => {
            const specShim = { queryBuilder: qb };
            const columns = this.buildColumnsFromSpec(specShim);
            this.columns = columns;
            this.initialWidths = this.columns.map(c => c.width || undefined);
            this.specSignature = this.columns.map(c => `${c.field}:${c.width || ''}`).join('|');
            this.globalFilterFields = columns
              .filter(c => (c.type === 'string' || !c.type) && !!c.field && c.field !== 'lineNumber' && c.field !== 'checkbox')
              .map(c => c.field);
          },
          error: () => {
            // Final fallback: safe minimal columns
            this.columns = [
              { field: 'lineNumber', header: '#', type: 'lineNumber', width: '4rem' },
              { field: 'checkbox', header: '', type: 'checkbox', width: '2rem' },
              { field: 'id', header: 'Id', type: 'string' },
              { field: 'expander', header: '', type: 'expander', width: '25px', style: 'font-weight: 700;' },
            ];
            this.initialWidths = this.columns.map(c => c.width || undefined);
            this.specSignature = this.columns.map(c => `${c.field}:${c.width || ''}`).join('|');
            this.globalFilterFields = ['id'];
          },
        });
      },
    });
  }

  private initForEntity(e: string | null): void {
    if (!e) return;
    this.entity = e;
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

    this.entityService.getQueryBuilderSpec(this.entity).subscribe({ next: spec => (this.spec = spec), error: () => (this.spec = undefined) });
    this.loadColumnsFromSpec();
    this.loadViews();

    this.viewName = null;
    this.currentQuery = '';
    this.loadPage();
  }

  private buildColumnsFromSpec(spec: any): ColumnConfig[] {
    const cols: ColumnConfig[] = [
      { field: 'lineNumber', header: '#', type: 'lineNumber', width: '4rem' },
      { field: 'checkbox', header: '', type: 'checkbox', width: '2rem' },
    ];
    const qbFieldsAll: Record<string, any> = spec?.queryBuilder?.fields || {};
    const EXCLUDE = new Set<string>(['document', 'category', 'categories']);
    const df: any = spec?.detailFields;
    if (Array.isArray(df)) {
      for (const f of df) {
        if (f !== undefined && f !== null) EXCLUDE.add(String(f).toLowerCase());
      }
    }

    const listFields: Array<string | { field: string; header?: string; type?: string; dateFormat?: string }> = spec?.listFields || [];
    if (Array.isArray(listFields) && listFields.length > 0) {
      for (const lf of listFields) {
        if (typeof lf === 'string') {
          if (EXCLUDE.has(lf.toLowerCase())) continue;
          const meta = qbFieldsAll[lf] || {};
          const header = (meta.column || meta.name || this.prettyHeader(lf));
          const width = meta.width ? String(meta.width) : undefined;
          cols.push({ field: lf, header, type: 'string', width });
        } else if (lf && typeof lf === 'object') {
          if (EXCLUDE.has(String(lf.field || '').toLowerCase())) continue;
          const meta = qbFieldsAll[lf.field] || {};
          const header = lf.header || meta.column || meta.name || this.prettyHeader(lf.field);
          const col: ColumnConfig = { field: lf.field, header } as any;
          const t = (lf.type || 'string').toLowerCase();
          if (t === 'date') { col.type = 'date'; col.filterType = 'date'; col.dateFormat = lf.dateFormat || 'MM/dd/yyyy'; }
          else if (t === 'number' || t === 'numeric') { col.type = 'string'; col.filterType = 'numeric'; }
          else if (t === 'boolean') { col.type = 'boolean'; col.filterType = 'boolean'; }
          else { col.type = 'string'; col.filterType = 'text'; }
          if (!col.width && meta.width) col.width = String(meta.width);
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
        if (t === 'date') col = { field: k, header, type: 'date', filterType: 'date', dateFormat: 'MM/dd/yyyy' };
        else if (t === 'boolean') col = { field: k, header, type: 'boolean', filterType: 'boolean' };
        else if (t === 'number') col = { field: k, header, type: 'string', filterType: 'numeric' };
        else if (t === 'category' && Array.isArray(f.options)) {
          col = { field: k, header, type: 'list', listOptions: (f.options || []).map((o: any) => ({ label: o.name || String(o.value), value: String(o.value) })) };
        } else {
          col = { field: k, header, type: 'string', filterType: 'text' };
        }
        if (f.width) col.width = String(f.width);
        cols.push(col);
      }
    }

    cols.push({ field: 'expander', header: '', type: 'expander', width: '25px', style: 'font-weight: 700;' });
    return cols;
  }

  private prettyHeader(k: string): string { return (k || '').replace(/_/g, ' ').replace(/\b\w/g, (m) => m.toUpperCase()); }

  onQueryChange(query: string, restoreState = false): void { this.currentQuery = query || ''; if (this.viewName) { this.loadRootGroups(restoreState); } else { this.loadPage(); } }

  loadRootGroups(restoreState: boolean = false): void {
    if (!this.viewName) { this.groups = []; this.loadPage(); this.viewMode = 'grid'; setTimeout(() => this.superTable.applyCapturedHeaderState(), 300); return; }
    const viewParams: any = { from: 0, pageSize: 1000, view: this.viewName! };
    const hasQuery = this.currentQuery.trim().length > 0;
    if (hasQuery) {
      this.entityService.searchWithBql<any>(this.entity, this.currentQuery.trim(), viewParams).subscribe((res: any) => {
        const hits = res.body?.hits ?? [];
        if (hits.length > 0 && (hits[0] as any).categoryName !== undefined) {
          this.groups = hits.map((h: any) => ({ name: h.categoryName, count: h.count, categories: null }));
          this.viewMode = 'group';
          setTimeout(() => (restoreState ? this.superTable.restoreState((this.superTable as any).captureState()) : this.superTable.applyCapturedHeaderState()), 300);
        } else {
          this.groups = [];
          const filter: any = { view: this.viewName! };
          filter.bqlQuery = this.currentQuery.trim();
          this.dataLoader.load(this.itemsPerPage, this.predicate, this.ascending, filter);
          this.viewMode = 'grid';
          setTimeout(() => this.superTable.applyCapturedHeaderState(), 300);
        }
      });
    } else {
      this.entityService.searchView(this.entity, { ...viewParams, query: '*' }).subscribe((res: any) => {
        const hits = res.body?.hits ?? [];
        if (hits.length > 0 && (hits[0] as any).categoryName !== undefined) {
          this.groups = hits.map((h: any) => ({ name: h.categoryName, count: h.count, categories: null }));
          this.viewMode = 'group';
          setTimeout(() => (restoreState ? this.superTable.restoreState((this.superTable as any).captureState()) : this.superTable.applyCapturedHeaderState()), 300);
        } else {
          this.groups = [];
          const filter: any = { view: this.viewName!, query: '*' };
          this.dataLoader.load(this.itemsPerPage, this.predicate, this.ascending, filter);
          this.viewMode = 'grid';
          setTimeout(() => this.superTable.applyCapturedHeaderState(), 300);
        }
      });
    }
  }

  onViewChange(view: string | null): void {
    this.viewName = view;
    if (this.viewName) { try { this.superTable?.filterGlobal(''); } catch {} this.loadRootGroups(); }
    else { this.groups = []; this.viewMode = 'grid'; this.loadPage(); setTimeout(() => this.superTable.applyCapturedHeaderState(), 500); }
  }

  loadPage(): void {
    const filter: any = {};
    if (this.currentQuery && this.currentQuery.trim().length > 0) filter.bqlQuery = this.currentQuery.trim(); else filter.luceneQuery = '*';
    if (this.viewName) filter.view = this.viewName;
    this.dataLoader.load(this.itemsPerPage, this.predicate, this.ascending, filter);
    setTimeout(() => this.superTable.applyCapturedHeaderState(), 300);
  }

  refreshData(): void { try { this.superTable.captureHeaderState(); this.onQueryChange(this.currentQuery, true); } catch {} }

  onCheckboxChange(): void { this.checkboxSelectedRows = this.selection || []; this.chipSelectedRows = this.checkboxSelectedRows.slice(0, 2); }
  getChipLabel(row: AnyRow): string { const titleFields = ['title','name','lname','fname']; let text = ''; for (const f of titleFields) { if (row[f]) { text = text ? `${text} ${row[f]}` : `${row[f]}`; } } return text || (row.id || ''); }
  onChipMouseEnter(event: MouseEvent, row: AnyRow): void { this.chipMenuIsCount = false; this.chipMenuRow = row; this.setMenu(row); this.chipMenu?.show(event); }
  onCountChipMouseEnter(event: MouseEvent): void { this.chipMenuIsCount = true; this.chipMenuRow = null; this.setMenu(null); this.chipMenu?.show(event); }
  onChipMouseLeave(): void { this.chipMenu?.hide(); }
  onRemoveChip(row: AnyRow): void { const id = row?.id; if (!id) return; this.selection = (this.selection || []).filter(r => (r?.id ?? r) !== id); this.onCheckboxChange(); }
  onRemoveCountChip(): void { this.selection = []; this.onCheckboxChange(); }
  onContextMenuSelect(dataOrEvent: any): void { const row: AnyRow | undefined = dataOrEvent && dataOrEvent.data ? dataOrEvent.data : dataOrEvent; if (!row) return; this.contextSelectedRow = row; this.setMenu(row); }
  onMenuShow(): void { this.setMenu(this.contextSelectedRow); }

  private setMenu(row: AnyRow | null): void {
    const items: MenuItem[] = [ { label: 'Categorize', icon: 'pi pi-tags', command: () => this.openCategorizeDialog() } ];
    if (row) { items.push({ label: 'View', icon: 'pi pi-search', command: () => this.viewIframeFromContext() }); items.push({ label: 'Edit', icon: 'pi pi-pencil', command: () => this.editFromContext() }); items.push({ label: 'Delete', icon: 'pi pi-trash', command: () => this.deleteFromContext() }); }
    this.menuItems = items;
    const chipModel: MenuItem[] = [{ label: 'Categorize', icon: 'pi pi-tags', command: () => this.openCategorizeDialog() }];
    if (row) { chipModel.push({ label: 'View', icon: 'pi pi-search', command: () => this.viewIframeFromContext() }); chipModel.push({ label: 'Edit', icon: 'pi pi-pencil', command: () => this.editFromContext() }); chipModel.push({ label: 'Delete', icon: 'pi pi-trash', command: () => this.deleteFromChipMenu() }); }
    this.chipMenuModel = chipModel;
  }

  openCategorizeDialog(): void {
    const rows = this.chipMenuIsCount ? (this.selection || []) : (this.chipMenuRow ? [this.chipMenuRow] : (this.selection?.length ? this.selection : (this.contextSelectedRow ? [this.contextSelectedRow] : [])));
    this.chipMenuRow = null; this.chipMenuIsCount = false;
    if (!rows.length) { this.messageService.add({ severity: 'warn', summary: 'No rows selected' }); return; }
    this.rowsToCategorizeCount = rows.length;
    const set = new Set<string>(); rows.forEach(r => (r.categories || []).forEach((c: string) => set.add(c)));
    this.allCategories = Array.from(set.values()).sort();
    this.categoryState = Object.fromEntries(this.allCategories.map(c => [c, 'checked'] as const));
    this.filteredCategories = [...this.allCategories];
    this.newCategoryText = ''; this.newCategoryChecked = false; this.showCategorizeDialog = true;
  }
  hasCategorizeChanges(): boolean { return true; }
  cancelCategorize(): void { this.showCategorizeDialog = false; }
  filterCategoriesList(): void { const q = (this.newCategoryText||'').trim().toLowerCase(); this.filteredCategories = !q ? [...this.allCategories] : this.allCategories.filter(c => c.toLowerCase().includes(q)); }
  toggleCategory(cat: string): void { const st = this.categoryState[cat] || 'unchecked'; this.categoryState[cat] = st === 'unchecked' ? 'checked' : 'unchecked'; }
  applyCategorize(): void { const rows = this.selection && this.selection.length > 0 ? this.selection : (this.contextSelectedRow ? [this.contextSelectedRow] : []); if (!rows.length) { this.showCategorizeDialog = false; return; } const adds: string[] = Object.keys(this.categoryState).filter(k => this.categoryState[k] === 'checked'); let newCat = (this.newCategoryText || '').trim(); if (newCat && this.newCategoryChecked && !adds.some(a => a.toLowerCase() === newCat.toLowerCase())) adds.push(newCat); const rowIds = rows.map(r => r.id).filter(Boolean) as string[]; const payload = { rows: rowIds, add: adds, remove: [] }; this.entityService.categorizeMultiple(this.entity, payload).subscribe({ next: () => { this.showCategorizeDialog = false; this.refreshData(); }, error: () => { this.showCategorizeDialog = false; this.refreshData(); } }); }

  viewIframeFromContext(): void { const row = (this.contextSelectedRow || (this.selection && this.selection[0])) as AnyRow | undefined; const id = row?.id; if (!id) return; this.detailDialogId = id; this.detailDialogTitle = this.buildTitle(row); this.bDisplayDetail = true; }
  onDetailDialogShow(): void { const id = this.detailDialogId; this.dialogSafeSrc = null; setTimeout(() => { const url = `/api/entity/${encodeURIComponent(this.entity)}/html/${encodeURIComponent(String(id))}`; this.dialogSafeSrc = this.sanitizer.bypassSecurityTrustResourceUrl(url); }, 50); }
  onDetailDialogHide(): void { this.dialogSafeSrc = null; }
  editFromContext(): void { const row = this.contextSelectedRow || (this.selection && this.selection[0]); const id = row?.id; if (!id) return; this.router.navigate([`/${this.entity}`, id, 'edit']); }
  deleteFromContext(): void { const row = this.contextSelectedRow || (this.selection && this.selection[0]); const id = row?.id; if (!id) return; this.entityService.delete(this.entity, id).subscribe({ next: () => this.refreshData(), error: () => this.refreshData() }); }
  deleteFromChipMenu(): void { const rows: AnyRow[] = this.chipMenuIsCount ? (this.selection || []) : (this.chipMenuRow ? [this.chipMenuRow] : []); this.chipMenuRow = null; this.chipMenuIsCount = false; const ids = rows.map(r => r.id).filter(Boolean) as string[]; if (!ids.length) return; const confirmAndRun = () => { const next = (remaining: string[]) => { if (!remaining.length) { this.refreshData(); return; } const id = remaining.shift()!; this.entityService.delete(this.entity, id).subscribe({ next: () => next(remaining), error: () => next(remaining) }); }; next([...ids]); }; if (ids.length > 1) { this.confirmationService.confirm({ header: 'Confirm Delete', icon: 'pi pi-exclamation-triangle', message: `Delete ${ids.length} item(s)?`, acceptLabel: 'Yes', rejectLabel: 'No', accept: () => confirmAndRun() }); } else { confirmAndRun(); } }

  onRowExpand(event: { originalEvent: Event; data: AnyRow }): void { const row = event.data as AnyRow; const key = row?.id || JSON.stringify(row); this.expandedRowKeys[key] = true; setTimeout(() => { const url = `/api/entity/${encodeURIComponent(this.entity)}/html/${encodeURIComponent(String(row.id))}`; this.iframeSafeSrcById[row.id!] = this.sanitizer.bypassSecurityTrustResourceUrl(url); }, 50); }
  onRowCollapse(event: any): void { const row = event.data as AnyRow; const key = row?.id || JSON.stringify(row); delete this.expandedRowKeys[key]; if (row?.id) delete this.iframeSafeSrcById[row.id]; }

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
          loader.load(this.itemsPerPage, this.predicate, this.ascending, filter);
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
          loader.load(this.itemsPerPage, this.predicate, this.ascending, filter);
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
  onExpandedIframeLoad(id: string, ev: Event): void { /* no-op for generic */ }
}
