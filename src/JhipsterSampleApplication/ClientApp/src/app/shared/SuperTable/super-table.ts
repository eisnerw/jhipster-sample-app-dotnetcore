/* eslint-disable */ 

import { NgModule, Component, HostListener, OnInit, OnDestroy, AfterViewInit, Directive, Optional, AfterContentInit,
    Input, Output, EventEmitter, ElementRef, NgZone, ChangeDetectorRef, OnChanges, ChangeDetectionStrategy, ViewEncapsulation, Renderer2, Inject, PLATFORM_ID} from '@angular/core';
import { CommonModule, DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SharedModule, PrimeNGConfig, FilterService, OverlayService } from 'primeng/api';
import { PaginatorModule } from 'primeng/paginator';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { SelectButtonModule } from 'primeng/selectbutton';
import { TriStateCheckboxModule } from 'primeng/tristatecheckbox';
import { CalendarModule } from 'primeng/calendar';
import { InputNumberModule } from 'primeng/inputnumber';
import { DropdownModule } from 'primeng/dropdown';
import { DomHandler } from 'primeng/dom';
import { Injectable } from '@angular/core';
import { BlockableUI } from 'primeng/api';
import { ScrollingModule } from '@angular/cdk/scrolling';
import {trigger,style,transition,animate} from '@angular/animations';
import { Table } from 'primeng/table'
import { TableService } from 'primeng/table';
import { TableBody } from 'primeng/table';
import { ColumnFilter } from 'primeng/table';
import { SelectableRow } from 'primeng/table';
import { ReorderableRow } from 'primeng/table';
import { ColumnFilterFormElement } from 'primeng/table';
import { SortIcon } from 'primeng/table';
import { SortableColumn } from 'primeng/table';
import { EditableColumn } from 'primeng/table';
import { CellEditor } from 'primeng/table';
import { ContextMenuRow } from 'primeng/table';
import { RowToggler } from 'primeng/table';
import { ResizableColumn } from 'primeng/table';
import { ReorderableColumn } from 'primeng/table';
import { TableRadioButton } from 'primeng/table';
import { TableCheckbox } from 'primeng/table';
import { TableHeaderCheckbox } from 'primeng/table';
import { TableState } from 'primeng/api';

@Injectable()

