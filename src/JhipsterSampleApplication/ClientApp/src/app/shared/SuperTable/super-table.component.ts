/* eslint-disable */ 

import { Component, Input, Output, EventEmitter, ContentChild, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { CheckboxModule } from 'primeng/checkbox';
import { RippleModule } from 'primeng/ripple';
import { Table } from 'primeng/table';
import { MultiSelectModule } from 'primeng/multiselect';
import { FormsModule } from '@angular/forms';

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
export class SuperTable {
    @Input() value: any[] = [];
    @Input() columns: ColumnConfig[] = [];
    @Input() groups: string[] | undefined;
    @Input() displayMode: 'grid' | 'group' = 'grid';
    @Input() loading = false;
    @Input() resizableColumns = false;
    @Input() reorderableColumns = false;
    @Input() scrollable = false;
    @Input() paginator = false;
    @Input() showHeader = true;
    @Input() dataKey: string | undefined;
    @Input() globalFilterFields: string[] = [];
    @Input() contextMenuSelection: any;
    @Input() contextMenu: any;
    @Input() selectionMode: 'single' | 'multiple' | null | undefined;
    @Input() selection: any;
    @Input() expandedRowKeys: { [key: string]: boolean } = {};
    @Input() superTableParent: any;
    @Input() loadingMessage: string | undefined;

    @ViewChild('pTable') pTable!: Table;

    @ContentChild('customHeader', { read: TemplateRef }) headerTemplate?: TemplateRef<any>;
    @ContentChild('expandedRow', { read: TemplateRef, static: true }) expandedRowTemplate?: TemplateRef<any>;
    
    @Output() rowExpand = new EventEmitter<any>();
    @Output() rowCollapse = new EventEmitter<any>();
    @Output() selectionChange = new EventEmitter<any>();
    @Output() contextMenuSelectionChange = new EventEmitter<any>();
    @Output() onContextMenuSelect = new EventEmitter<any>();
    @Output() onColResize = new EventEmitter<any>();
    @Output() onSort = new EventEmitter<any>();

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
}
