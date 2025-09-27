/* eslint-disable */

import { Component, OnInit, AfterViewInit, OnDestroy, OnChanges, SimpleChanges, Input, Output, EventEmitter, ViewChild, ViewChildren, QueryList, TemplateRef, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { CheckboxModule } from 'primeng/checkbox';
import { RippleModule } from 'primeng/ripple';
import { Table } from 'primeng/table';
import { MultiSelectModule } from 'primeng/multiselect';
import { FormsModule } from '@angular/forms';
import { DataLoader } from '../data-loader';
import { environment } from 'environments/environment';

export interface ColumnConfig {
  field: string;
  header: string;
  filterType?: 'text' | 'date' | 'numeric' | 'boolean';
  width?: string;
  minWidth?: string;
  style?: string;
  // Optional: for computed display columns, provide fallback fields in priority order
  computeFields?: string[];
  type?:
    | 'checkbox'
    | 'expander'
    | 'string'
    | 'date'
    | 'boolean'
    | 'list'
    | 'lineNumber';
  dateFormat?: string;
  listOptions?: { label: string; value: string }[];
}

export interface GroupDescriptor {
  name: string;
  count: number;
  categories?: string[] | null;
}

export interface GroupData {
  mode: 'grid' | 'group';
  loader?: DataLoader<any>;
  groups?: GroupDescriptor[];
}

@Component({
  selector: 'super-table',
  standalone: true,
  templateUrl: './super-table.component.html',
  styleUrl: './super-table.component.scss',
  imports: [
    CommonModule,
    TableModule,
    ButtonModule,
    TooltipModule,
    CheckboxModule,
    RippleModule,
    MultiSelectModule,
    FormsModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SuperTable implements OnInit, AfterViewInit, OnDestroy, OnChanges {
  // Increment this to invalidate/ignore all previously persisted widths when
  // the algorithm or persisted object shape changes.
  private static readonly WIDTHS_STORAGE_VERSION = 2;
  private cdr = inject(ChangeDetectorRef);

  @Input() dataLoader: DataLoader<any> | undefined;
  @Input() columns: ColumnConfig[] = [];
  @Input() widthKey: string | undefined;
  @Input() initialWidths: (string | undefined)[] | undefined;
  @Input() specSignature: string | undefined;
  @Input() groups: GroupDescriptor[] = [];
  @Input() mode: 'grid' | 'group' = 'grid';
  @Input() groupQuery: ((group: GroupDescriptor) => GroupData) | undefined;
  @Input() loading = false;
  @Input() resizableColumns = false;
  @Input() reorderableColumns = false;
  @Input() scrollable = false;
  @Input() scrollHeight: string | undefined;
  @Input() paginator = false;
  @Input() showHeader = true;
  @Input() dataKey: string | undefined;
  @Input() globalFilterFields: string[] = [];
  @Input() contextMenuSelection: any;
  @Input() contextMenu: any;
  @Input() showRowNumbers = false;
  @Input() selectionMode: 'single' | 'multiple' | null | undefined;
  @Input() selection: any;
  @Input() expandedRowKeys: { [key: string]: boolean } = {};
  @Input() loadingMessage: string | undefined;
  @Input() superTableParent: SuperTable | null = null;
  @Input() parentKey: string | undefined;
  @Input() expandedRowTemplate: TemplateRef<any> | undefined;
  @Input() highlightPattern: string | null | undefined;

  @ViewChild('pTable') pTable!: Table;
  @ViewChildren('detailTable') detailTables!: QueryList<SuperTable>;

  groupLoaders: { [key: string]: GroupData | undefined } = {};

  private lastSortEvent: any;
  private lastFilterEvent: any;
  private lastColumnWidths: string[] | undefined;
  private resizingIndex: number | null = null;
  private startX = 0;
  private startLeftWidth = 0;
  private startRightWidth = 0;
  private minWidthsCache: number[] | null = null;
  private startWidthsPx: number[] | null = null;

  private scrollContainer?: HTMLElement;
  private lastScrollTop = 0;
  private desiredScrollTop: number | null = null;
  private scrollRestoreHandle: any;
  private scrollListener = () => {
    if (this.scrollContainer) {
      this.lastScrollTop = this.scrollContainer.scrollTop;
      if (this.desiredScrollTop !== null) {
        // User interacted with the scroll while a restoration was pending.
        // Cancel any further attempts to reposition.
        this.desiredScrollTop = null;
        if (this.scrollRestoreHandle) {
          clearTimeout(this.scrollRestoreHandle);
          this.scrollRestoreHandle = null;
        }
      }
    }
  };
  private capturedWidths = false;
  private enforceHandle: any;
  private windowResizeHandler?: () => void;
  tableStyle: { [k: string]: any } = {};
  private autoExpandedApplied = false;
  private justResized = false;
  private applyingWidths = false; // guard to prevent ngOnChanges loop
  private purgeAllPersistedWidths(): void {
    try {
      if (typeof localStorage === 'undefined') return;
      const keys: string[] = [];
      for (let i = 0; i < localStorage.length; i++) {
        const k = localStorage.key(i);
        if (k && k.startsWith('supertable.widths:')) keys.push(k);
      }
      keys.forEach(k => localStorage.removeItem(k));
    } catch {}
  }

  // Resolve the real horizontal container width for this table.
  // Prefer a surrounding Bootstrap .table-responsive scroller when present; otherwise use PrimeNG wrapper.
  private getContainerWidth(): number {
    try {
      const host = this.pTable?.el?.nativeElement as HTMLElement | undefined;
      if (!host) return 0;
      let cand: HTMLElement | null = host;
      while (cand && cand.parentElement) {
        if (cand.classList && cand.classList.contains('table-responsive')) break;
        cand = cand.parentElement as HTMLElement;
      }
      const containerEl: HTMLElement = (cand && cand.classList.contains('table-responsive'))
        ? cand
        : ((host.querySelector('.p-datatable-wrapper') as HTMLElement) || host);
      // Take the smaller of container clientWidth and viewport clientWidth to avoid page-level overflow,
      // then subtract a conservative fudge (24px) to beat borders/rounding across browsers.
      const viewport = typeof document !== 'undefined' ? (document.documentElement?.clientWidth || 0) : 0;
      const base = Math.min(containerEl.clientWidth || 0, viewport || (containerEl.clientWidth || 0));
      return Math.max(0, base - 24);
    } catch { return 0; }
  }

  // Holds the current global filter text for group mode
  private groupFilterValue: string = '';

  // Returns groups filtered by groupFilterValue (group mode only)
  get displayGroups(): GroupDescriptor[] {
    if (!this.groups) {
      return [];
    }
    const query = (this.groupFilterValue || '').trim().toLowerCase();
    if (!query) {
      return this.groups;
    }
    return this.groups.filter(g => (g.name || '').toLowerCase().includes(query));
  }

  get sortEvent(): any {
    return this.lastSortEvent;
  }

  get filterEvent(): any {
    return this.lastFilterEvent;
  }

  get visibleColumns(): ColumnConfig[] {
    return this.showRowNumbers
      ? this.columns
      : this.columns.filter((c) => c.type !== 'lineNumber');
  }

  // headerTemplate removed to fix compilation

  @Output() rowExpand = new EventEmitter<any>();
  @Output() rowCollapse = new EventEmitter<any>();
  @Output() selectionChange = new EventEmitter<any>();
  @Output() contextMenuSelectionChange = new EventEmitter<any>();
  @Output() onContextMenuSelect = new EventEmitter<any>();
  @Output() onColResize = new EventEmitter<any>();
  @Output() onSort = new EventEmitter<any>();
  @Output() onFilter = new EventEmitter<any>();

  trackByFn(index: number, item: any): any {
    return item?.id || index;
  }

  ngOnInit(): void {
    // Dev helper: allow clean test runs by adding ?stReset=1 or ?cleanWidths=1
    if (environment.DEBUG_INFO_ENABLED) {
      try {
        const qs = typeof location !== 'undefined' ? location.search : '';
        if (/([?&])(stReset|cleanWidths)(=1)?(&|$)/.test(qs)) {
          this.purgeAllPersistedWidths();
        }
      } catch {}
    }
    // Recompute layout on browser resize using default heuristic
    if (typeof window !== 'undefined') {
      this.windowResizeHandler = () => {
        try {
          // Do not purge persisted widths globally; instead, widths are scoped
          // by spec signature (which includes viewport width from parent).
          // Reset column explicit widths back to their initial spec so columns
          // become flexible again before recalculation. Otherwise previously
          // applied pixel widths will be treated as explicit and block growth
          // when the container gets larger (e.g., restore -> maximize).
          if (this.initialWidths && this.initialWidths.length === this.visibleColumns.length) {
            this.visibleColumns.forEach((c, i) => (c.width = this.initialWidths![i]));
            this.columns = [...this.columns];
          } else {
            // No explicit initial widths captured; clear widths on flexible columns
            this.visibleColumns.forEach((c) => {
              const t = c.type;
              const isUtility = t === 'lineNumber' || t === 'checkbox' || t === 'expander';
              if (!isUtility) c.width = undefined;
            });
            this.columns = [...this.columns];
          }
          // Clear any cached widths captured from prior renders
          this.lastColumnWidths = undefined;
          this.minWidthsCache = null;
          const defaults = this.computeDefaultWidths();
          const fitted = this.fitWidthsToContainer(defaults);
          this.applyWidthsToDom(fitted);
          this.justResized = true;
          setTimeout(() => (this.justResized = false), 1500);
        } catch {}
      };
      window.addEventListener('resize', this.windowResizeHandler, { passive: true });
    }
  }

  // Highlight helper: returns HTML with <mark> around pattern matches
  formatCell(value: any, field?: string): any {
    if (value === null || value === undefined) return '';
    const text = String(value);
    const pat = (this.highlightPattern || '').trim();
    if (!pat) return this.escapeHtml(text);
    try {
      let rx: RegExp;
      if (pat.startsWith('/') && /\/([a-z]*)$/.test(pat)) {
        const m = pat.match(/^\/(.*)\/([a-z]*)$/);
        if (m) {
          rx = new RegExp(m[1], m[2].includes('i') ? 'gi' : 'g');
        } else {
          return this.escapeHtml(text);
        }
      } else {
        // Build union of words, escape specials
        const parts = pat
          .split(/\s+/)
          .map(p => p.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'))
          .filter(p => p.length > 0);
        if (parts.length === 0) return this.escapeHtml(text);
        rx = new RegExp('(' + parts.join('|') + ')', 'gi');
      }
      const escaped = this.escapeHtml(text);
      // Replace on escaped string by mapping back literal matches; approximate by running on original and rebuilding
      const segments: any[] = [];
      let last = 0;
      let match: RegExpExecArray | null;
      rx.lastIndex = 0;
      while ((match = rx.exec(text)) !== null) {
        const start = match.index;
        const end = start + match[0].length;
        segments.push(this.escapeHtml(text.slice(last, start)));
        segments.push('<mark>' + this.escapeHtml(text.slice(start, end)) + '</mark>');
        last = end;
        if (match[0].length === 0) {
          rx.lastIndex++;
        }
      }
      segments.push(this.escapeHtml(text.slice(last)));
      return segments.join('');
    } catch {
      return this.escapeHtml(text);
    }
  }

  // Truthy check that treats empty strings (including whitespace-only) as empty
  nonEmpty(value: any): boolean {
    try {
      if (value === null || value === undefined) return false;
      const s = String(value);
      return s.trim().length > 0;
    } catch { return false; }
  }

  // Get display string for a column, supporting computed fields (first non-empty among computeFields)
  getCellString(row: any, col: ColumnConfig): string {
    try {
      if (col && Array.isArray((col as any).computeFields)) {
        for (const f of (col as any).computeFields as string[]) {
          const v = row?.[f];
          if (v !== undefined && v !== null && String(v).trim().length > 0) return String(v);
        }
        return '';
      }
      const v = row?.[(col as any).field];
      return v === undefined || v === null ? '' : String(v);
    } catch {
      return '';
    }
  }

  private escapeHtml(s: string): string {
    return s
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  getIndentStyle(group: GroupDescriptor): { [key: string]: string } {
    const level = group.categories ? group.categories.length : 0;
    return { 'padding-left': `${level * 1.5}rem` };
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['mode'] && !changes['mode'].firstChange) {
      if (changes['mode'].currentValue === 'group') {
        setTimeout(() => {
          this.captureColumnWidths();
          this.initScroll();
          this.enforceWidthsAfterLayout();
        });
      } else {
        this.initScroll();
        this.enforceWidthsAfterLayout();
      }
    }
    if (changes['groups'] && !changes['groups'].firstChange) {
      // Clear cached group data and expanded state when groups change
      this.groupLoaders = {};
      this.expandedRowKeys = {};
    }
    // Avoid re-entrancy: ignore change events we caused while applying widths
    if (this.applyingWidths) {
      return;
    }
    // Trigger enforcement only for external shape/signature changes, not for internal 'columns' clones
    if (changes['initialWidths'] || changes['widthKey'] || changes['specSignature']) {
      this.autoExpandedApplied = false;
      this.enforceWidthsAfterLayout();
    }
  }

  ngAfterViewInit(): void {
    setTimeout(() => {
      if (this.mode === 'group') {
        this.captureColumnWidths();
      }
      this.initScroll();
      this.enforceWidthsAfterLayout();
    });
    this.detailTables.changes.subscribe(() => {
      setTimeout(() => this.applyStoredStateToDetails(), 500);
    });
  }

  ngOnDestroy(): void {
    this.destroyScroll();
    if (typeof window !== 'undefined' && this.windowResizeHandler) {
      window.removeEventListener('resize', this.windowResizeHandler as any);
      this.windowResizeHandler = undefined;
    }
  }

  isRowExpanded(row: any): boolean {
    const key = row.id || row.key || JSON.stringify(row);
    return this.expandedRowKeys[key] === true;
  }

  isGroupExpanded(group: GroupDescriptor): boolean {
    return this.expandedRowKeys[group.name] === true;
  }

  onGroupToggle(group: GroupDescriptor): void {
    const groupName = group.name;
    const isExpanded = this.isGroupExpanded(group);
    if (isExpanded) {
      delete this.expandedRowKeys[groupName];
      this.rowCollapse.emit({ data: group });
    } else {
      this.expandedRowKeys[groupName] = true;
      if (this.groupQuery && !this.groupLoaders[groupName]) {
        this.groupLoaders[groupName] = this.groupQuery(group);
      }
      this.rowExpand.emit({ data: group });
      setTimeout(() => this.applyStoredStateToDetails(), 500);
    }
  }

  onRowExpand(event: { originalEvent: Event; data: any }) {
    console.log('Expanded:', event.data);
    this.expandedRowKeys[event.data.id] = true;
    this.rowExpand.emit(event);
    setTimeout(() => this.applyStoredStateToDetails(), 500);
  }

  onRowCollapse(event: any) {
    delete this.expandedRowKeys[event.data.id];
    this.rowCollapse.emit(event);
  }

  filterList(event: any, field: string): void {
    const selectedValues = event.value.map((item: any) => item.value);
    if (selectedValues.length === 0) {
      this.pTable.filter(null, field, 'in');
    } else {
      this.pTable.filter(selectedValues, field, 'in');
    }
    // Persist filter state so it can be restored after mode changes
    this.lastFilterEvent = { filters: (this.pTable as any).filters };
  }

  handleSort(event: any): void {
    this.lastSortEvent = event;
    this.onSort.emit(event);
  }

  handleFilter(event: any): void {
    this.lastFilterEvent = event;
    this.onFilter.emit(event);
  }

  /**
   * Capture the current header state (widths, sort, filters) from the
   * underlying PrimeNG table and store them in the internal properties. This
   * should be invoked before a mode change or data refresh so the latest
   * column settings can be restored afterwards.
   */
  captureHeaderState(): void {
    this.lastColumnWidths = this._getColumnWidths();
    if (this.pTable) {
      const sortField = (this.pTable as any).sortField;
      const sortOrder = (this.pTable as any).sortOrder;
      if (sortField !== undefined) {
        this.lastSortEvent = {
          field: sortField,
          sortField,
          order: sortOrder,
          sortOrder,
        };
      }
      const filters = (this.pTable as any).filters;
      if (filters) {
        this.lastFilterEvent = { filters };
      }
    }
  }

  /**
   * Apply previously captured header state to the current table instance.
   * This is used after switching modes or refreshing the list so the
   * user's column settings remain intact.
   */
  applyCapturedHeaderState(): void {
    if (this.lastColumnWidths) {
      this.visibleColumns.forEach((c, i) => {
        c.width = this.lastColumnWidths![i];
      });
      this.columns = [...this.columns];
    }
    if (this.lastSortEvent) {
      this.applySort(this.lastSortEvent);
    }
    if (this.lastFilterEvent) {
      this.applyFilter(this.lastFilterEvent);
    }
  }

  private applyStoredStateToDetails(): void {
    const currentWidths = this._getColumnWidths();
    if (currentWidths) {
      this.lastColumnWidths = currentWidths;
    }

    this.detailTables?.forEach((table) => {
      table.columns = [...this.columns];
      if (this.lastSortEvent) {
        table.applySort(this.lastSortEvent);
      }
      if (this.lastFilterEvent) {
        table.applyFilter(this.lastFilterEvent);
      }
    });
    this.attemptScrollRestore();
  }

  applySort(event: any): void {
    if (this.pTable && event) {
      (this.pTable as any).sortField = event.sortField || event.field;
      (this.pTable as any).sortOrder = event.sortOrder || event.order;
      if ((this.pTable as any).sortSingle) {
        (this.pTable as any).sortSingle();
      }
    }
  }

  applyFilter(event: any): void {
    if (this.pTable && event?.filters) {
      (this.pTable as any).filters = event.filters;
      if ((this.pTable as any)._filter) {
        (this.pTable as any)._filter();
      }
    }
  }

  filterGlobal(value: string): void {
    if (this.mode === 'group') {
      this.groupFilterValue = value || '';
      this.cdr.markForCheck();
      return;
    }
    if (this.pTable) {
      this.pTable.filterGlobal(value, 'contains');
    }
  }

  onHeaderSort(event: any): void {
    this.lastSortEvent = event;
    this.detailTables?.forEach((table) => table.applySort(event));
    requestAnimationFrame(() => this.attemptScrollRestore());
  }

  onHeaderFilter(event: any): void {
    // In group mode, all header/global filters apply only to the top-level groups (e.g., by name).
    // Do not propagate any of them to child tables and do not clear the parent's filteredValue.
    if (this.mode === 'group') {
      setTimeout(() => this.attemptScrollRestore());
      return;
    }

    // In grid mode, propagate column/global filters to any visible child tables.
    this.lastFilterEvent = event;
    this.detailTables?.forEach((table) => table.applyFilter(event));
    this.pTable.filteredValue = null;
    setTimeout(() => this.attemptScrollRestore());
  }

  private _getColumnWidths(): string[] | undefined {
    if (this.superTableParent) {
      return this.superTableParent._getColumnWidths();
    }

    if (this.pTable?.el) {
      const header: NodeListOf<HTMLTableCellElement> =
        this.pTable.el.nativeElement.querySelectorAll('th');
      if (header) {
        return Array.from(header).map(
          (th: HTMLTableCellElement) => th.offsetWidth + 'px',
        );
      }
    }
    return undefined;
  }

  private captureColumnWidths(): void {
    if (this.capturedWidths || this.mode !== 'group') {
      return;
    }
    if (this.lastColumnWidths) {
      // If we already have stored widths (e.g. from grid mode), reuse them
      this.visibleColumns.forEach((col, index) => {
        col.width = this.lastColumnWidths![index] || '';
      });
      this.columns = [...this.columns];
      this.capturedWidths = true;
      return;
    }

    const widths = this._getColumnWidths();
    if (widths) {
      this.visibleColumns.forEach((col, index) => {
        col.width = widths[index] || '';
      });
      this.columns = [...this.columns];
      this.lastColumnWidths = widths;
      this.capturedWidths = true;
    }
  }

  captureState(): any {
    this.lastScrollTop = this.scrollContainer?.scrollTop || 0;
    this.desiredScrollTop = this.lastScrollTop;
    const state: any = {
      expandedRowKeys: { ...this.expandedRowKeys },
      scrollTop: this.lastScrollTop,
      children: {},
    };
    this.detailTables?.forEach((table) => {
      if (table.parentKey) {
        state.children[table.parentKey] = table.captureState();
      }
    });
    return state;
  }

  restoreState(state: any): void {
    if (!state) {
      return;
    }
    const expanded = state.expandedRowKeys || {};
    const groupNames = new Set(this.groups?.map((g) => g.name));
    for (const key of Object.keys(expanded)) {
      if (groupNames.has(key)) {
        const group = this.groups.find((g) => g.name === key);
        if (group && !this.isGroupExpanded(group)) {
          this.onGroupToggle(group);
        }
      } else {
        this.expandedRowKeys[key] = true;
      }
    }
    this.cdr.detectChanges();
    this.desiredScrollTop = state.scrollTop || 0;
    setTimeout(() => {
      this.applyStoredStateToDetails();
      const children = state.children || {};
      this.detailTables?.forEach((table) => {
        const key = table.parentKey;
        if (key && children[key]) {
          table.restoreState(children[key]);
        }
      });
      this.attemptScrollRestore();
    }, 500);
  }

  onHeaderColResize(event: any): void {
    this.lastColumnWidths = this._getColumnWidths();
    if (this.lastColumnWidths) {
      this.visibleColumns.forEach((c, i) => {
        c.width = this.lastColumnWidths![i];
      });
      this.columns = [...this.columns];
      this.writePersistedWidths(this.lastColumnWidths);
    }
    this.detailTables?.forEach((table) => {
      if (this.lastColumnWidths) {
        table.visibleColumns.forEach((c, i) => {
          c.width = this.lastColumnWidths![i];
        });
        table.columns = [...table.columns];
        table.cdr.detectChanges();
      }
    });
    this.onColResize.emit(event);
    requestAnimationFrame(() => this.attemptScrollRestore());
  }

  // === Custom column resize (adjacent columns, fixed table width) ===
  onResizeStart(index: number, ev: MouseEvent): void {
    ev.preventDefault();
    ev.stopPropagation();
    let left = this.findResizableIndexFrom(index, 0);
    let right = this.findResizableIndexFrom(index + 1, +1);
    // If there is no right neighbor (last column), use the previous neighbor instead
    if (left != null && right == null) {
      const prev = this.findResizableIndexFrom(left - 1, -1);
      if (prev != null) { right = left; left = prev; }
    }
    if (left == null || right == null) return;
    const widths = this._getColumnWidths() || this.visibleColumns.map(c => c.width || '120px');
    this.resizingIndex = left;
    this.startX = ev.clientX;
    this.startLeftWidth = parseFloat(widths[left] || '0');
    this.startRightWidth = parseFloat(widths[right] || '0');
    // Snapshot starting widths so each move is computed relative to initial state
    this.startWidthsPx = widths.map(w => this.parsePx(String(w), 120));
    this.minWidthsCache = this.getMinWidths();
    const onMove = (e: MouseEvent) => this.onResizeMove(e, left, right);
    const onUp = (e: MouseEvent) => this.onResizeEnd(e, onMove, onUp);
    document.addEventListener('mousemove', onMove, true);
    document.addEventListener('mouseup', onUp, true);
    document.body.classList.add('st-resizing');
  }

  private onResizeMove(ev: MouseEvent, left: number, right: number): void {
    if (this.resizingIndex == null) return;
    const dx = ev.clientX - this.startX;
    const start = (this.startWidthsPx && this.startWidthsPx.length === this.visibleColumns.length)
      ? [...this.startWidthsPx]
      : (this._getColumnWidths() || this.visibleColumns.map(c => c.width || '120px')).map(w => this.parsePx(String(w), 120));
    const minW = this.minWidthsCache || this.getMinWidths();

    // Clamp desired left within [minLeft, startLeft + totalHeadroomRight]
    const totalHeadroomRight = start.slice(right).reduce((s, w, i) => s + Math.max(0, w - (minW[right + i] || 30)), 0);
    const minLeft = minW[left] || 30;
    const desiredLeftRaw = this.startLeftWidth + dx;
    const desiredLeft = Math.min(this.startLeftWidth + totalHeadroomRight, Math.max(minLeft, desiredLeftRaw));
    let delta = Math.round(desiredLeft - this.startLeftWidth);

    const newWidths = [...start];
    if (delta > 0) {
      let need = delta;
      let i = right;
      while (i < newWidths.length && need > 0) {
        const headroom = Math.max(0, newWidths[i] - (minW[i] || 30));
        const take = Math.min(headroom, need);
        newWidths[i] -= take;
        need -= take;
        i++;
      }
      const stolen = delta - need;
      newWidths[left] = this.startLeftWidth + stolen;
    } else if (delta < 0) {
      // Symmetric algorithm for moving handle to the left.
      // 1) Decrease the left column down to its minimum.
      // 2) If more movement is requested, reduce columns to the LEFT (left-1, left-2, ...)
      // 3) Increase the immediate right column by the total movement amount to keep total width constant.

      // Total requested shrink from the left side based on pointer movement
      const want = Math.round(-dx); // positive pixels desired to move left
      if (want <= 0) return;

      // Available pixels from the left side (left column + columns to its left)
      let available = Math.max(0, start[left] - (minLeft));
      for (let i = left - 1; i >= 0; i--) {
        available += Math.max(0, start[i] - (minW[i] || 30));
      }
      const grant = Math.min(want, available);

      // Apply shrink to the left column first
      let remaining = grant;
      const shrinkLeft = Math.min(remaining, Math.max(0, start[left] - (minLeft)));
      newWidths[left] = start[left] - shrinkLeft;
      remaining -= shrinkLeft;

      // Then steal from columns to the left, right-to-left
      if (remaining > 0) {
        for (let i = left - 1; i >= 0 && remaining > 0; i--) {
          const headroom = Math.max(0, start[i] - (minW[i] || 30));
          const take = Math.min(headroom, remaining);
          newWidths[i] = start[i] - take;
          remaining -= take;
        }
      }

      // Grow the immediate right column by the total granted movement
      if (right < newWidths.length) newWidths[right] = start[right] + grant;
    } else {
      // no change
    }

    // Apply only changed indices to avoid jitter
    const pairs: Array<[number, number]> = [];
    newWidths.forEach((w, i) => {
      if (w !== start[i]) pairs.push([i, w]);
    });
    if (pairs.length) this.applyWidthsByIndex(pairs);
    this.updateTableStyleWidth();
  }

  private onResizeEnd(ev: MouseEvent, onMove: any, onUp: any): void {
    document.removeEventListener('mousemove', onMove, true);
    document.removeEventListener('mouseup', onUp, true);
    document.body.classList.remove('st-resizing');
    this.lastColumnWidths = this._getColumnWidths();
    if (this.lastColumnWidths) this.writePersistedWidths(this.lastColumnWidths);
    this.resizingIndex = null;
    this.startWidthsPx = null;
  }

  // Double-click on a resize handle resets widths to algorithm-determined defaults
  onResizeReset(index: number, ev: MouseEvent): void {
    ev.preventDefault();
    ev.stopPropagation();
    this.resetColumnWidthsToDefaults();
  }

  private resetColumnWidthsToDefaults(): void {
    try {
      // Remove only the current profile from persisted storage
      if (this.widthKey) {
        const key = `supertable.widths:${this.widthKey}`;
        let obj: any = null;
        try { obj = JSON.parse(localStorage.getItem(key) || 'null'); } catch { obj = null; }
        if (obj && obj.v === SuperTable.WIDTHS_STORAGE_VERSION && obj.profiles) {
          const sig = this.specSignature || 'default';
          if (obj.profiles[sig]) {
            delete obj.profiles[sig];
            // Clean up if empty
            if (Object.keys(obj.profiles).length === 0) {
              localStorage.removeItem(key);
            } else {
              localStorage.setItem(key, JSON.stringify(obj));
            }
          }
        }
      }
    } catch {}

    // Restore initial/implicit widths as a starting point
    if (this.initialWidths && this.initialWidths.length === this.visibleColumns.length) {
      this.visibleColumns.forEach((c, i) => (c.width = this.initialWidths![i]));
    } else {
      this.visibleColumns.forEach((c) => {
        const t = c.type;
        const isUtility = t === 'lineNumber' || t === 'checkbox' || t === 'expander';
        if (!isUtility) c.width = undefined;
      });
    }
    this.columns = [...this.columns];
    this.lastColumnWidths = undefined;
    this.minWidthsCache = null;
    const defaults = this.computeDefaultWidths();
    const fitted = this.fitWidthsToContainer(defaults);
    this.applyWidthsToDom(fitted);
  }

  private findResizableIndexFrom(start: number, dir: number): number | null {
    const n = this.visibleColumns.length;
    for (let i = start; i >= 0 && i < n; i += (dir === 0 ? 1 : dir)) {
      const c = this.visibleColumns[i];
      if (!c) continue;
      const t = c.type;
      if (t !== 'lineNumber' && t !== 'checkbox' && t !== 'expander') return i;
      if (dir === 0) continue;
    }
    return null;
  }

  private applyWidthsByIndex(pairs: Array<[number, number]>): void {
    pairs.forEach(([i, px]) => {
      if (!this.visibleColumns[i]) return;
      this.visibleColumns[i].width = Math.max(30, Math.round(px)) + 'px';
    });
    this.columns = [...this.columns];
    this.cdr.detectChanges();
    setTimeout(() => {
      if (!this.pTable?.el) return;
      const ths: NodeListOf<HTMLTableCellElement> = this.pTable.el.nativeElement.querySelectorAll('thead th');
      pairs.forEach(([i, px]) => {
        const th = ths[i];
        if (th) th.style.width = Math.max(30, Math.round(px)) + 'px';
      });
    }, 0);
  }

  private initScroll(): void {
    const body =
      (this.pTable?.scroller?.getElementRef()?.nativeElement as HTMLElement) ||
      (this.pTable?.wrapperViewChild?.nativeElement as HTMLElement);
    if (body) {
      this.destroyScroll();
      this.scrollContainer = body;
      this.scrollContainer.addEventListener('scroll', this.scrollListener);
      this.lastScrollTop = this.scrollContainer.scrollTop;
      // Do not set desiredScrollTop on initialization. This prevents
      // unintended scroll restoration on the initial load. The value will be
      // explicitly set when the user requests a refresh and captureState is
      // invoked.
    }
  }

  private attemptScrollRestore(): void {
    let top: SuperTable = this;
    while (top.superTableParent) {
      top = top.superTableParent;
    }
    top.scheduleScrollRestore();
  }

  private scheduleScrollRestore(): void {
    if (!this.scrollContainer || this.desiredScrollTop === null) {
      return;
    }
    if (this.scrollRestoreHandle) {
      clearTimeout(this.scrollRestoreHandle);
    }
    this.scrollContainer.scrollTop = this.desiredScrollTop;
    if (this.scrollContainer.scrollTop !== this.desiredScrollTop) {
      this.scrollRestoreHandle = setTimeout(
        () => this.scheduleScrollRestore(),
        50,
      );
    } else {
      // Scroll position has been successfully restored. Clear the handle and
      // reset desiredScrollTop so no further repositioning occurs.
      this.scrollRestoreHandle = null;
      this.desiredScrollTop = null;
    }
  }

  private destroyScroll(): void {
    this.scrollContainer?.removeEventListener('scroll', this.scrollListener);
    this.scrollContainer = undefined;
    if (this.scrollRestoreHandle) {
      clearTimeout(this.scrollRestoreHandle);
      this.scrollRestoreHandle = null;
    }
    if (this.enforceHandle) {
      clearTimeout(this.enforceHandle);
      this.enforceHandle = null;
    }
  }

  private readPersistedWidths(): string[] | null {
    try {
      if (!this.widthKey) return null;
      const raw = localStorage.getItem(`supertable.widths:${this.widthKey}`);
      if (!raw) return null;
      const obj = JSON.parse(raw);
      // Strictly accept only the current versioned payload. Any other shape
      // or older version is ignored by design to avoid cross-run interference.
      if (!obj || typeof obj !== 'object' || Array.isArray(obj)) return null;
      if (obj.v !== SuperTable.WIDTHS_STORAGE_VERSION) return null;
      const profiles = obj.profiles;
      if (!profiles || typeof profiles !== 'object') return null;
      const sig = this.specSignature || '';
      const candidate = profiles[sig];
      if (Array.isArray(candidate)) return candidate as string[];
      return null;
    } catch {
      return null;
    }
  }

  private writePersistedWidths(widths: string[]): void {
    try {
      if (!this.widthKey) return;
      const key = `supertable.widths:${this.widthKey}`;
      let obj: any = null;
      try { obj = JSON.parse(localStorage.getItem(key) || 'null'); } catch { obj = null; }
      // Start fresh unless the stored object matches the current version/shape
      if (!obj || typeof obj !== 'object' || Array.isArray(obj) || obj.v !== SuperTable.WIDTHS_STORAGE_VERSION) {
        obj = { v: SuperTable.WIDTHS_STORAGE_VERSION, profiles: {} };
      }
      if (!obj.profiles || typeof obj.profiles !== 'object') obj.profiles = {};
      const sig = this.specSignature || 'default';
      obj.profiles[sig] = widths;
      localStorage.setItem(key, JSON.stringify(obj));
    } catch {}
  }

  private applyWidthsToDom(widths: (string | undefined)[]): void {
    this.applyingWidths = true;
    this.visibleColumns.forEach((c, i) => {
      const w = widths[i];
      if (w && typeof w === 'string' && w.trim()) c.width = w;
    });
    this.columns = [...this.columns];
    this.updateTableStyleWidth();
    setTimeout(() => {
      if (!this.pTable?.el) return;
      const ths: NodeListOf<HTMLTableCellElement> = this.pTable.el.nativeElement.querySelectorAll('thead th');
      widths.forEach((w, i) => {
        if (!w || !ths[i]) return;
        ths[i].style.width = w;
      });
      this.applyingWidths = false;
    }, 0);
  }

  private enforceWidthsAfterLayout(): void {
    const persisted = this.readPersistedWidths();
    // Prefer persisted widths only if they fit the available container width and minima.
    let desired: (string | undefined)[] | undefined = undefined;
    if (persisted && persisted.length === this.visibleColumns.length) {
      try {
        const containerWidth = this.getContainerWidth();
        const totalPersisted = persisted.reduce((sum, w) => sum + this.parsePx(String(w), 120), 0);
        const minW = this.getMinWidths();
        const anyBelowMin = persisted.some((w, i) => this.parsePx(String(w), 120) < (minW[i] || 30));
        if ((containerWidth && totalPersisted > containerWidth) || anyBelowMin) {
          desired = undefined;
        } else {
          desired = persisted;
        }
      } catch {
        desired = persisted;
      }
    }
    if (!desired) {
      desired = (this.initialWidths && this.initialWidths.length === this.visibleColumns.length ? this.initialWidths : undefined);
    }
    if (!desired) {
      desired = this.computeDefaultWidths();
    }
    if (!desired) return;

    desired = this.fitWidthsToContainer(desired);

    const applyNow = () => this.applyWidthsToDom(desired);
    // Apply immediately once
    requestAnimationFrame(applyNow);

    // If this is a nested table (rendered inside another SuperTable),
    // skip the enforcement loop to avoid multiple concurrent timers and
    // heavy layout churn when many nested tables are expanded.
    if (this.superTableParent) {
      return;
    }

    // Then enforce until stable for a short window, to outlast PrimeNG adjustments
    const start = Date.now();
    let lastMismatchAt = Date.now();

    const checkAndEnforce = () => {
      // Do not enforce while the user is actively resizing columns
      if (this.resizingIndex != null) return;
      const ths: NodeListOf<HTMLTableCellElement> | null = this.pTable?.el?.nativeElement?.querySelectorAll('thead th') || null;
      if (!ths || ths.length === 0) return; // nothing to enforce
      // Compare only for indices where a desired width exists
      let mismatch = false;
      desired.forEach((w, i) => {
        if (!w) return;
        const th = ths[i];
        if (!th) return;
        const want = parseFloat(String(w));
        const have = parseFloat(String(th.style.width || th.offsetWidth + ''));
        if (isFinite(want) && Math.abs(have - want) > 1) {
          mismatch = true;
        }
      });
      if (mismatch) {
        lastMismatchAt = Date.now();
        this.applyWidthsToDom(desired);
      }
      // Skip auto-expand; widths are already fit to container by heuristic
      const elapsed = Date.now() - start;
      const sinceLastMismatch = Date.now() - lastMismatchAt;
      // Stop if stable for 200ms or after 800ms total (tighter guard)
      if (sinceLastMismatch > 200 || elapsed > 800) {
        if (this.enforceHandle) {
          clearInterval(this.enforceHandle);
          this.enforceHandle = null;
        }
        // Final micro-adjust based on actual rendered header widths to avoid tiny overflow/underflow
        this.adjustToMeasuredContainer();
        // Persist final widths if a key is set and not immediately after a window resize
        if (!this.justResized) {
          const final = this._getColumnWidths();
          if (final && final.length === this.visibleColumns.length) this.writePersistedWidths(final);
        }
      }
    };

    if (this.enforceHandle) {
      clearInterval(this.enforceHandle);
      this.enforceHandle = null;
    }
    this.enforceHandle = setInterval(checkAndEnforce, 50);
  }

  private parsePx(value: string | undefined, fallback = 120): number {
    if (!value) return fallback;
    const s = String(value).trim();
    if (s.endsWith('px')) return parseFloat(s);
    if (s.endsWith('rem')) return parseFloat(s) * 16;
    const n = parseFloat(s);
    return isFinite(n) ? n : fallback;
  }

  private getMinWidths(): number[] {
    if (this.minWidthsCache && this.minWidthsCache.length === this.visibleColumns.length) return this.minWidthsCache;
    const arr = this.visibleColumns.map(c => {
      if (c.minWidth) return this.parsePx(c.minWidth, 30);
      const label = (c.header || '').trim();
      const approx = Math.max(40, Math.min(240, Math.round(label.length * 9 + 24)));
      if (c.type === 'boolean' || c.type === 'checkbox' || c.type === 'expander' || c.type === 'lineNumber') return Math.max(30, Math.min(approx, 80));
      if (c.type === 'date') return Math.max(80, approx);
      // Numeric columns: readable default minimum
      if (c.filterType === 'numeric') return 80;
      return approx;
    });
    this.minWidthsCache = arr;
    return arr;
  }

  private computeDefaultWidths(): (string | undefined)[] {
    try {
      const container = this.getContainerWidth();
      const minW = this.getMinWidths();
      // Only text-like columns without an explicit width are flexible
      const isFlexible = (c: ColumnConfig) => ((c.type === 'string' || c.type === 'list' || c.type === undefined) && c.filterType !== 'numeric' && !c.width);
      const base = this.visibleColumns.map((c, i) => {
        const explicit = this.parsePx(c.width, NaN);
        if (!isNaN(explicit)) return Math.max(minW[i], explicit);
        if (c.type === 'date') return Math.max(minW[i], 120);
        if (c.type === 'boolean') return Math.max(minW[i], 70);
        // Numeric default ~120px unless a higher min is specified
        if (c.filterType === 'numeric') return Math.max(minW[i], 120);
        if (c.type === 'checkbox' || c.type === 'expander' || c.type === 'lineNumber') return Math.max(minW[i], 30);
        return minW[i];
      });
      const flexIdx: number[] = [];
      base.forEach((_, i) => { if (isFlexible(this.visibleColumns[i])) flexIdx.push(i); });
      const baseSum = base.reduce((s, v) => s + v, 0);
      const room = Math.max(0, container - baseSum);
      const addEach = flexIdx.length > 0 ? Math.floor(room / Math.max(1, flexIdx.length)) : 0;
      flexIdx.forEach(i => base[i] += addEach);
      const remainder = container - base.reduce((s, v) => s + v, 0);
      if (remainder > 0 && flexIdx.length > 0) base[flexIdx[flexIdx.length - 1]] += remainder;
      return base.map(px => Math.round(px) + 'px');
    } catch {
      return this.visibleColumns.map(() => '120px');
    }
  }

  private fitWidthsToContainer(widths: (string | undefined)[]): (string | undefined)[] {
    try {
      const container = this.getContainerWidth();
      const minW = this.getMinWidths();
      let arr = widths.map(w => this.parsePx(String(w), 120));
      arr = arr.map((w, i) => Math.max(minW[i] || 30, w));
      const sum = arr.reduce((s, v) => s + v, 0);
      const isFlexible = (i: number) => {
        const col = this.visibleColumns[i];
        const t = col?.type;
        const ft = (col as any)?.filterType as any;
        const hasExplicit = !!col?.width;
        return (t === 'string' || t === 'list' || t === undefined) && ft !== 'numeric' && !hasExplicit;
      };
      const flexIdx = arr.map((_, i) => i).filter(isFlexible);
      if (container > 0 && flexIdx.length > 0) {
        let diff = Math.round(container - sum);
        if (diff !== 0) {
          const step = diff > 0 ? 1 : -1;
          let guard = Math.min(5000, Math.abs(diff) * 5);
          while (diff !== 0 && guard-- > 0) {
            let progressed = false;
            for (const i of flexIdx) {
              const next = arr[i] + step;
              if (step < 0 && next < (minW[i] || 30)) continue;
              arr[i] = next;
              diff -= step;
              progressed = true;
              if (diff === 0) break;
            }
            if (!progressed) break;
          }
        }
      }
      // Round and correct to avoid tiny overflow
      let rounded = arr.map(px => Math.max(30, Math.round(px)));
      let total = rounded.reduce((s, v) => s + v, 0);
      let delta = Math.round(container - total);
      if (delta !== 0) {
        const target = flexIdx.length > 0 ? flexIdx[flexIdx.length - 1] : (rounded.length - 1);
        const minVal = minW[target] || 30;
        rounded[target] = Math.max(minVal, rounded[target] + delta);
      }
      return rounded.map(px => px + 'px');
    } catch {
      return widths;
    }
  }

  private updateTableStyleWidth(): void {
    try {
      const total = this.visibleColumns.reduce((sum, c) => sum + this.parsePx(c.width, 120), 0);
      const container = this.getContainerWidth();
      const width = container > 0 ? Math.min(container, Math.floor(total)) : Math.floor(total);
      this.tableStyle = { width: Math.max(100, width) + 'px', 'table-layout': 'fixed' };
      this.cdr.markForCheck();
    } catch {}
  }

  private adjustToMeasuredContainer(): void {
    try {
      if (!this.pTable?.el) return;
      const host = this.pTable.el.nativeElement as HTMLElement;
      const container = this.getContainerWidth();
      const ths: NodeListOf<HTMLTableCellElement> = host.querySelectorAll('thead th');
      const firstRow = host.querySelector('.p-datatable-tbody tr');
      // Measure header sum
      let sumHeader = 0;
      if (ths && ths.length) ths.forEach(th => (sumHeader += th.offsetWidth));
      // Measure first body row sum (if present)
      let sumBody = 0;
      if (firstRow) {
        const tds = firstRow.querySelectorAll('td');
        if (tds && tds.length) tds.forEach(td => (sumBody += (td as HTMLTableCellElement).offsetWidth));
      }
      const actual = Math.max(sumHeader, sumBody);
      let delta = Math.round(container - actual);
      if (delta === 0) return;
      // Adjust the last flexible column to absorb the delta
      const isFlexible = (i: number) => {
        const t = this.visibleColumns[i]?.type;
        return t === 'string' || t === 'list' || t === undefined;
      };
      const flexIdx = this.visibleColumns.map((_, i) => i).filter(isFlexible);
      const target = flexIdx.length > 0 ? flexIdx[flexIdx.length - 1] : (this.visibleColumns.length - 1);
      const current = this._getColumnWidths() || this.visibleColumns.map(c => c.width || '120px');
      const minW = this.getMinWidths();
      const now = current.map(w => this.parsePx(String(w), 120));
      const newVal = Math.max(minW[target] || 30, now[target] + delta);
      this.applyWidthsByIndex([[target, newVal]]);
      this.updateTableStyleWidth();
    } catch {}
  }

  private autoExpandToContainer(): void {
    try {
      if (!this.pTable?.el) return;
      const host = this.pTable.el.nativeElement as HTMLElement;
      const wrapper = (host.querySelector('.p-datatable-wrapper') as HTMLElement) || host;
      const container = wrapper.clientWidth || host.clientWidth;
      const total = this.visibleColumns.reduce((sum, c) => sum + this.parsePx(c.width, 120), 0);
      const diff = Math.round(container - total);
      if (diff > 8) {
        // add the extra space to the last resizable column
        let idx = this.visibleColumns.length - 1;
        while (idx >= 0) {
          const t = this.visibleColumns[idx]?.type;
          if (t !== 'lineNumber' && t !== 'checkbox' && t !== 'expander') break;
          idx--;
        }
        if (idx >= 0) {
          const newW = this.parsePx(this.visibleColumns[idx].width, 120) + diff;
          this.applyWidthsByIndex([[idx, newW]]);
          this.updateTableStyleWidth();
        }
      } else {
        this.updateTableStyleWidth();
      }
    } catch {}
  }

  // === Group header select-all helpers ===
  isAllVisibleSelected(): boolean {
    if (this.mode !== 'group') return false;
    // If any expanded detail table has any unselected visible row, return false
    const anyDetail = this.detailTables && this.detailTables.length > 0;
    if (!anyDetail) return false;
    for (const table of this.detailTables.toArray()) {
      // Only consider grid-mode detail tables with data
      const dl = table.dataLoader;
      const list: any[] = (dl && (dl as any).data$?.getValue && (dl as any).data$?.getValue()) || [];
      if (!list || list.length === 0) continue;
      for (const row of list) {
        if (!this.isRowSelected(row)) {
          return false;
        }
      }
    }
    return true;
  }

  private isRowSelected(row: any): boolean {
    if (!this.selection || !Array.isArray(this.selection)) return false;
    const rowId = row?.id;
    return this.selection.some((r: any) => (r?.id ?? r) === rowId);
  }

  onGroupHeaderSelectAll(checked: boolean): void {
    if (this.mode !== 'group') return;

    // When unchecking in group mode, clear ALL selections globally per requirement
    if (!checked) {
      this.selection = [];
      this.selectionChange.emit([]);
      this.cdr.markForCheck();
      return;
    }

    // Checking: add all currently exposed detail rows to the selection
    const selectionSet = new Map<string, any>();
    if (Array.isArray(this.selection)) {
      for (const s of this.selection) {
        const id = (s && s.id) ? s.id : s;
        if (id) selectionSet.set(id, s);
      }
    }

    for (const table of this.detailTables.toArray()) {
      const dl = table.dataLoader;
      const list: any[] = (dl && (dl as any).data$?.getValue && (dl as any).data$?.getValue()) || [];
      if (!list || list.length === 0) continue;
      for (const row of list) {
        const id = row?.id;
        if (id && !selectionSet.has(id)) {
          selectionSet.set(id, row);
        }
      }
    }

    const newSelection = Array.from(selectionSet.values());
    this.selection = newSelection;
    this.selectionChange.emit(newSelection);
    this.cdr.markForCheck();
  }
}