@Component({
    selector: 'super-table',
    template: `
    <div #container [ngStyle]="style" [class]="styleClass"
        [ngClass]="{'p-datatable p-component': true,
            'p-datatable-hoverable-rows': (rowHover||selectionMode),
            'p-datatable-scrollable': scrollable,
            'p-datatable-flex-scrollable': (scrollable && scrollHeight === 'flex')}" [attr.id]="id">
        <div class="p-datatable-loading-overlay p-component-overlay" *ngIf="loading && showLoader">
            <i [class]="'p-datatable-loading-icon pi-spin ' + loadingIcon"></i>
        </div>
        <div *ngIf="captionTemplate" class="p-datatable-header">
            <ng-container *ngTemplateOutlet="captionTemplate"></ng-container>
        </div>
        <p-paginator [rows]="rows" [first]="first" [totalRecords]="totalRecords" [pageLinkSize]="pageLinks" styleClass="p-paginator-top" [alwaysShow]="alwaysShowPaginator"
            (onPageChange)="onPageChange($event)" [rowsPerPageOptions]="rowsPerPageOptions" *ngIf="paginator && (paginatorPosition === 'top' || paginatorPosition =='both')"
            [templateLeft]="paginatorLeftTemplate" [templateRight]="paginatorRightTemplate" [dropdownAppendTo]="paginatorDropdownAppendTo" [dropdownScrollHeight]="paginatorDropdownScrollHeight"
            [currentPageReportTemplate]="currentPageReportTemplate" [showFirstLastIcon]="showFirstLastIcon" [dropdownItemTemplate]="paginatorDropdownItemTemplate" [showCurrentPageReport]="showCurrentPageReport" [showJumpToPageDropdown]="showJumpToPageDropdown" [showJumpToPageInput]="showJumpToPageInput" [showPageLinks]="showPageLinks"></p-paginator>

        <div #wrapper class="p-datatable-wrapper" [ngStyle]="{maxHeight: virtualScroll ? '' : scrollHeight}">
            <p-scroller #scroller *ngIf="virtualScroll" [items]="processedData" [columns]="columns" [style]="{'height': scrollHeight !== 'flex' ? scrollHeight : ''}" [scrollHeight]="scrollHeight !== 'flex' ? '' : '100%'" [itemSize]="virtualScrollItemSize||_virtualRowHeight" [delay]="lazy ? virtualScrollDelay : 0"
                [lazy]="lazy" (onLazyLoad)="onLazyItemLoad($event)" [loaderDisabled]="true" [showSpacer]="false" [showLoader]="!!loadingBodyTemplate" [options]="virtualScrollOptions">
                <ng-template pTemplate="content" let-items let-scrollerOptions="options">
                    <ng-container *ngTemplateOutlet="buildInTable; context: {$implicit: items, options: scrollerOptions}"></ng-container>
                </ng-template>
            </p-scroller>
            <ng-container *ngIf="!virtualScroll">
                <ng-container *ngTemplateOutlet="buildInTable; context: {$implicit: processedData, options: { columns }}"></ng-container>
            </ng-container>

            <ng-template #buildInTable let-items let-scrollerOptions="options">
                <table #table role="table" [ngClass]="{'p-datatable-table': true, 
                                                    'p-datatable-scrollable-table': scrollable,
                                                    'p-datatable-resizable-table': resizableColumns,
                                                    'p-datatable-resizable-table-fit': (resizableColumns && columnResizeMode === 'fit')}" 
                    [class]="tableStyleClass" [ngStyle]="tableStyle" [style]="scrollerOptions.spacerStyle" [attr.id]="id+'-table'">
                    <ng-container *ngTemplateOutlet="colGroupTemplate; context: {$implicit: scrollerOptions.columns}"></ng-container>
                    <thead #thead class="p-datatable-thead">
                        <ng-container *ngTemplateOutlet="headerGroupedTemplate||headerTemplate; context: {$implicit: scrollerOptions.columns}"></ng-container>
                    </thead>
                    <tbody class="p-datatable-tbody p-datatable-frozen-tbody" *ngIf="frozenValue||frozenBodyTemplate" [value]="frozenValue" [frozenRows]="true" [super-table-body]="scrollerOptions.columns" [pTableBodyTemplate]="frozenBodyTemplate" [frozen]="true"></tbody>
                    <tbody class="p-datatable-tbody" [ngClass]="scrollerOptions.contentStyleClass" [style]="scrollerOptions.contentStyle" [value]="dataToRender(scrollerOptions.rows)" [super-table-body]="scrollerOptions.columns" [pTableBodyTemplate]="bodyTemplate" [scrollerOptions]="scrollerOptions"></tbody>
                    <tfoot *ngIf="footerGroupedTemplate||footerTemplate" #tfoot class="p-datatable-tfoot">
                        <ng-container *ngTemplateOutlet="footerGroupedTemplate||footerTemplate; context: {$implicit: scrollerOptions.columns}"></ng-container>
                    </tfoot>
                </table>
            </ng-template>
        </div>

        <p-paginator [rows]="rows" [first]="first" [totalRecords]="totalRecords" [pageLinkSize]="pageLinks" styleClass="p-paginator-bottom" [alwaysShow]="alwaysShowPaginator"
            (onPageChange)="onPageChange($event)" [rowsPerPageOptions]="rowsPerPageOptions" *ngIf="paginator && (paginatorPosition === 'bottom' || paginatorPosition =='both')"
            [templateLeft]="paginatorLeftTemplate" [templateRight]="paginatorRightTemplate" [dropdownAppendTo]="paginatorDropdownAppendTo" [dropdownScrollHeight]="paginatorDropdownScrollHeight"
            [currentPageReportTemplate]="currentPageReportTemplate" [showFirstLastIcon]="showFirstLastIcon" [dropdownItemTemplate]="paginatorDropdownItemTemplate" [showCurrentPageReport]="showCurrentPageReport" [showJumpToPageDropdown]="showJumpToPageDropdown" [showJumpToPageInput]="showJumpToPageInput" [showPageLinks]="showPageLinks"></p-paginator>

        <div *ngIf="summaryTemplate" class="p-datatable-footer">
            <ng-container *ngTemplateOutlet="summaryTemplate"></ng-container>
        </div>

        <div #resizeHelper class="p-column-resizer-helper" style="display:none" *ngIf="resizableColumns"></div>
        <span #reorderIndicatorUp class="pi pi-arrow-down p-datatable-reorder-indicator-up" style="display:none" *ngIf="reorderableColumns"></span>
        <span #reorderIndicatorDown class="pi pi-arrow-up p-datatable-reorder-indicator-down" style="display:none" *ngIf="reorderableColumns"></span>
    </div>
    `,
    providers: [TableService],
    changeDetection: ChangeDetectionStrategy.Default,
    encapsulation: ViewEncapsulation.None,
    styles: [`
    .p-datatable {
        position: relative;
    }
    
    .p-datatable table {
        border-collapse: collapse;
        min-width: 100%;
        table-layout: fixed;
    }
    
    .p-datatable .p-sortable-column {
        cursor: pointer;
        user-select: none;
    }
    
    .p-datatable .p-sortable-column .p-column-title,
    .p-datatable .p-sortable-column .p-sortable-column-icon,
    .p-datatable .p-sortable-column .p-sortable-column-badge {
        vertical-align: middle;
    }
    
    .p-datatable .p-sortable-column .p-sortable-column-badge {
        display: inline-flex;
        align-items: center;
        justify-content: center;
    }
    
    .p-datatable-auto-layout > .p-datatable-wrapper {
        overflow-x: auto;
    }
    
    .p-datatable-auto-layout > .p-datatable-wrapper > table {
        table-layout: auto;
    }
    
    .p-datatable-responsive-scroll > .p-datatable-wrapper {
        overflow-x: auto;
    }
    
    .p-datatable-responsive-scroll > .p-datatable-wrapper > table,
    .p-datatable-auto-layout > .p-datatable-wrapper > table {
        table-layout: auto;
    }
    
    .p-datatable-hoverable-rows .p-selectable-row {
        cursor: pointer;
    }
    
    /* Scrollable */
    .p-datatable-scrollable .p-datatable-wrapper {
        position: relative;
        overflow: auto;
    }
    
    .p-datatable-scrollable .p-datatable-thead,
    .p-datatable-scrollable .p-datatable-tbody,
    .p-datatable-scrollable .p-datatable-tfoot {
        display: block;
    }
    
    .p-datatable-scrollable .p-datatable-thead > tr,
    .p-datatable-scrollable .p-datatable-tbody > tr,
    .p-datatable-scrollable .p-datatable-tfoot > tr {
        display: flex;
        flex-wrap: nowrap;
        width: 100%;
    }
    
    .p-datatable-scrollable .p-datatable-thead > tr > th,
    .p-datatable-scrollable .p-datatable-tbody > tr > td,
    .p-datatable-scrollable .p-datatable-tfoot > tr > td {
        display: flex;
        flex: 1 1 0;
        align-items: center;
    }
    
    .p-datatable-scrollable > .p-datatable-wrapper > .p-datatable-table > .p-datatable-thead,
    .p-datatable-scrollable > .p-datatable-wrapper > .p-datatable-virtual-scrollable-body > .cdk-virtual-scroll-content-wrapper > .p-datatable-table > .p-datatable-thead {
        position: sticky;
        top: 0;
        z-index: 1;
    }
    
    .p-datatable-scrollable > .p-datatable-wrapper > .p-datatable-table > .p-datatable-frozen-tbody {
        position: sticky;
        z-index: 1;
    }
    
    .p-datatable-scrollable > .p-datatable-wrapper > .p-datatable-table > .p-datatable-tfoot {
        position: sticky;
        bottom: 0;
        z-index: 1;
    }
    
    .p-datatable-scrollable .p-frozen-column {
        position: sticky;
        background: inherit;
    }
    
    .p-datatable-scrollable th.p-frozen-column {
        z-index: 1;
    }
    
    .p-datatable-scrollable-both .p-datatable-thead > tr > th,
    .p-datatable-scrollable-both .p-datatable-tbody > tr > td,
    .p-datatable-scrollable-both .p-datatable-tfoot > tr > td,
    .p-datatable-scrollable-horizontal .p-datatable-thead > tr > th
    .p-datatable-scrollable-horizontal .p-datatable-tbody > tr > td,
    .p-datatable-scrollable-horizontal .p-datatable-tfoot > tr > td {
        flex: 0 0 auto;
    }
    
    .p-datatable-flex-scrollable {
        display: flex;
        flex-direction: column;
        height: 100%;
    }
    
    .p-datatable-flex-scrollable .p-datatable-wrapper {
        display: flex;
        flex-direction: column;
        flex: 1;
        height: 100%;
    }
    
    .p-datatable-scrollable .p-rowgroup-header {
        position: sticky;
        z-index: 1;
    }
    
    .p-datatable-scrollable.p-datatable-grouped-header .p-datatable-thead,
    .p-datatable-scrollable.p-datatable-grouped-footer .p-datatable-tfoot {
        display: table;
        border-collapse: collapse;
        width: 100%;
        table-layout: fixed;
    }
    
    .p-datatable-scrollable.p-datatable-grouped-header .p-datatable-thead > tr,
    .p-datatable-scrollable.p-datatable-grouped-footer .p-datatable-tfoot > tr {
        display: table-row;
    }
    
    .p-datatable-scrollable.p-datatable-grouped-header .p-datatable-thead > tr > th,
    .p-datatable-scrollable.p-datatable-grouped-footer .p-datatable-tfoot > tr > td {
        display: table-cell;
    }
    
    /* Flex Scrollable */
    .p-datatable-flex-scrollable {
        display: flex;
        flex-direction: column;
        flex: 1;
        height: 100%;
    }
    
    .p-datatable-flex-scrollable .p-datatable-virtual-scrollable-body {
        flex: 1;
    }
    
    /* Resizable */
    .p-datatable-resizable > .p-datatable-wrapper {
        overflow-x: auto;
    }
    
    .p-datatable-resizable .p-datatable-thead > tr > th,
    .p-datatable-resizable .p-datatable-tfoot > tr > td,
    .p-datatable-resizable .p-datatable-tbody > tr > td {
        overflow: hidden;
        white-space: nowrap;
    }
    
    .p-datatable-resizable .p-resizable-column:not(.p-frozen-column) {
        background-clip: padding-box;
        position: relative;
    }
    
    .p-datatable-resizable-fit .p-resizable-column:last-child .p-column-resizer {
        display: none;
    }
    
    .p-datatable .p-column-resizer {
        display: block;
        position: absolute !important;
        top: 0;
        right: 0;
        margin: 0;
        width: .5rem;
        height: 100%;
        padding: 0px;
        cursor:col-resize;
        border: 1px solid transparent;
    }
    
    .p-datatable .p-column-resizer-helper {
        width: 1px;
        position: absolute;
        z-index: 10;
        display: none;
    }
    
    .p-datatable .p-row-editor-init,
    .p-datatable .p-row-editor-save,
    .p-datatable .p-row-editor-cancel {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        overflow: hidden;
        position: relative;
    }
    
    /* Expand */
    .p-datatable .p-row-toggler {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        overflow: hidden;
        position: relative;
    }
    
    /* Reorder */
    .p-datatable-reorder-indicator-up,
    .p-datatable-reorder-indicator-down {
        position: absolute;
        display: none;
    }
    
    .p-datatable-reorderablerow-handle {
        cursor: move;
    }
    
    [pReorderableColumn] {
        cursor: move;
    }
    
    /* Loader */
    .p-datatable .p-datatable-loading-overlay {
        position: absolute;
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 2;
    }
    
    /* Filter */
    .p-column-filter-row {
        display: flex;
        align-items: center;
        width: 100%;
    }
    
    .p-column-filter-menu {
        display: inline-flex;
    }
    
    .p-column-filter-row p-columnfilterformelement {
        flex: 1 1 auto;
        width: 1%;
    }
    
    .p-column-filter-menu-button,
    .p-column-filter-clear-button {
        display: inline-flex;
        justify-content: center;
        align-items: center;
        cursor: pointer;
        text-decoration: none;
        overflow: hidden;
        position: relative;
    }
    
    .p-column-filter-overlay {
        position: absolute;
        top: 0;
        left: 0;
    }
    
    .p-column-filter-row-items {
        margin: 0;
        padding: 0;
        list-style: none;
    }
    
    .p-column-filter-row-item {
        cursor: pointer;
    }
    
    .p-column-filter-add-button,
    .p-column-filter-remove-button {
        justify-content: center;
    }
    
    .p-column-filter-add-button .p-button-label,
    .p-column-filter-remove-button .p-button-label {
        flex-grow: 0;
    }
    
    .p-column-filter-buttonbar {
        display: flex;
        align-items: center;
        justify-content: space-between;
    }
    
    .p-column-filter-buttonbar .p-button {
        width: auto;
    }
    
    /* Responsive */
    .p-datatable .p-datatable-tbody > tr > td > .p-column-title {
        display: none;
    }
    
    /* Virtual Scroll*/
    
    cdk-virtual-scroll-viewport {
        outline: 0 none;
    }    `]
})
export class SuperTable extends Table implements OnInit, AfterViewInit, AfterContentInit, BlockableUI, OnChanges {

