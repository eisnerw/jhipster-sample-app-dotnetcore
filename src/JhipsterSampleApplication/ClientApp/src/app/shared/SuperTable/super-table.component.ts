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

export interface ColumnConfig {
  field: string;
  header: string;
  filterType?: 'text' | 'date' | 'numeric' | 'boolean';
  width?: string;
  style?: string;
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
  private cdr = inject(ChangeDetectorRef);

  @Input() dataLoader: DataLoader<any> | undefined;
  @Input() columns: ColumnConfig[] = [];
  @Input() widthKey: string | undefined;
  @Input() initialWidths: (string | undefined)[] | undefined;
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
    // no initialization required
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
    if ((changes['columns'] && !changes['columns'].firstChange) || changes['initialWidths'] || changes['widthKey']) {
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
    const onMove = (e: MouseEvent) => this.onResizeMove(e, left, right);
    const onUp = (e: MouseEvent) => this.onResizeEnd(e, onMove, onUp);
    document.addEventListener('mousemove', onMove, true);
    document.addEventListener('mouseup', onUp, true);
    document.body.classList.add('st-resizing');
  }

  private onResizeMove(ev: MouseEvent, left: number, right: number): void {
    if (this.resizingIndex == null) return;
    const dx = ev.clientX - this.startX;
    let newLeft = Math.max(60, this.startLeftWidth + dx);
    let newRight = Math.max(60, this.startRightWidth - dx);
    this.applyWidthsByIndex([[left, newLeft], [right, newRight]]);
  }

  private onResizeEnd(ev: MouseEvent, onMove: any, onUp: any): void {
    document.removeEventListener('mousemove', onMove, true);
    document.removeEventListener('mouseup', onUp, true);
    document.body.classList.remove('st-resizing');
    this.lastColumnWidths = this._getColumnWidths();
    if (this.lastColumnWidths) this.writePersistedWidths(this.lastColumnWidths);
    this.resizingIndex = null;
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
      this.visibleColumns[i].width = Math.max(20, Math.round(px)) + 'px';
    });
    this.columns = [...this.columns];
    this.cdr.detectChanges();
    setTimeout(() => {
      if (!this.pTable?.el) return;
      const ths: NodeListOf<HTMLTableCellElement> = this.pTable.el.nativeElement.querySelectorAll('thead th');
      pairs.forEach(([i, px]) => {
        const th = ths[i];
        if (th) th.style.width = Math.max(20, Math.round(px)) + 'px';
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
      const arr = JSON.parse(raw);
      return Array.isArray(arr) ? (arr as string[]) : null;
    } catch {
      return null;
    }
  }

  private writePersistedWidths(widths: string[]): void {
    try {
      if (!this.widthKey) return;
      localStorage.setItem(`supertable.widths:${this.widthKey}`, JSON.stringify(widths));
    } catch {}
  }

  private applyWidthsToDom(widths: (string | undefined)[]): void {
    this.visibleColumns.forEach((c, i) => {
      const w = widths[i];
      if (w && typeof w === 'string' && w.trim()) c.width = w;
    });
    this.columns = [...this.columns];
    setTimeout(() => {
      if (!this.pTable?.el) return;
      const ths: NodeListOf<HTMLTableCellElement> = this.pTable.el.nativeElement.querySelectorAll('thead th');
      widths.forEach((w, i) => {
        if (!w || !ths[i]) return;
        ths[i].style.width = w;
      });
    }, 0);
  }

  private enforceWidthsAfterLayout(): void {
    const persisted = this.readPersistedWidths();
    const desired = (persisted && persisted.length === this.visibleColumns.length)
      ? persisted
      : (this.initialWidths && this.initialWidths.length === this.visibleColumns.length ? this.initialWidths : undefined);
    if (!desired) return;

    const applyNow = () => this.applyWidthsToDom(desired);
    // Apply immediately once
    requestAnimationFrame(applyNow);

    // Then enforce until stable for a short window, to outlast PrimeNG adjustments
    const start = Date.now();
    let lastMismatchAt = Date.now();

    const checkAndEnforce = () => {
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
      const elapsed = Date.now() - start;
      const sinceLastMismatch = Date.now() - lastMismatchAt;
      // Stop if stable for 250ms or after 1500ms total
      if (sinceLastMismatch > 250 || elapsed > 1500) {
        if (this.enforceHandle) {
          clearInterval(this.enforceHandle);
          this.enforceHandle = null;
        }
        // Persist final widths if a key is set
        const final = this._getColumnWidths();
        if (final && final.length === this.visibleColumns.length) this.writePersistedWidths(final);
      }
    };

    if (this.enforceHandle) {
      clearInterval(this.enforceHandle);
      this.enforceHandle = null;
    }
    this.enforceHandle = setInterval(checkAndEnforce, 50);
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
