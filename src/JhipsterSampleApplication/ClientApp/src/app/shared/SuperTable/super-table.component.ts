/* eslint-disable */

import {
  Component,
  Input,
  Output,
  EventEmitter,
  ContentChild,
  TemplateRef,
  ViewChild,
  OnInit,
  ViewChildren,
  QueryList,
  AfterViewInit,
  OnDestroy,
  OnChanges,
  SimpleChanges,
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
})
export class SuperTable implements OnInit, AfterViewInit, OnDestroy, OnChanges {
  @Input() dataLoader: DataLoader<any> | undefined;
  @Input() columns: ColumnConfig[] = [];
  @Input() groups: GroupDescriptor[] = [];
  @Input() mode: 'grid' | 'group' = 'grid';
  @Input() groupQuery:
    | ((group: GroupDescriptor) => GroupData)
    | undefined;
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
  @Input() expandedRowTemplate: TemplateRef<any> | undefined;

  @ViewChild('pTable') pTable!: Table;
  @ViewChildren('detailTable') detailTables!: QueryList<SuperTable>;

  groupLoaders: { [key: string]: GroupData | undefined } = {};

  private lastSortEvent: any;
  private lastFilterEvent: any;
  private lastColumnWidths: string[] | undefined;

  private scrollContainer?: HTMLElement;
  private topGroupName?: string;
  private scrollListener = () => this.captureTopGroup();
  private capturedWidths = false;

  get visibleColumns(): ColumnConfig[] {
    return this.showRowNumbers ? this.columns : this.columns.filter(c => c.type !== 'lineNumber');
  }

  @ContentChild('customHeader', { read: TemplateRef })
  headerTemplate?: TemplateRef<any>;

  @Output() rowExpand = new EventEmitter<any>();
  @Output() rowCollapse = new EventEmitter<any>();
  @Output() selectionChange = new EventEmitter<any>();
  @Output() contextMenuSelectionChange = new EventEmitter<any>();
  @Output() onContextMenuSelect = new EventEmitter<any>();
  @Output() onColResize = new EventEmitter<any>();
  @Output() onSort = new EventEmitter<any>();
  @Output() onFilter = new EventEmitter<any>();