    @Input() parent: SuperTable | null = null;

    @Input() frozenColumns = [];

    @Input() frozenValue: any[] = [];    

    @Input() rowTrackBy = (index: number, item: any):any => item;

    @Input() get columns(): any {
        return this._columns;
    }

    set columns(cols: any) {
        this._columns = cols;
    }

    @Input() get selection(): any {
        return this._selection;
    }

    @Input() selectedView: any = null;

    @Input() displayingAsCategories = false;
    @Output() setTableReference: EventEmitter<any> = new EventEmitter();

    set selection(val: any) {
        this._selection = val;
        if (this.children.length > 0){
            this.children.forEach(c=>{
                c.selection = val;
                c.selectionChange.emit(c.selection);
            });
        }
    }

    children: SuperTable[] = [];

    filteringGlobal = false;
    
    constructor (@Inject(DOCUMENT) document: Document, @Inject(PLATFORM_ID) platformId: any, renderer: Renderer2, el: ElementRef, zone: NgZone, tableService: TableService, cd: ChangeDetectorRef, filterService: FilterService, overlayService: OverlayService){
        super(document, platformId, renderer, el, zone, tableService, cd, filterService, overlayService,);
    }

    @Input() get value(): any[] {
        return this._value;
    }
    set value(val: any[]) {
        this._value = val;
    }

    ngOnInit(): any {
        super.ngOnInit();
        if (this.parent !== null){
            this.parent.children.push(this);
            const child = this;
            const parent = this.parent;
            setTimeout(function() : void{
                let state: TableState = {};
                parent.saveColumnWidths(state);
                (child.columnWidthsState as any) = state.columnWidths;
                child.restoreColumnWidths();
            }, 0);
        }
        this.setTableReference.emit(this); // used to provide controller a reference to the table        
    }

