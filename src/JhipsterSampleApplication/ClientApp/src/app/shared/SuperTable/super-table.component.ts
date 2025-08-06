/* eslint-disable */

import {
  Component,
  OnInit,
  AfterViewInit,
  OnDestroy,
  OnChanges,
  SimpleChanges,
  Input,
  Output,
  EventEmitter,
  ViewChild,
  ViewChildren,
  QueryList,
  TemplateRef,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
} from '@angular/core';
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
  @Input() dataLoader: DataLoader<any> | undefined;
  @Input() columns: ColumnConfig[] = [];
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

  @ViewChild('pTable') pTable!: Table;
  @ViewChildren('detailTable') detailTables!: QueryList<SuperTable>;

  groupLoaders: { [key: string]: GroupData | undefined } = {};

  private lastSortEvent: any;
  private lastFilterEvent: any;
  private lastColumnWidths: string[] | undefined;

  private scrollContainer?: HTMLElement;
  private lastScrollTop = 0;
  private desiredScrollTop: number | null = null;
  private scrollRestoreHandle: any;
  private scrollListener = () => {
    if (this.scrollContainer) {
      this.lastScrollTop = this.scrollContainer.scrollTop;
    }
  };
  private capturedWidths = false;

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

  constructor(private cdr: ChangeDetectorRef) {}

  trackByFn(index: number, item: any): any {
    return item?.id || index;
  }

  ngOnInit(): void {
    // no initialization required
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
        });
      } else {
        this.initScroll();
      }
    }
    if (changes['groups'] && !changes['groups'].firstChange) {
      // Clear cached group data and expanded state when groups change
      this.groupLoaders = {};
      this.expandedRowKeys = {};
    }
  }

  ngAfterViewInit(): void {
    setTimeout(() => {
      if (this.mode === 'group') {
        this.captureColumnWidths();
      }
      this.initScroll();
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

  onHeaderSort(event: any): void {
    this.lastSortEvent = event;
    this.detailTables?.forEach((table) => table.applySort(event));
    requestAnimationFrame(() => this.attemptScrollRestore());
  }

  onHeaderFilter(event: any): void {
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

  private initScroll(): void {
    const body =
      (this.pTable?.scroller?.getElementRef()?.nativeElement as HTMLElement) ||
      (this.pTable?.wrapperViewChild?.nativeElement as HTMLElement);
    if (body) {
      this.destroyScroll();
      this.scrollContainer = body;
      this.scrollContainer.addEventListener('scroll', this.scrollListener);
      this.lastScrollTop = this.scrollContainer.scrollTop;
      this.desiredScrollTop = this.lastScrollTop;
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
      this.scrollRestoreHandle = null;
    }
  }

  private destroyScroll(): void {
    this.scrollContainer?.removeEventListener('scroll', this.scrollListener);
    this.scrollContainer = undefined;
    if (this.scrollRestoreHandle) {
      clearTimeout(this.scrollRestoreHandle);
      this.scrollRestoreHandle = null;
    }
  }
}