  constructor(private cdr: ChangeDetectorRef) {}

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
          this.initGroupScroll();
        });
      } else {
        this.destroyGroupScroll();
      }
    }
  }

  ngAfterViewInit(): void {
    if (this.mode === 'group') {
      setTimeout(() => {
        this.captureColumnWidths();
        this.initGroupScroll();
      });
    }
  }

  ngOnDestroy(): void {
    this.destroyGroupScroll();
  }

  trackByFn(index: number, item: any): any {
    return item.id || index;
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
      setTimeout(() => this.applyStoredStateToDetails());
    }
  }

  onRowExpand(event: { originalEvent: Event; data: any }) {
    console.log('Expanded:', event.data);
    this.expandedRowKeys[event.data.id] = true;
    this.rowExpand.emit(event);
    setTimeout(() => this.applyStoredStateToDetails());
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
  }

  handleSort(event: any): void {
    this.lastSortEvent = event;
    this.onSort.emit(event);
  }

  handleFilter(event: any): void {
    this.lastFilterEvent = event;
    this.onFilter.emit(event);
  }

  private applyStoredStateToDetails(): void {
    const currentWidths = this._getColumnWidths();
    if (currentWidths) {
      this.lastColumnWidths = currentWidths;
    }

    this.detailTables?.forEach(table => {
      table.columns = [...this.columns];
      if (this.lastSortEvent) {
        table.applySort(this.lastSortEvent);
      }
      if (this.lastFilterEvent) {
        table.applyFilter(this.lastFilterEvent);
      }
      if (this.lastColumnWidths) {
        table.columns = table.columns.map((c, i) => ({ ...c, width: this.lastColumnWidths![i] }));
        table.cdr.detectChanges();
      }
    });
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
    if (this.pTable && event?.filters && this.mode == 'grid') {
      (this.pTable as any).filters = event.filters;
      if ((this.pTable as any)._filter) {
        (this.pTable as any)._filter();
      }
    }
  }

  onHeaderSort(event: any): void {
    const targetGroup = this.topGroupName;
    this.lastSortEvent = event;
    this.detailTables?.forEach(table => table.applySort(event));
    requestAnimationFrame(() => {
      if (targetGroup) {
        this.scrollToGroup(targetGroup);
      }
    });
  }

  onHeaderFilter(event: any): void {
    const targetGroup = this.topGroupName;
    this.lastFilterEvent = event;
    this.detailTables?.forEach(table => table.applyFilter(event));
    this.pTable.filteredValue = null;
    setTimeout(() => {
      if (targetGroup) {
        this.scrollToGroup(targetGroup);
      }
    });
  }

  private _getColumnWidths(): string[] | undefined {
    if (this.superTableParent) {
      return this.superTableParent._getColumnWidths();
    }

    if (this.pTable?.el) {
      const header: NodeListOf<HTMLTableCellElement> = this.pTable.el.nativeElement.querySelectorAll('th');
      if (header) {
        return Array.from(header).map((th: HTMLTableCellElement) => th.offsetWidth + 'px');
      }
    }
    return undefined;
  }

  private captureColumnWidths(): void {
    if (this.capturedWidths || this.mode !== 'group') {
      return;
    }
    const widths = this._getColumnWidths();
    if (widths) {
      this.columns.forEach((col, index) => {
        if (col) {
          col.width = widths[index] || '';
        }
      });
      this.columns = [...this.columns];
      this.lastColumnWidths = widths;
      this.capturedWidths = true;
    }
  }

  private captureTopGroup(): void {
    if (!this.scrollContainer || this.mode !== 'group') {
      return;
    }

    const rows = Array.from(this.scrollContainer.querySelectorAll('tbody > tr.p-row-odd')) as HTMLElement[];
    let topRow: HTMLElement | undefined;
    let minDistance = Number.MAX_VALUE;

    for (const row of rows) {
      const distance = Math.abs(row.offsetTop - this.scrollContainer.scrollTop);
      if (distance < minDistance) {
        minDistance = distance;
        topRow = row;
      }
    }

    if (topRow) {
      const nameEl = topRow.querySelector('span.group-name');
      const groupName = nameEl?.textContent?.trim();
      this.topGroupName = groupName;
    }
  }

  private scrollToGroup(groupName: string): void {
    if (!this.scrollContainer) {
      return;
    }
  
    const rows = Array.from(this.scrollContainer.querySelectorAll('tbody > tr.p-row-odd')) as HTMLElement[];
  
    for (const row of rows) {
      const nameEl = row.querySelector('span.group-name');
      const text = nameEl?.textContent?.trim();
      if (text === groupName) {
        this.scrollContainer.scrollTop = row.offsetTop;
        return;
      }
    }
  }

  onHeaderColResize(event: any): void {
    this.lastColumnWidths = this._getColumnWidths();
    this.detailTables?.forEach(table => {
      if (this.lastColumnWidths) {
        table.columns = table.columns.map((c, i) => ({ ...c, width: this.lastColumnWidths![i] }));
        table.cdr.detectChanges();
      }
    });
    this.onColResize.emit(event);
    requestAnimationFrame(() => {
      if (this.topGroupName) {
        this.scrollToGroup(this.topGroupName);
      }
    });
  }

  private initGroupScroll(): void {
    const body =
      (this.pTable?.scroller?.getElementRef()?.nativeElement as HTMLElement) ||
      (this.pTable?.wrapperViewChild?.nativeElement as HTMLElement);
    if (body) {
      this.destroyGroupScroll();
      this.scrollContainer = body;
      this.scrollContainer.addEventListener('scroll', this.scrollListener);
      this.captureTopGroup();
    }
  }

  private destroyGroupScroll(): void {
    this.scrollContainer?.removeEventListener('scroll', this.scrollListener);
    this.scrollContainer = undefined;
  }
}