    ngOnChanges(simpleChange: any): any{
        super.ngOnChanges(simpleChange);
        if (this.parent !== null){
            this._selection = this.parent?._selection;
            this.selectionKeys = this.parent?.selectionKeys;
        }
    }

    onColumnResizeEnd() {
        super.onColumnResizeEnd();
        this.setChildWidths();
    }

    setChildWidths(): void{
        let state: TableState = {}; 
        this.saveColumnWidths(state);
        this.children.forEach(child=>{
            child.setChildWidthState(state);
        });
    }

    setChildWidthState(state: TableState): void{
        (this.columnWidthsState as any) = state.columnWidths;
        this.restoreColumnWidths();
        this.children.forEach(child=>{
            child.setChildWidthState(state);
        });        
    }

    toggleRowsWithCheckbox(event: any, check: any){
        if (this.children.length == 1 && this._totalRecords == 1){
            this.children[0].toggleRowsWithCheckbox(event, check);
            let selection = this.children[0].selection;
            let selectionKeys = this.children[0].selectionKeys;
            super.toggleRowsWithCheckbox(event, check);
            this.selectionKeys = selectionKeys;
            //this.selection = selection; // use the selection changed by the child
            this.selectionChange.emit(selection);
        } else {
            if (this.children.length > 0){
                check = false;
                this.children.forEach(c=>{
                    c.toggleRowsWithCheckbox(event, check);
                });
            }
            super.toggleRowsWithCheckbox(event, check);
        }
    }

    filter(value: any, field: string, matchMode: string) {
        if (this.parent === null && !(field === "global" || this.displayingAsCategories)){
            if (!this.isFilterBlank(value)) {
                this.filters[field] = { value: value, matchMode: matchMode };
            } else if (this.filters[field]) {
                delete this.filters[field];
            }
            this.children.forEach(c=>{
                c.filter(value, field, matchMode);
            });
        } else {
            if (field === "global" && this.displayingAsCategories){
                this.filteringGlobal = true;
            }
            super.filter(value, field, matchMode);
        }
    }

    doFilter(){
        let childFilters : any = {};
        Object.keys(this.filters).forEach(k=>{
            if (k !== "global" || !this.displayingAsCategories){
                childFilters[k] = this.filters[k];
            }
        });
        this.children.forEach(c=>{
            c.filters = childFilters;
            c._filter();
        }); 
    }

    _filter(){
        if (this.children.length > 0 && !this.filteringGlobal){
            let childFilters : any = {
                
            };
            Object.keys(this.filters).forEach(k=>{
                if (k !== "global" || !this.displayingAsCategories){
                    childFilters[k] = this.filters[k];
                }
            });
            this.children.forEach(c=>{
                c.filters = childFilters;
                c._filter();
            });
        } else {
            super._filter();
            if (this.filteringGlobal){
                this.filteringGlobal = false;
            }
        }
    }

    sortSingle(){
        if (this.children.length > 0){
            this.children.forEach(c=>{
                c.sortField = this.sortField;
                c.sortOrder = this.sortOrder;
                c.sortSingle();
            });
            let sortMeta: any = {
                field: this.sortField,
                order: this.sortOrder
            };

            this.onSort.emit(sortMeta);
            this.tableService.onSort(sortMeta);            
        } else {
            super.sortSingle();
        }
    }

    setChildSelection(child : SuperTable, selectingTable : SuperTable){
        if (child.children.length > 0){
            child.children.forEach(c=>{
                c.setChildSelection(c, selectingTable);
            })
        } else if (child !== selectingTable){
            // insure each child has the same selection
            child.selection = selectingTable.selection;
            child.selectionKeys = selectingTable.selectionKeys;
            // change the visual checkboxes
            child.tableService.onSelectionChange();
        }
    }    
}

