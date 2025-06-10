/* eslint-disable */ 

import { Component, Input, Output, EventEmitter, ContentChild, TemplateRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { CheckboxModule } from 'primeng/checkbox';
import { RippleModule } from 'primeng/ripple';

export interface ColumnConfig {
  field: string;
  header: string;
  filterType?: 'text' | 'date' | 'numeric' | 'boolean';
  width?: string;
  style?: string;
  type?: 'checkbox' | 'expander' | 'data';
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
  ],
})
export class SuperTable {
    @Input() value: any[] = [];
    @Input() columns: ColumnConfig[] = [];
    @Input() resizableColumns = false;
    @Input() reorderableColumns = false;
    @Input() scrollable = false;
    @Input() paginator = false;
    @Input() dataKey: string | undefined;
    @Input() globalFilterFields: string[] = [];
    @Input() contextMenuSelection: any;
    @Input() contextMenu: any;
    @Input() selectionMode: 'single' | 'multiple' | null | undefined;
    @Input() selection: any;
    @Input() expandedRowKeys: { [key: string]: boolean } = {};
    @Input() superTableParent: any;

    @ContentChild('customHeader', { read: TemplateRef }) headerTemplate?: TemplateRef<any>;   
    
    @Output() onRowExpand = new EventEmitter<any>();
    @Output() onRowCollapse = new EventEmitter<any>();
    @Output() selectionChange = new EventEmitter<any>();
    @Output() onContextMenuSelect = new EventEmitter<any>();
    @Output() onColResize = new EventEmitter<any>();

    toggleRow(row: any) {
      const key = row.id || row.key || JSON.stringify(row);
      this.expandedRowKeys[key] = !this.expandedRowKeys[key];
    }
}
