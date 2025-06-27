/* eslint-disable */ 

import { Component, Input, Output, EventEmitter, ContentChild, TemplateRef, ViewChild, OnInit, ViewChildren, QueryList, AfterViewInit, OnDestroy } from '@angular/core';
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
  type?: 'checkbox' | 'expander' | 'string' | 'date' | 'boolean' | 'list' | 'lineNumber';
  dateFormat?: string;
  listOptions?: { label: string; value: string }[];
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
export class SuperTable implements OnInit, AfterViewInit, OnDestroy {
    @Input() dataLoader: DataLoader<any> | undefined;
    @Input() columns: ColumnConfig[] = [];
    @Input() groups: string[] = [];
    @Input() mode: 'grid' | 'group' = 'grid';
    @Input() groupQuery: ((groupName: string) => DataLoader<any>) | undefined;
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
    @Input() selectionMode: 'single' | 'multiple' | null | undefined;
    @Input() selection: any;
    @Input() expandedRowKeys: { [key: string]: boolean } = {};
    @Input() loadingMessage: string | undefined;
    @Input() superTableParent: any;
    @Input() expandedRowTemplate: TemplateRef<any> | undefined;

    @ViewChild('pTable') pTable!: Table;
    @ViewChildren('detailTable') detailTables!: QueryList<SuperTable>;

    groupLoaders: { [key: string]: DataLoader<any> } = {};

    private scrollContainer?: HTMLElement;
    private topGroupName?: string;
    private scrollListener = () => this.captureTopGroup();

    @ContentChild('customHeader', { read: TemplateRef }) headerTemplate?: TemplateRef<any>;
    
    @Output() rowExpand = new EventEmitter<any>();
    @Output() rowCollapse = new EventEmitter<any>();
    @Output() selectionChange = new EventEmitter<any>();
    @Output() contextMenuSelectionChange = new EventEmitter<any>();
    @Output() onContextMenuSelect = new EventEmitter<any>();
    @Output() onColResize = new EventEmitter<any>();
    @Output() onSort = new EventEmitter<any>();
  @Output() onFilter = new EventEmitter<any>();

  ngOnInit(): void {
    if (!this.superTableParent) {
      throw new Error('superTableParent is a required input');
    }
  }

  ngAfterViewInit(): void {
    if (this.mode === 'group') {
      const body = (this.pTable?.el?.nativeElement as HTMLElement)?.querySelector(
        '.p-datatable-scrollable-body'
      );
      if (body) {
        this.scrollContainer = body as HTMLElement;
        this.scrollContainer.addEventListener('scroll', this.scrollListener);
        this.captureTopGroup();
      }
    }
  }

  ngOnDestroy(): void {
    this.scrollContainer?.removeEventListener('scroll', this.scrollListener);
  }

    trackByFn(index: number, item: any): any {
      return item.id || index;
    }

    isRowExpanded(row: any): boolean {
      const key = row.id || row.key || JSON.stringify(row);
      return this.expandedRowKeys[key] === true;
    }

    isGroupExpanded(groupName: string): boolean {
      return this.expandedRowKeys[groupName] === true;
    }

    onGroupToggle(groupName: string): void {
      const isExpanded = this.isGroupExpanded(groupName);
      if (isExpanded) {
        delete this.expandedRowKeys[groupName];
        this.rowCollapse.emit({ data: groupName });
      } else {
        this.expandedRowKeys[groupName] = true;
        if (this.groupQuery && !this.groupLoaders[groupName]) {
          this.groupLoaders[groupName] = this.groupQuery(groupName);
        }
        this.rowExpand.emit({ data: groupName });
      }
    }

    onRowExpand(event: { originalEvent: Event, data: any }) {
      console.log('Expanded:', event.data);
      this.expandedRowKeys[event.data.id] = true;
      this.rowExpand.emit(event);
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
    this.detailTables?.forEach(table => table.applySort(event));
    setTimeout(() => {
      if (targetGroup) {
        this.scrollToGroup(targetGroup);
      }
    });
  }

  onHeaderFilter(event: any): void {
    const targetGroup = this.topGroupName;
    this.detailTables?.forEach(table => table.applyFilter(event));
    this.pTable.filteredValue = null;
    setTimeout(() => {
      if (targetGroup) {
        this.scrollToGroup(targetGroup);
      }
    });
  }

  private captureTopGroup(): void {
    if (!this.scrollContainer || this.mode !== 'group') {
      return;
    }
    const rows = Array.from(
      this.scrollContainer.querySelectorAll('tbody > tr')
    );
    for (const row of rows) {
      const el = row as HTMLElement;
      if (el.offsetTop + el.offsetHeight > this.scrollContainer.scrollTop) {
        if (el.classList.contains('p-row-odd')) {
          const nameCell = el.querySelector('td:nth-child(2)');
          this.topGroupName = nameCell?.textContent?.trim() || undefined;
        }
        break;
      }
    }
  }

  private scrollToGroup(groupName: string): void {
    if (!this.scrollContainer) {
      return;
    }
    const rows = Array.from(
      this.scrollContainer.querySelectorAll('tbody > tr')
    );
    for (const row of rows) {
      const el = row as HTMLElement;
      if (el.classList.contains('p-row-odd')) {
        const nameCell = el.querySelector('td:nth-child(2)');
        if (nameCell?.textContent?.trim() === groupName) {
          this.scrollContainer.scrollTop = el.offsetTop;
          break;
        }
      }
    }
  }

  onHeaderColResize(event: any): void {
    if (event?.element) {
      const index = (event.element as any).cellIndex;
      const newWidth = event.element.offsetWidth + 'px';
      if (this.columns[index]) {
        this.columns[index].width = newWidth;
        this.columns = [...this.columns];
      }
    }

    this.detailTables?.forEach(table => {
      table.columns = [...this.columns];
    });
  }

}