@Component({
    selector: '[super-table-body]',
    template: `
        <ng-container *ngIf="!dt.expandedRowTemplate && !dt.virtualScroll">
        <ng-template ngFor let-rowData let-rowIndex="index" [ngForOf]="value" [ngForTrackBy]="dt.rowTrackBy">
            <ng-container *ngIf="dt.groupHeaderTemplate && dt.rowGroupMode === 'subheader' && shouldRenderRowGroupHeader(value, rowData, getRowIndex(rowIndex))" role="row">
                <ng-container *ngTemplateOutlet="dt.groupHeaderTemplate; context: {$implicit: rowData, rowIndex: getRowIndex(rowIndex), columns: columns, editing: (dt.editMode === 'row' && dt.isRowEditing(rowData)), frozen: frozen}"></ng-container>
            </ng-container>
            <ng-container *ngIf="dt.rowGroupMode !== 'rowspan'">
                <ng-container *ngTemplateOutlet="template; context: {$implicit: rowData, rowIndex: getRowIndex(rowIndex), columns: columns, editing: (dt.editMode === 'row' && dt.isRowEditing(rowData)), frozen: frozen}"></ng-container>
            </ng-container>
            <ng-container *ngIf="dt.rowGroupMode === 'rowspan'">
                <ng-container *ngTemplateOutlet="template; context: {$implicit: rowData, rowIndex: getRowIndex(rowIndex), columns: columns, editing: (dt.editMode === 'row' && dt.isRowEditing(rowData)), frozen: frozen, rowgroup: shouldRenderRowspan(value, rowData, getRowIndex(rowIndex)), rowspan: calculateRowGroupSize(value, rowData, getRowIndex(rowIndex))}"></ng-container>
            </ng-container>
            <ng-container *ngIf="dt.groupFooterTemplate && dt.rowGroupMode === 'subheader' && shouldRenderRowGroupFooter(value, rowData, getRowIndex(rowIndex))" role="row">
                <ng-container *ngTemplateOutlet="dt.groupFooterTemplate; context: {$implicit: rowData, rowIndex: getRowIndex(rowIndex), columns: columns, editing: (dt.editMode === 'row' && dt.isRowEditing(rowData)), frozen: frozen}"></ng-container>
            </ng-container>
        </ng-template>
    </ng-container>
    <ng-container *ngIf="!dt.expandedRowTemplate && dt.virtualScroll">
        <ng-template ngFor let-rowData let-rowIndex="index" [ngForOf]="dt.filteredValue||dt.value" [ngForTrackBy]="dt.rowTrackBy">
            <ng-container *ngTemplateOutlet="rowData ? template: dt.loadingBodyTemplate; context: {$implicit: rowData, rowIndex: getRowIndex(rowIndex), columns: columns, editing: (dt.editMode === 'row' && dt.isRowEditing(rowData)), frozen: frozen}"></ng-container>
        </ng-template>
    </ng-container>
    <ng-container *ngIf="dt.expandedRowTemplate && !(frozen && dt.frozenExpandedRowTemplate)">
        <ng-template ngFor let-rowData let-rowIndex="index" [ngForOf]="value" [ngForTrackBy]="dt.rowTrackBy">
            <ng-container *ngIf="!dt.groupHeaderTemplate">
                <ng-container *ngTemplateOutlet="template; context: {$implicit: rowData, rowIndex: getRowIndex(rowIndex), columns: columns, expanded: dt.isRowExpanded(rowData), editing: (dt.editMode === 'row' && dt.isRowEditing(rowData)), frozen: frozen}"></ng-container>
            </ng-container>
            <ng-container *ngIf="dt.groupHeaderTemplate && dt.rowGroupMode === 'subheader' && shouldRenderRowGroupHeader(value, rowData, getRowIndex(rowIndex))" role="row">
                <ng-container *ngTemplateOutlet="dt.groupHeaderTemplate; context: {$implicit: rowData, rowIndex: getRowIndex(rowIndex), columns: columns, expanded: dt.isRowExpanded(rowData), editing: (dt.editMode === 'row' && dt.isRowEditing(rowData)), frozen: frozen}"></ng-container>
            </ng-container>
            <ng-container *ngIf="dt.isRowExpanded(rowData)">
                <ng-container *ngTemplateOutlet="dt.expandedRowTemplate; context: {$implicit: rowData, rowIndex: getRowIndex(rowIndex), columns: columns, frozen: frozen}"></ng-container>
                <ng-container *ngIf="dt.groupFooterTemplate && dt.rowGroupMode === 'subheader' && shouldRenderRowGroupFooter(value, rowData, getRowIndex(rowIndex))" role="row">
                    <ng-container *ngTemplateOutlet="dt.groupFooterTemplate; context: {$implicit: rowData, rowIndex: getRowIndex(rowIndex), columns: columns, expanded: dt.isRowExpanded(rowData), editing: (dt.editMode === 'row' && dt.isRowEditing(rowData)), frozen: frozen}"></ng-container>
                </ng-container>
            </ng-container>
        </ng-template>
    </ng-container>
    <ng-container *ngIf="dt.frozenExpandedRowTemplate && frozen">
        <ng-template ngFor let-rowData let-rowIndex="index" [ngForOf]="value" [ngForTrackBy]="dt.rowTrackBy">
            <ng-container *ngTemplateOutlet="template; context: {$implicit: rowData, rowIndex: getRowIndex(rowIndex), columns: columns, expanded: dt.isRowExpanded(rowData), editing: (dt.editMode === 'row' && dt.isRowEditing(rowData)), frozen: frozen}"></ng-container>
            <ng-container *ngIf="dt.isRowExpanded(rowData)">
                <ng-container *ngTemplateOutlet="dt.frozenExpandedRowTemplate; context: {$implicit: rowData, rowIndex: getRowIndex(rowIndex), columns: columns, frozen: frozen}"></ng-container>
            </ng-container>
        </ng-template>
    </ng-container>
    <ng-container *ngIf="dt.loading">
        <ng-container *ngTemplateOutlet="dt.loadingBodyTemplate; context: {$implicit: columns, frozen: frozen}"></ng-container>
    </ng-container>
    <ng-container *ngIf="dt.isEmpty() && !dt.loading">
        <ng-container *ngTemplateOutlet="dt.emptyMessageTemplate; context: {$implicit: columns, frozen: frozen}"></ng-container>
    </ng-container>
    `,
    changeDetection: ChangeDetectionStrategy.Default,
    encapsulation: ViewEncapsulation.None
})
export class SuperTableBody extends TableBody implements OnDestroy {
    @Input("super-table-body") columns= [];

    @Input("pTableBodyTemplate") template: any;
    @Input() get value(): any[] {
        return this._value;
    }
    set value(val: any[]) {
        this._value = val;
        if (this.frozenRows) {
            this.updateFrozenRowStickyPosition();
        }

        if (this.dt.scrollable && this.dt.rowGroupMode === 'subheader') {
            this.updateFrozenRowGroupHeaderStickyPosition();
        }
    }

    @Input() frozen: boolean = false;

    @Input() frozenRows: boolean = false;  
      
    constructor(public dt: SuperTable, public tableService: TableService, public cd: ChangeDetectorRef, el: ElementRef) {
        super(dt, tableService, cd, el);
    }
}

@Directive({
    selector: '[super-SortableColumn]',
    host: {
        '[class.p-sortable-column]': 'isEnabled()',
        '[class.p-highlight]': 'sorted',
        '[attr.tabindex]': 'isEnabled() ? "0" : null',
        '[attr.role]': '"columnheader"',
        '[attr.aria-sort]': 'sortOrder'
    }
})
export class SuperSortableColumn extends SortableColumn implements OnInit, OnDestroy {
    @Input("super-SortableColumn") field= "";
    constructor(public dt: SuperTable) {
        super(dt);
    }

    ngOnInit() {
        super.ngOnInit();
    }

    ngOnDestroy() {
        super.ngOnDestroy();
    }

}

@Component({
    selector: 'super-sortIcon',
    template: `
        <i class="p-sortable-column-icon pi pi-fw" [ngClass]="{'pi-sort-amount-up-alt': sortOrder === 1, 'pi-sort-amount-down': sortOrder === -1, 'pi-sort-alt': sortOrder === 0}"></i>
        <span *ngIf="isMultiSorted()" class="p-sortable-column-badge">{{getBadgeValue()}}</span>
    `,
    changeDetection: ChangeDetectionStrategy.OnPush,
    encapsulation: ViewEncapsulation.None
})
export class SuperSortIcon extends SortIcon implements OnInit, OnDestroy {

    constructor(public dt: SuperTable, public cd: ChangeDetectorRef) {
        super(dt, cd);
    }

    ngOnInit() {
        super.ngOnInit();
    }

    ngOnDestroy() {
        super.ngOnDestroy();
    }
}

@Directive({
    selector: '[super-selectable-row]',
    host: {
        '[class.p-selectable-row]': 'isEnabled()',
        '[class.p-highlight]': 'selected',
        '[attr.tabindex]': 'isEnabled() ? 0 : undefined'
    }
})
export class SuperSelectableRow extends SelectableRow implements OnInit, OnDestroy {

    @Input("super-selectable-row") data: any;

    constructor(public dt: SuperTable, public tableService: TableService) {
        super(dt, tableService);
    }

    ngOnInit() {
        super.ngOnInit();
    }

    ngOnDestroy() {
        super.ngOnDestroy();
    }

}

@Directive({
    selector: '[super-ContextMenuRow]',
    host: {
        '[class.p-highlight-contextmenu]': 'selected',
        '[attr.tabindex]': 'isEnabled() ? 0 : undefined'
    }
})
export class SuperContextMenuRow extends ContextMenuRow {
    @Input("super-ContextMenuRow") data: any;
    constructor(public dt: SuperTable, public tableService: TableService, el: ElementRef) {
        super(dt, tableService, el);
    }
}

@Directive({
    selector: '[super-RowToggler]'
})
export class SuperRowToggler extends RowToggler {

    @Input('super-RowToggler') data: any;

    constructor(public dt: SuperTable) { 
        super(dt);
    }
}

@Directive({
    selector: '[super-ResizableColumn]'
})
export class SuperResizableColumn extends ResizableColumn implements AfterViewInit, OnDestroy {

    constructor(@Inject(DOCUMENT) document: Document, @Inject(PLATFORM_ID) platformId: any, renderer: Renderer2, dt: SuperTable, el: ElementRef, zone: NgZone) { 
        super(document, platformId, renderer, dt, el, zone);
    }

    ngAfterViewInit() {
        super.ngAfterViewInit();
    }

    ngOnDestroy() {
        super.ngOnDestroy();
    }
}

@Directive({
    selector: '[super-ReorderableColumn]'
})
export class SuperReorderableColumn extends ReorderableColumn implements AfterViewInit, OnDestroy {
    constructor(@Inject(PLATFORM_ID) platformId: any, renderer: Renderer2, dt: SuperTable, el: ElementRef, zone: NgZone) { 
        super(platformId, renderer, dt, el, zone);
    }

    ngAfterViewInit() {
        super.ngAfterViewInit();
    }

    ngOnDestroy() {
        super.ngOnDestroy();
    }

}

@Directive({
    selector: '[super-EditableColumn]'
})
export class SuperEditableColumn extends EditableColumn implements AfterViewInit {

    constructor(public dt: SuperTable, public el: ElementRef, public zone: NgZone) {
        super(dt, el, zone);
    }

    ngAfterViewInit() {
        super.ngAfterViewInit();
    }
}

@Directive({
    selector: '[pEditableRow]'
})
export class EditableRow {

    @Input("pEditableRow") data: any;

    @Input() pEditableRowDisabled= false;

    constructor(public el: ElementRef) {}

    isEnabled() {
        return this.pEditableRowDisabled !== true;
    }

}

@Directive({
    selector: '[pInitEditableRow]'
})
export class InitEditableRow {

    constructor(public dt: SuperTable, public editableRow: EditableRow) {}

    @HostListener('click', ['$event'])
    onClick(event: Event) {
        this.dt.initRowEdit(this.editableRow.data);
        event.preventDefault();
    }

}

@Directive({
    selector: '[pSaveEditableRow]'
})
export class SaveEditableRow {

    constructor(public dt: SuperTable, public editableRow: EditableRow) {}

    @HostListener('click', ['$event'])
    onClick(event: Event) {
        this.dt.saveRowEdit(this.editableRow.data, this.editableRow.el.nativeElement);
        event.preventDefault();
    }
}

@Directive({
    selector: '[pCancelEditableRow]'
})
export class CancelEditableRow {

    constructor(public dt: SuperTable, public editableRow: EditableRow) {}

    @HostListener('click', ['$event'])
    onClick(event: Event) {
        this.dt.cancelRowEdit(this.editableRow.data);
        event.preventDefault();
    }
}

@Component({
    selector: 'p-cellEditor',
    template: `
        <ng-container *ngIf="editing">
            <ng-container *ngTemplateOutlet="inputTemplate"></ng-container>
        </ng-container>
        <ng-container *ngIf="!editing">
            <ng-container *ngTemplateOutlet="outputTemplate"></ng-container>
        </ng-container>
    `,
    encapsulation: ViewEncapsulation.None
})
export class SuperCellEditor extends CellEditor implements AfterContentInit {


    constructor(public dt: SuperTable, @Optional() public editableColumn: SuperEditableColumn, @Optional() public editableRow: EditableRow) {
        super(dt, editableColumn, editableRow);
    }

    ngAfterContentInit() {
        super.ngAfterContentInit();
    }
}

@Component({
    selector: 'super-tableRadioButton',
    template: `
    <div class="p-radiobutton p-component" [ngClass]="{'p-radiobutton-focused':focused, 'p-radiobutton-disabled': disabled}" (click)="onClick($event)">
        <div class="p-hidden-accessible">
            <input type="radio" [attr.id]="inputId" [attr.name]="name" [checked]="checked" (focus)="onFocus()" (blur)="onBlur()"
            [disabled]="disabled" [attr.aria-label]="ariaLabel">
        </div>
        <div #box [ngClass]="{'p-radiobutton-box p-component':true, 'p-highlight':checked, 'p-focus':focused, 'p-disabled':disabled}" role="radio" [attr.aria-checked]="checked">
            <div class="p-radiobutton-icon"></div>
        </div>
    </div>
    `,
    changeDetection: ChangeDetectionStrategy.OnPush,
    encapsulation: ViewEncapsulation.None
})
export class SuperTableRadioButton extends TableRadioButton  {
    
    constructor(public dt: SuperTable, public cd: ChangeDetectorRef) {
        super(dt, cd);
    }

    ngOnInit() {
        this.checked = this.dt.isSelected(this.value);
    }

}

@Component({
    selector: 'super-tableCheckbox',
    template: `
        <div class="p-checkbox p-component" [ngClass]="{'p-checkbox-focused':focused, 'p-checkbox-disabled': disabled}" (click)="onClick($event)">
            <div class="p-hidden-accessible">
                <input type="checkbox" [attr.id]="inputId" [attr.name]="name" [checked]="checked" (focus)="onFocus()" (blur)="onBlur()" [disabled]="disabled"
                [attr.required]="required" [attr.aria-label]="ariaLabel">
            </div>
            <div #box [ngClass]="{'p-checkbox-box p-component':true,
                'p-highlight':checked, 'p-focus':focused, 'p-disabled':disabled}" role="checkbox" [attr.aria-checked]="checked">
                <span class="p-checkbox-icon" [ngClass]="{'pi pi-check':checked}"></span>
            </div>
        </div>
    `,
    changeDetection: ChangeDetectionStrategy.OnPush,
    encapsulation: ViewEncapsulation.None
})
export class SuperTableCheckbox extends TableCheckbox  {

    constructor(public dt: SuperTable, public tableService: TableService, public cd: ChangeDetectorRef) {
        super(dt, tableService, cd);
    }

    onClick(event: Event) {
        const dt = this.dt;
        if (dt.parent !== null){
            // perform the change using rows selected in the parent
            dt.selection = dt.parent.selection;
            dt.selectionKeys = dt.parent.selectionKeys;
        }
        super.onClick(event);
        let topParent = dt.parent;
        if (topParent !== null){
            while (topParent.parent){
                topParent = topParent.parent;
            }
            // insure the parent sees the change
            topParent.selection = dt.selection;
            topParent.selectionKeys = dt.selectionKeys;
        }
        if (topParent !== null){
            topParent.children.forEach(c=>{
                if (c !== dt){
                    c.setChildSelection(c, dt);
                }
            });
            // notify the component's owner
            topParent?.selectionChange.emit(topParent.selection);
        }
    }
}

@Component({
    selector: 'super-tableHeaderCheckbox',
    template: `
        <div class="p-checkbox p-component" [ngClass]="{'p-checkbox-focused':focused, 'p-checkbox-disabled': isDisabled()}" (click)="onClick($event)">
            <div class="p-hidden-accessible">
                <input #cb type="checkbox" [attr.id]="inputId" [attr.name]="name" [checked]="checked" (focus)="onFocus()" (blur)="onBlur()"
                [disabled]="isDisabled()" [attr.aria-label]="ariaLabel">
            </div>
            <div #box [ngClass]="{'p-checkbox-box':true,
                'p-highlight':checked, 'p-focus':focused, 'p-disabled': isDisabled()}" role="checkbox" [attr.aria-checked]="checked">
                <span class="p-checkbox-icon" [ngClass]="{'pi pi-check':checked}"></span>
            </div>
        </div>
    `,
    changeDetection: ChangeDetectionStrategy.OnPush,
    encapsulation: ViewEncapsulation.None
})
export class SuperTableHeaderCheckbox extends TableHeaderCheckbox  {
    constructor(public dt: SuperTable, public tableService: TableService, public cd: ChangeDetectorRef) {
        super(dt, tableService, cd);
    }
}

@Directive({
    selector: '[pReorderableRowHandle]'
})
export class ReorderableRowHandle implements AfterViewInit {

    @Input("pReorderableRowHandle") index= 0;

    constructor(public el: ElementRef) {}

    ngAfterViewInit() {
        DomHandler.addClass(this.el.nativeElement, 'p-datatable-reorderablerow-handle');
    }
}

@Directive({
    selector: '[super-reorderable-row]'
})
export class SuperReorderableRow extends ReorderableRow implements AfterViewInit {

    @Input("super-reorderable-row") index= 0;

    constructor(renderer: Renderer2, dt: SuperTable, el: ElementRef, zone: NgZone) {
        super (renderer, dt, el, zone);
    }

    ngAfterViewInit() {
        super.ngAfterViewInit();
    }
}

@Component({
    selector: 'super-columnFilterFormElement',
    template: `
        <ng-container *ngIf="filterTemplate; else builtInElement">
            <ng-container *ngTemplateOutlet="filterTemplate; context: {$implicit: filterConstraint.value, filterCallback: filterCallback}"></ng-container>
        </ng-container>
        <ng-template #builtInElement>
            <ng-container [ngSwitch]="type">
                <!--input *ngSwitchCase="'text'" type="text" pInputText [value]="filterConstraint?.value" (input)="onModelChange($event.target.value)"
                    (keydown.enter)="onTextInputEnterKeyDown($event)" [attr.placeholder]="placeholder"-->
                <p-inputNumber *ngSwitchCase="'numeric'" [ngModel]="filterConstraint?.value" (ngModelChange)="onModelChange($event)" (onKeyDown)="onNumericInputKeyDown($event)" [showButtons]="true"
                    [minFractionDigits]="minFractionDigits" [maxFractionDigits]="maxFractionDigits" [prefix]="prefix" [suffix]="suffix" [placeholder]="placeholder"
                    [mode]="currency ? 'currency' : 'decimal'" [locale]="locale" [localeMatcher]="localeMatcher" [currency]="currency" [currencyDisplay]="currencyDisplay" [useGrouping]="useGrouping"></p-inputNumber>
                <p-triStateCheckbox *ngSwitchCase="'boolean'" [ngModel]="filterConstraint?.value" (ngModelChange)="onModelChange($event)"></p-triStateCheckbox>
                <p-calendar *ngSwitchCase="'date'" [placeholder]="placeholder" [ngModel]="filterConstraint?.value" (ngModelChange)="onModelChange($event)"></p-calendar>
            </ng-container>
        </ng-template>
    `,
    encapsulation: ViewEncapsulation.None
})
export class SuperColumnFilterFormElement extends ColumnFilterFormElement implements OnInit {
    @Input() filterConstraint: any;    
    constructor(public dt: SuperTable, private cf: ColumnFilter) {
        super(dt, cf);
    }

    get showButtons(): boolean {
        return super.showButtons;
    }

    ngOnInit() {
        super.ngOnInit();
    }
    onModelChange(event: any) {
        (this.filterConstraint as any).value = event.target.value;

        if (this.type === 'boolean' || event.target.value === '') {
            this.dt._filter();
        }
    }
    onTextInputEnterKeyDown(event: any) {
        super.onTextInputEnterKeyDown(event);
    }

}

@Component({
    selector: 'super-columnFilter',
    template: `
        <div class="p-column-filter" [ngClass]="{'p-column-filter-row': display === 'row', 'p-column-filter-menu': display === 'menu'}">
            <super-columnFilterFormElement *ngIf="display === 'row'" class="p-fluid" [type]="type" [field]="field" [filterConstraint]="dt.filters[field]" [filterTemplate]="filterTemplate" [placeholder]="placeholder" [minFractionDigits]="minFractionDigits" [maxFractionDigits]="maxFractionDigits" [prefix]="prefix" [suffix]="suffix"
                                    [locale]="locale"  [localeMatcher]="localeMatcher" [currency]="currency" [currencyDisplay]="currencyDisplay" [useGrouping]="useGrouping"></super-columnFilterFormElement>
            <button #icon *ngIf="showMenuButton" type="button" class="p-column-filter-menu-button p-link" aria-haspopup="true" [attr.aria-expanded]="overlayVisible"
                [ngClass]="{'p-column-filter-menu-button-open': overlayVisible, 'p-column-filter-menu-button-active': hasFilter()}"
                (click)="toggleMenu()" (keydown)="onToggleButtonKeyDown($event)"><span class="pi pi-filter-icon pi-filter"></span></button>
            <button #icon *ngIf="showClearButton && display === 'row'" [ngClass]="{'p-hidden-space': !hasRowFilter()}" type="button" class="p-column-filter-clear-button p-link" (click)="clearFilter()"><span class="pi pi-filter-slash"></span></button>
            <div *ngIf="showMenu && overlayVisible" [ngClass]="{'p-column-filter-overlay p-component p-fluid': true, 'p-column-filter-overlay-menu': display === 'menu'}" (click)="onContentClick()"
                [@overlayAnimation]="'visible'" (@overlayAnimation.start)="onOverlayAnimationStart($event)" (@overlayAnimation.done)="onOverlayAnimationEnd($event)" (keydown.escape)="onEscape()">
                <ng-container *ngTemplateOutlet="headerTemplate; context: {$implicit: field}"></ng-container>
                <ul *ngIf="display === 'row'; else menu" class="p-column-filter-row-items">
                    <li class="p-column-filter-row-item" *ngFor="let matchMode of matchModes; let i = index;" (click)="onRowMatchModeChange(matchMode.value)" (keydown)="onRowMatchModeKeyDown($event)" (keydown.enter)="this.onRowMatchModeChange(matchMode.value)"
                        [ngClass]="{'p-highlight': isRowMatchModeSelected(matchMode.value)}" [attr.tabindex]="i === 0 ? '0' : null">{{matchMode.label}}</li>
                    <li class="p-column-filter-separator"></li>
                    <li class="p-column-filter-row-item" (click)="onRowClearItemClick()" (keydown)="onRowMatchModeKeyDown($event)" (keydown.enter)="onRowClearItemClick()">{{noFilterLabel}}</li>
                </ul>
                <ng-template #menu>
                    <div class="p-column-filter-operator" *ngIf="isShowOperator">
                        <p-dropdown [options]="operatorOptions" [ngModel]="operator" (ngModelChange)="onOperatorChange($event)" styleClass="p-column-filter-operator-dropdown"></p-dropdown>
                    </div>
                    <div class="p-column-filter-constraints">
                        <div *ngFor="let fieldConstraint of fieldConstraints; let i = index" class="p-column-filter-constraint">
                            <p-dropdown  *ngIf="showMatchModes && matchModes" [options]="matchModes" [ngModel]="fieldConstraint.matchMode" (ngModelChange)="onMenuMatchModeChange($event, fieldConstraint)" styleClass="p-column-filter-matchmode-dropdown"></p-dropdown>
                            <super-columnFilterFormElement [type]="type" [field]="field" [filterConstraint]="fieldConstraint" [filterTemplate]="filterTemplate" [placeholder]="placeholder"
                            [minFractionDigits]="minFractionDigits" [maxFractionDigits]="maxFractionDigits" [prefix]="prefix" [suffix]="suffix"
                            [locale]="locale"  [localeMatcher]="localeMatcher" [currency]="currency" [currencyDisplay]="currencyDisplay" [useGrouping]="useGrouping"></super-columnFilterFormElement>
                            <div>
                                <button *ngIf="showRemoveIcon" type="button" pButton icon="pi pi-trash" class="p-column-filter-remove-button p-button-text p-button-danger p-button-sm" (click)="removeConstraint(fieldConstraint)" pRipple [label]="removeRuleButtonLabel"></button>
                            </div>
                        </div>
                    </div>
                    <div class="p-column-filter-add-rule" *ngIf="isShowAddConstraint">
                        <button type="button" pButton [label]="addRuleButtonLabel" icon="pi pi-plus" class="p-column-filter-add-button p-button-text p-button-sm" (click)="addConstraint()" pRipple></button>
                    </div>
                    <div class="p-column-filter-buttonbar">
                        <button *ngIf="showClearButton" type="button" pButton class="p-button-outlined p-button-sm" (click)="clearFilter()" [label]="clearButtonLabel" pRipple></button>
                        <button *ngIf="showApplyButton" type="button" pButton (click)="applyFilter()" class="p-button-sm" [label]="applyButtonLabel" pRipple></button>
                    </div>
                </ng-template>
                <ng-container *ngTemplateOutlet="footerTemplate; context: {$implicit: field}"></ng-container>
            </div>
        </div>
    `,
    animations: [
        trigger('overlayAnimation', [
            transition(':enter', [
                style({opacity: 0, transform: 'scaleY(0.8)'}),
                animate('.12s cubic-bezier(0, 0, 0.2, 1)')
            ]),
            transition(':leave', [
                animate('.1s linear', style({ opacity: 0 }))
            ])
        ])
    ],
    encapsulation: ViewEncapsulation.None
})
export class SuperColumnFilter extends ColumnFilter implements AfterContentInit {
    constructor(@Inject(DOCUMENT) document: Document, el: ElementRef, dt: SuperTable, renderer: Renderer2, config: PrimeNGConfig, overlayService: OverlayService) {
        super(document, el, dt, renderer, config, overlayService,);
    }

    ngAfterContentInit() {
        super.ngAfterContentInit();
    }
}

@NgModule({
    imports: [CommonModule,PaginatorModule,InputTextModule,DropdownModule,ScrollingModule,FormsModule,ButtonModule,SelectButtonModule,CalendarModule,InputNumberModule,TriStateCheckboxModule],
    exports: [SuperTable,SharedModule,SuperSortableColumn,SuperSelectableRow,SuperRowToggler,SuperContextMenuRow,SuperResizableColumn,SuperReorderableColumn,SuperEditableColumn,SuperCellEditor,SuperSortIcon,
            SuperTableRadioButton,SuperTableCheckbox,SuperTableHeaderCheckbox,ReorderableRowHandle,SuperReorderableRow,EditableRow,InitEditableRow,SaveEditableRow,CancelEditableRow,ScrollingModule,SuperColumnFilter],
    declarations: [SuperTable,SuperSortableColumn,SuperSelectableRow,SuperRowToggler,SuperContextMenuRow,SuperResizableColumn,SuperReorderableColumn,SuperEditableColumn,SuperCellEditor,SuperTableBody,SuperSortIcon,
            SuperTableRadioButton,SuperTableCheckbox,SuperTableHeaderCheckbox,ReorderableRowHandle,SuperReorderableRow,EditableRow,InitEditableRow,SaveEditableRow,CancelEditableRow,SuperColumnFilter,SuperColumnFilterFormElement]
})
export class SuperTableModule { }